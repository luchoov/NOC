// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Events;
using NOC.Shared.Infrastructure.Data;
using StackExchange.Redis;

namespace NOC.Worker.Messaging;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<Worker> logger) : BackgroundService
{
    private const string StreamName = "stream:messaging:incoming";
    private const string ConsumerGroupName = "messaging-workers";

    private readonly string _consumerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
    private readonly int _reopenWindowHours = Math.Clamp(configuration.GetValue<int?>("Workers:Messaging:ReopenWindowHours") ?? 24, 1, 168);
    private readonly TimeSpan _pollDelay = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redisDb = redis.GetDatabase();
        await EnsureConsumerGroupAsync(redisDb);

        logger.LogInformation(
            "Messaging worker started. Stream={Stream}, Group={Group}, Consumer={Consumer}, ReopenWindowHours={ReopenWindowHours}",
            StreamName,
            ConsumerGroupName,
            _consumerName,
            _reopenWindowHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await redisDb.StreamReadGroupAsync(StreamName, ConsumerGroupName, _consumerName, ">", count: 20);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to read from Redis stream {Stream}", StreamName);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(_pollDelay, stoppingToken);
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    await ProcessEntryAsync(entry, stoppingToken);
                    await redisDb.StreamAcknowledgeAsync(StreamName, ConsumerGroupName, entry.Id);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed processing stream entry {EntryId}", entry.Id);
                    // Ack to prevent poison-message loops for now.
                    await redisDb.StreamAcknowledgeAsync(StreamName, ConsumerGroupName, entry.Id);
                }
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase redisDb)
    {
        try
        {
            await redisDb.StreamCreateConsumerGroupAsync(
                key: StreamName,
                groupName: ConsumerGroupName,
                position: "0-0",
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Consumer group {Group} already exists for stream {Stream}", ConsumerGroupName, StreamName);
        }
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        var type = entry.Values.FirstOrDefault(v => v.Name == "type").Value.ToString();
        var payload = entry.Values.FirstOrDefault(v => v.Name == "payload").Value.ToString();

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(payload))
        {
            logger.LogWarning("Stream entry {EntryId} missing type or payload", entry.Id);
            return;
        }

        if (!string.Equals(type, nameof(EvolutionMessageWebhookReceivedEvent), StringComparison.Ordinal))
        {
            logger.LogDebug("Ignoring unsupported event type {EventType}", type);
            return;
        }

        var webhookEvent = JsonSerializer.Deserialize<EvolutionMessageWebhookReceivedEvent>(payload);
        if (webhookEvent is null)
        {
            logger.LogWarning("Could not deserialize webhook payload for entry {EntryId}", entry.Id);
            return;
        }

        await HandleIncomingWebhookAsync(webhookEvent, ct);
    }

    private async Task HandleIncomingWebhookAsync(EvolutionMessageWebhookReceivedEvent evt, CancellationToken ct)
    {
        var parsed = ParseIncomingPayload(evt.RawPayload);
        if (string.IsNullOrWhiteSpace(parsed.Phone))
        {
            logger.LogWarning("Incoming webhook without phone. EventId={EventId}, InboxId={InboxId}", evt.EventId, evt.InboxId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();

        // Durable idempotency barrier: UNIQUE index on messages.external_id.
        if (await db.Messages.AnyAsync(m => m.ExternalId == evt.ExternalId, ct))
        {
            logger.LogDebug("Skipping duplicate inbound message with external ID {ExternalId}", evt.ExternalId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == parsed.Phone, ct);
        if (contact is null)
        {
            contact = new Contact
            {
                Id = Guid.CreateVersion7(),
                Phone = parsed.Phone,
                Name = parsed.ContactName,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Contacts.Add(contact);
        }
        else if (!string.IsNullOrWhiteSpace(parsed.ContactName) && string.IsNullOrWhiteSpace(contact.Name))
        {
            contact.Name = parsed.ContactName;
            contact.UpdatedAt = now;
        }

        var conversation = await db.Conversations
            .Where(c => c.InboxId == evt.InboxId && c.ContactId == contact.Id && c.Status != ConversationStatus.RESOLVED && c.Status != ConversationStatus.ARCHIVED)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = await ReopenResolvedConversationIfEligibleAsync(db, evt.InboxId, contact.Id, now, ct)
                ?? CreateConversation(evt.InboxId, contact.Id, now);
        }

        var inboundMessage = new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversation.Id,
            ExternalId = evt.ExternalId,
            Direction = MessageDirection.INBOUND,
            Type = ParseIncomingMessageType(parsed),
            Content = parsed.Content,
            ProviderMetadata = evt.RawPayload,
            CreatedAt = now,
        };

        db.Messages.Add(inboundMessage);

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = BuildPreview(parsed.Content);
        conversation.LastMessageDirection = MessageDirection.INBOUND.ToString();
        conversation.LastInboundAt = now;
        conversation.UnreadCount += 1;
        conversation.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExternalIdUniqueViolation(ex))
        {
            logger.LogDebug(ex, "Duplicate external message detected during save. ExternalId={ExternalId}", evt.ExternalId);
        }
    }

    private async Task<Conversation?> ReopenResolvedConversationIfEligibleAsync(
        NocDbContext db,
        Guid inboxId,
        Guid contactId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var reopenThreshold = now.AddHours(-_reopenWindowHours);

        var resolvedConversation = await db.Conversations
            .Where(c => c.InboxId == inboxId && c.ContactId == contactId && c.Status == ConversationStatus.RESOLVED && c.ResolvedAt != null)
            .OrderByDescending(c => c.ResolvedAt)
            .FirstOrDefaultAsync(ct);

        if (resolvedConversation is null || resolvedConversation.ResolvedAt < reopenThreshold)
            return null;

        resolvedConversation.Status = ConversationStatus.OPEN;
        resolvedConversation.ResolvedAt = null;
        resolvedConversation.ClosedBy = null;
        resolvedConversation.ReopenedCount += 1;
        resolvedConversation.UpdatedAt = now;

        db.Messages.Add(new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = resolvedConversation.Id,
            Direction = MessageDirection.INBOUND,
            Type = MessageType.SYSTEM,
            Content = "Conversation reopened due to inbound customer message",
            IsPrivateNote = true,
            ProviderMetadata = "{}",
            CreatedAt = now,
        });

        return resolvedConversation;
    }

    private static Conversation CreateConversation(Guid inboxId, Guid contactId, DateTimeOffset now)
    {
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            InboxId = inboxId,
            ContactId = contactId,
            Status = ConversationStatus.OPEN,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    private static bool IsExternalIdUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("uq_msg_external_id", StringComparison.OrdinalIgnoreCase) == true ||
               ex.Message.Contains("uq_msg_external_id", StringComparison.OrdinalIgnoreCase);
    }

    private static MessageType ParseIncomingMessageType(IncomingPayload payload)
    {
        if (payload.HasMedia)
            return MessageType.IMAGE;
        return MessageType.TEXT;
    }

    private static string BuildPreview(string? content, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;
        var trimmed = content.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static IncomingPayload ParseIncomingPayload(string rawPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var root = document.RootElement;

            var phone = NormalizePhone(
                TryGetPath(root, "data", "key", "remoteJid")
                ?? TryGetPath(root, "data", "from")
                ?? TryGetPath(root, "from")
                ?? TryGetPath(root, "sender", "id")
                ?? string.Empty);

            var contactName =
                TryGetPath(root, "data", "pushName")
                ?? TryGetPath(root, "data", "sender", "pushName")
                ?? TryGetPath(root, "sender", "name");

            var text =
                TryGetPath(root, "data", "message", "conversation")
                ?? TryGetPath(root, "data", "message", "extendedTextMessage", "text")
                ?? TryGetPath(root, "message", "conversation")
                ?? TryGetPath(root, "text")
                ?? string.Empty;

            var hasMedia =
                !string.IsNullOrWhiteSpace(TryGetPath(root, "data", "message", "imageMessage", "url")) ||
                !string.IsNullOrWhiteSpace(TryGetPath(root, "data", "message", "videoMessage", "url")) ||
                !string.IsNullOrWhiteSpace(TryGetPath(root, "data", "message", "documentMessage", "url"));

            return new IncomingPayload(phone, text, contactName, hasMedia);
        }
        catch (JsonException)
        {
            return new IncomingPayload(string.Empty, string.Empty, null, false);
        }
    }

    private static string? TryGetPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : current.ToString();
    }

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        // Evolution often sends `number@s.whatsapp.net`.
        var cleaned = value.Split('@')[0];
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());
        return digits;
    }

    private sealed record IncomingPayload(string Phone, string Content, string? ContactName, bool HasMedia);
}

