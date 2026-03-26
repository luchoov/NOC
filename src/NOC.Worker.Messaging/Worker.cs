// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Events;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Shared.Infrastructure.Storage;
using StackExchange.Redis;

namespace NOC.Worker.Messaging;

public class Worker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    IEvolutionApiClient evolutionApiClient,
    IMediaStorageService mediaStorage,
    IConfiguration configuration,
    ILogger<Worker> logger) : BackgroundService
{
    private const string StreamName = "stream:messaging:incoming";
    private const string ReceiptsStreamName = "stream:status:receipts";
    private const string ConsumerGroupName = "messaging-workers";

    private readonly string _consumerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}"[..32];
    private readonly int _reopenWindowHours = Math.Clamp(configuration.GetValue<int?>("Workers:Messaging:ReopenWindowHours") ?? 24, 1, 168);
    private readonly TimeSpan _pollDelay = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redisDb = redis.GetDatabase();
        await EnsureConsumerGroupAsync(redisDb, StreamName);
        await EnsureConsumerGroupAsync(redisDb, ReceiptsStreamName);

        try { await mediaStorage.EnsureBucketAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to ensure MinIO bucket on startup"); }

        logger.LogInformation(
            "Messaging worker started. Consumer={Consumer}, ReopenWindowHours={ReopenWindowHours}",
            _consumerName,
            _reopenWindowHours);

        // Run both consumer loops concurrently
        await Task.WhenAll(
            ConsumeStreamAsync(redisDb, StreamName, ProcessEntryAsync, stoppingToken),
            ConsumeStreamAsync(redisDb, ReceiptsStreamName, ProcessReceiptEntryAsync, stoppingToken));
    }

    private async Task ConsumeStreamAsync(IDatabase redisDb, string stream,
        Func<StreamEntry, CancellationToken, Task> handler, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await redisDb.StreamReadGroupAsync(stream, ConsumerGroupName, _consumerName, ">", count: 20);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to read from Redis stream {Stream}", stream);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(_pollDelay, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                try
                {
                    await handler(entry, ct);
                    await redisDb.StreamAcknowledgeAsync(stream, ConsumerGroupName, entry.Id);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed processing stream entry {EntryId} from {Stream}", entry.Id, stream);
                    await redisDb.StreamAcknowledgeAsync(stream, ConsumerGroupName, entry.Id);
                }
            }
        }
    }

    private async Task EnsureConsumerGroupAsync(IDatabase redisDb, string stream)
    {
        try
        {
            await redisDb.StreamCreateConsumerGroupAsync(
                key: stream,
                groupName: ConsumerGroupName,
                position: "0-0",
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogDebug("Consumer group {Group} already exists for stream {Stream}", ConsumerGroupName, stream);
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

        // Check if this is a send-message event (sent from phone)
        if (webhookEvent.EventType == "EVOLUTION_SEND_MESSAGE_WEBHOOK_RECEIVED")
        {
            await HandleSendMessageWebhookAsync(webhookEvent, ct);
            return;
        }

        await HandleIncomingWebhookAsync(webhookEvent, ct);
    }

    private async Task ProcessReceiptEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        var type = entry.Values.FirstOrDefault(v => v.Name == "type").Value.ToString();
        var payload = entry.Values.FirstOrDefault(v => v.Name == "payload").Value.ToString();

        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(payload))
        {
            logger.LogWarning("Receipt stream entry {EntryId} missing type or payload", entry.Id);
            return;
        }

        if (!string.Equals(type, nameof(EvolutionReceiptWebhookReceivedEvent), StringComparison.Ordinal))
        {
            logger.LogDebug("Ignoring unsupported receipt event type {EventType}", type);
            return;
        }

        var receiptEvent = JsonSerializer.Deserialize<EvolutionReceiptWebhookReceivedEvent>(payload);
        if (receiptEvent is null)
        {
            logger.LogWarning("Could not deserialize receipt payload for entry {EntryId}", entry.Id);
            return;
        }

        await HandleReceiptAsync(receiptEvent, ct);
    }

    private async Task HandleIncomingWebhookAsync(EvolutionMessageWebhookReceivedEvent evt, CancellationToken ct)
    {
        var parsed = ParseIncomingPayload(evt.RawPayload);
        if (parsed.FromMe)
        {
            logger.LogDebug("Ignoring outbound/self webhook for ExternalId={ExternalId}", evt.ExternalId);
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

        var phone = parsed.Phone;
        var isLidBased = LooksLikeLidJid(parsed.RemoteJid);

        if (isLidBased)
        {
            // If the phone was derived from a @lid JID (no direct phone alternative),
            // we MUST resolve the real number. The @lid digits are NOT a valid phone.
            string? resolvedPhone = null;
            if (!parsed.HasDirectPhoneAlternative)
            {
                resolvedPhone = await TryResolveInboundPhoneAsync(
                    scope.ServiceProvider,
                    db,
                    evt.InboxId,
                    parsed,
                    ct);
            }

            if (!string.IsNullOrWhiteSpace(resolvedPhone))
            {
                logger.LogInformation(
                    "Resolved inbound WhatsApp LID for inbox {InboxId}. RemoteJid={RemoteJid}, Number={Number}",
                    evt.InboxId,
                    parsed.RemoteJid,
                    resolvedPhone);
                phone = resolvedPhone;
            }
            else if (!parsed.HasDirectPhoneAlternative)
            {
                // Could not resolve — do NOT create a contact with the @lid digits as phone
                logger.LogWarning(
                    "Could not resolve real phone for @lid inbound. Dropping message. InboxId={InboxId}, RemoteJid={RemoteJid}, ExternalId={ExternalId}",
                    evt.InboxId,
                    parsed.RemoteJid,
                    evt.ExternalId);
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            logger.LogWarning("Incoming webhook without phone. EventId={EventId}, InboxId={InboxId}", evt.EventId, evt.InboxId);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (contact is null)
        {
            contact = new Contact
            {
                Id = Guid.CreateVersion7(),
                Phone = phone,
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
            db.Conversations.Add(conversation);
        }

        var messageType = ParseIncomingMessageType(parsed);
        var messageContent = !string.IsNullOrWhiteSpace(parsed.MediaCaption) ? parsed.MediaCaption : parsed.Content;

        var inboundMessage = new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversation.Id,
            ExternalId = evt.ExternalId,
            Direction = MessageDirection.INBOUND,
            Type = messageType,
            Content = messageContent,
            MediaMimeType = parsed.MediaMimeType,
            MediaFilename = parsed.MediaFileName,
            ProviderMetadata = evt.RawPayload,
            CreatedAt = now,
        };

        // Download and store media from Evolution API
        if (parsed.HasMedia && !string.IsNullOrWhiteSpace(parsed.RawMessageJson))
        {
            try
            {
                var inbox = await db.Inboxes
                    .AsNoTracking()
                    .Include(i => i.ProxyOutbound)
                    .FirstOrDefaultAsync(i => i.Id == evt.InboxId, ct);

                if (inbox is not null && !string.IsNullOrWhiteSpace(inbox.EvolutionInstanceName))
                {
                    var proxyOptions = BuildEvolutionProxyOptions(scope.ServiceProvider, inbox.ProxyOutbound);
                    var messageJson = JsonNode.Parse(parsed.RawMessageJson) as JsonObject ?? new JsonObject();

                    var downloadResponse = await evolutionApiClient.DownloadMediaAsync(
                        inbox.EvolutionInstanceName,
                        new EvolutionMediaDownloadRequest { Message = messageJson },
                        proxyOptions,
                        ct);

                    if (!string.IsNullOrWhiteSpace(downloadResponse.Base64))
                    {
                        var bytes = Convert.FromBase64String(downloadResponse.Base64);
                        var mimeType = parsed.MediaMimeType ?? downloadResponse.MimeType ?? "application/octet-stream";
                        var fileName = parsed.MediaFileName ?? downloadResponse.FileName ?? GenerateMediaFileName(mimeType);
                        var objectKey = $"{evt.InboxId}/{now:yyyy/MM}/{inboundMessage.Id}/{fileName}";

                        using var ms = new MemoryStream(bytes);
                        await mediaStorage.UploadAsync(ms, objectKey, mimeType, bytes.Length, ct);

                        inboundMessage.MediaUrl = objectKey;
                        inboundMessage.MediaMimeType = mimeType;
                        inboundMessage.MediaFilename = fileName;
                        inboundMessage.MediaSizeBytes = bytes.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download/store media for message {MessageId}", inboundMessage.Id);
            }
        }

        db.Messages.Add(inboundMessage);

        var preview = !string.IsNullOrWhiteSpace(messageContent)
            ? BuildPreview(messageContent)
            : BuildMediaPreview(messageType);

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = preview;
        conversation.LastMessageDirection = MessageDirection.INBOUND.ToString();
        conversation.LastInboundAt = now;
        conversation.UnreadCount += 1;
        conversation.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);

            // Publish to Redis Pub/Sub for SignalR bridge in NOC.Web
            await PublishSignalREventAsync(evt.InboxId, conversation.Id, inboundMessage);
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

    private async Task<string?> TryResolveInboundPhoneAsync(
        IServiceProvider services,
        NocDbContext db,
        Guid inboxId,
        IncomingPayload payload,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.RemoteJid))
            return null;

        // Strategy 1: Check if we already have a conversation in this inbox whose
        // last outbound used the real number. This is the most reliable approach
        // because it uses data we already have.
        if (!string.IsNullOrWhiteSpace(payload.ContactName))
        {
            var existingContact = await db.Contacts
                .AsNoTracking()
                .Where(c => c.Name == payload.ContactName)
                .Where(c => db.Conversations.Any(conv => conv.ContactId == c.Id && conv.InboxId == inboxId))
                .FirstOrDefaultAsync(ct);

            if (existingContact is not null && !string.IsNullOrWhiteSpace(existingContact.Phone))
            {
                logger.LogInformation(
                    "Resolved @lid via existing contact match. Name={Name}, Phone={Phone}",
                    payload.ContactName, existingContact.Phone);
                return existingContact.Phone;
            }
        }

        // Strategy 2: Ask Evolution API for contacts and try to match
        var inbox = await db.Inboxes
            .AsNoTracking()
            .Include(i => i.ProxyOutbound)
            .FirstOrDefaultAsync(i => i.Id == inboxId, ct);

        if (inbox is null || string.IsNullOrWhiteSpace(inbox.EvolutionInstanceName))
            return null;

        try
        {
            var evolutionApiClient = services.GetRequiredService<IEvolutionApiClient>();
            var proxyOptions = BuildEvolutionProxyOptions(services, inbox.ProxyOutbound);
            var contactsPayload = await evolutionApiClient.FindContactsAsync(
                inbox.EvolutionInstanceName,
                request: null,
                proxy: proxyOptions,
                cancellationToken: ct);

            var contacts = ExtractEvolutionContacts(contactsPayload.Payload);
            if (contacts.Count == 0)
                return null;

            var lidContact = contacts.FirstOrDefault(contact =>
                string.Equals(contact.RemoteJid, payload.RemoteJid, StringComparison.OrdinalIgnoreCase));

            var candidateContacts = contacts
                .Where(contact => IsResolvableWhatsappJid(contact.RemoteJid))
                .ToList();

            string? resolved = null;
            if (!string.IsNullOrWhiteSpace(lidContact?.ProfilePicUrl))
            {
                resolved = SelectUniqueNumber(candidateContacts.Where(contact =>
                    string.Equals(contact.ProfilePicUrl, lidContact.ProfilePicUrl, StringComparison.Ordinal) &&
                    string.Equals(contact.PushName, lidContact.PushName ?? payload.ContactName, StringComparison.Ordinal)));

                resolved ??= SelectUniqueNumber(candidateContacts.Where(contact =>
                    string.Equals(contact.ProfilePicUrl, lidContact.ProfilePicUrl, StringComparison.Ordinal)));
            }

            if (resolved is null && !string.IsNullOrWhiteSpace(lidContact?.PushName ?? payload.ContactName))
            {
                var targetName = lidContact?.PushName ?? payload.ContactName;
                resolved = SelectUniqueNumber(candidateContacts.Where(contact =>
                    string.Equals(contact.PushName, targetName, StringComparison.Ordinal)));
            }

            return resolved;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve @lid via Evolution API for inbox {InboxId}", inboxId);
            return null;
        }
    }

    private static EvolutionProxyOptions? BuildEvolutionProxyOptions(IServiceProvider services, ProxyOutbound? proxy)
    {
        if (proxy is null)
            return null;

        string? password = null;
        if (!string.IsNullOrWhiteSpace(proxy.EncryptedPassword))
        {
            var encryptor = services.GetService<AesGcmEncryptor>();
            if (encryptor is null)
                throw new InvalidOperationException("Encryption is not configured for proxy credentials.");

            password = encryptor.Decrypt(proxy.EncryptedPassword);
        }

        return new EvolutionProxyOptions(
            proxy.Protocol,
            proxy.Host,
            proxy.Port,
            proxy.Username,
            password);
    }

    private async Task PublishSignalREventAsync(Guid inboxId, Guid conversationId, Message message)
    {
        try
        {
            var redisDb = redis.GetDatabase();
            var payload = JsonSerializer.Serialize(new
            {
                Event = "MessageReceived",
                InboxId = inboxId.ToString(),
                ConversationId = conversationId.ToString(),
                Payload = new
                {
                    id = message.Id.ToString(),
                    conversationId = conversationId.ToString(),
                    externalId = message.ExternalId,
                    direction = message.Direction.ToString(),
                    type = message.Type.ToString(),
                    content = message.Content,
                    mediaUrl = message.MediaUrl,
                    mediaMimeType = message.MediaMimeType,
                    mediaFilename = message.MediaFilename,
                    mediaSizeBytes = message.MediaSizeBytes,
                    deliveryStatus = message.DeliveryStatus?.ToString(),
                    isPrivateNote = message.IsPrivateNote,
                    createdAt = message.CreatedAt.ToString("o"),
                },
            });

            await redisDb.PublishAsync(RedisChannel.Literal("signalr:events"), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish SignalR event for message {MessageId}", message.Id);
        }
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
        return payload.MediaKind switch
        {
            "imageMessage" => MessageType.IMAGE,
            "videoMessage" => MessageType.VIDEO,
            "audioMessage" or "pttMessage" => MessageType.AUDIO,
            "documentMessage" or "documentWithCaptionMessage" => MessageType.DOCUMENT,
            "stickerMessage" => MessageType.STICKER,
            _ when payload.HasMedia => MessageType.IMAGE,
            _ => MessageType.TEXT,
        };
    }

    private static string BuildMediaPreview(MessageType type)
    {
        return type switch
        {
            MessageType.IMAGE => "\ud83d\udcf7 Imagen",
            MessageType.VIDEO => "\ud83c\udfa5 Video",
            MessageType.AUDIO => "\ud83c\udfb5 Audio",
            MessageType.DOCUMENT => "\ud83d\udcc4 Documento",
            MessageType.STICKER => "Sticker",
            _ => "[Media]",
        };
    }

    private static string GenerateMediaFileName(string mimeType)
    {
        var ext = mimeType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "video/mp4" => ".mp4",
            "audio/ogg" or "audio/ogg; codecs=opus" => ".ogg",
            "audio/mpeg" => ".mp3",
            "audio/mp4" => ".m4a",
            "application/pdf" => ".pdf",
            _ when mimeType.Contains('/') => "." + mimeType.Split('/')[1].Split(';')[0],
            _ => ".bin",
        };
        return $"media{ext}";
    }

    private static string BuildPreview(string? content, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;
        var trimmed = content.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static readonly string[] MediaMessageTypes =
    [
        "imageMessage", "videoMessage", "audioMessage", "documentMessage",
        "stickerMessage", "pttMessage", "documentWithCaptionMessage",
    ];

    private static IncomingPayload ParseIncomingPayload(string rawPayload)
    {
        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var root = document.RootElement;

            var fromMe =
                TryGetBooleanPath(root, "data", "key", "fromMe")
                ?? TryGetBooleanPath(root, "key", "fromMe")
                ?? false;

            var directPhoneCandidate =
                TryGetPath(root, "data", "key", "remoteJidAlt")
                ?? TryGetPath(root, "key", "remoteJidAlt")
                ?? TryGetPath(root, "data", "remoteJidAlt")
                ?? TryGetPath(root, "remoteJidAlt")
                ?? TryGetPath(root, "data", "senderPn")
                ?? TryGetPath(root, "senderPn")
                ?? TryGetPath(root, "data", "participantPn")
                ?? TryGetPath(root, "participantPn");

            var remoteJid =
                TryGetPath(root, "data", "key", "remoteJid")
                ?? TryGetPath(root, "key", "remoteJid");

            var phone = NormalizePhone(
                directPhoneCandidate
                ?? remoteJid
                ?? TryGetPath(root, "data", "from")
                ?? TryGetPath(root, "from")
                ?? TryGetPath(root, "sender", "id")
                ?? string.Empty);

            var contactName =
                TryGetPath(root, "data", "pushName")
                ?? TryGetPath(root, "pushName")
                ?? TryGetPath(root, "data", "sender", "pushName")
                ?? TryGetPath(root, "sender", "name");

            var text =
                TryGetPath(root, "data", "message", "conversation")
                ?? TryGetPath(root, "data", "message", "extendedTextMessage", "text")
                ?? TryGetPath(root, "message", "conversation")
                ?? TryGetPath(root, "message", "extendedTextMessage", "text")
                ?? TryGetPath(root, "text")
                ?? string.Empty;

            // Detect media type and extract metadata
            string? mediaKind = null;
            string? mediaMimeType = null;
            string? mediaCaption = null;
            string? mediaFileName = null;
            string? rawMessageJson = null;
            var hasMedia = false;

            // Try both data.message and message paths
            JsonElement messageElement = default;
            var hasMessageElement = false;

            if (root.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("message", out messageElement))
                hasMessageElement = true;
            else if (root.TryGetProperty("message", out messageElement))
                hasMessageElement = true;

            if (hasMessageElement && messageElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var kind in MediaMessageTypes)
                {
                    if (messageElement.TryGetProperty(kind, out var mediaEl) && mediaEl.ValueKind == JsonValueKind.Object)
                    {
                        hasMedia = true;
                        mediaKind = kind;
                        mediaMimeType = TryGetPath(mediaEl, "mimetype");
                        mediaCaption = TryGetPath(mediaEl, "caption");
                        mediaFileName = TryGetPath(mediaEl, "fileName");

                        // For documentWithCaptionMessage, the actual document is nested
                        if (kind == "documentWithCaptionMessage" && mediaEl.TryGetProperty("message", out var innerMsg))
                        {
                            if (innerMsg.TryGetProperty("documentMessage", out var innerDoc))
                            {
                                mediaMimeType ??= TryGetPath(innerDoc, "mimetype");
                                mediaFileName ??= TryGetPath(innerDoc, "fileName");
                                mediaCaption ??= TryGetPath(innerDoc, "caption");
                            }
                        }

                        // Store the full key+message wrapper for Evolution's getBase64FromMediaMessage
                        // Evolution API needs { key: {...}, message: {...} } to download media
                        JsonElement keyElement = default;
                        var hasKey = false;
                        if (root.TryGetProperty("data", out var dataForKey) && dataForKey.TryGetProperty("key", out keyElement))
                            hasKey = true;
                        else if (root.TryGetProperty("key", out keyElement))
                            hasKey = true;

                        if (hasKey)
                        {
                            rawMessageJson = JsonSerializer.Serialize(new
                            {
                                key = JsonSerializer.Deserialize<JsonElement>(keyElement.GetRawText()),
                                message = JsonSerializer.Deserialize<JsonElement>(messageElement.GetRawText()),
                            });
                        }
                        else
                        {
                            rawMessageJson = messageElement.GetRawText();
                        }
                        break;
                    }
                }
            }

            return new IncomingPayload(
                phone,
                text,
                contactName,
                hasMedia,
                mediaKind,
                mediaMimeType,
                mediaCaption,
                mediaFileName,
                rawMessageJson,
                fromMe,
                remoteJid,
                !string.IsNullOrWhiteSpace(directPhoneCandidate));
        }
        catch (JsonException)
        {
            return new IncomingPayload(string.Empty, string.Empty, null, false, null, null, null, null, null, false, null, false);
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

    private static bool? TryGetBooleanPath(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;

            if (!current.TryGetProperty(segment, out current))
                return null;
        }

        return current.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(current.GetString(), out var parsed) => parsed,
            _ => null,
        };
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

    private static IReadOnlyList<EvolutionContactCandidate> ExtractEvolutionContacts(JsonObject payload)
    {
        var contacts = new List<EvolutionContactCandidate>();
        var items =
            payload["items"] as JsonArray
            ?? payload["contacts"] as JsonArray
            ?? payload["records"] as JsonArray;

        if (items is null)
            return contacts;

        foreach (var item in items)
        {
            if (item is not JsonObject jsonObject)
                continue;

            contacts.Add(new EvolutionContactCandidate(
                TryGetJsonString(jsonObject, "remoteJid"),
                TryGetJsonString(jsonObject, "pushName"),
                TryGetJsonString(jsonObject, "profilePicUrl")));
        }

        return contacts;
    }

    private static string? SelectUniqueNumber(IEnumerable<EvolutionContactCandidate> contacts)
    {
        var numbers = contacts
            .Select(contact => NormalizePhone(contact.RemoteJid))
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return numbers.Count == 1 ? numbers[0] : null;
    }

    private static bool LooksLikeLidJid(string? remoteJid)
    {
        return !string.IsNullOrWhiteSpace(remoteJid) &&
               remoteJid.EndsWith("@lid", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResolvableWhatsappJid(string? remoteJid)
    {
        return !string.IsNullOrWhiteSpace(remoteJid) &&
               !remoteJid.EndsWith("@lid", StringComparison.OrdinalIgnoreCase) &&
               !remoteJid.EndsWith("@g.us", StringComparison.OrdinalIgnoreCase) &&
               !remoteJid.EndsWith("@broadcast", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(NormalizePhone(remoteJid));
    }

    private static string? TryGetJsonString(JsonObject jsonObject, string propertyName)
    {
        if (jsonObject[propertyName] is not JsonValue value)
            return null;

        return value.TryGetValue<string>(out var result) ? result : null;
    }

    // ── Receipt handling (delivery/read status updates) ──────────────────

    private async Task HandleReceiptAsync(EvolutionReceiptWebhookReceivedEvent evt, CancellationToken ct)
    {
        var deliveryStatus = evt.StatusCode switch
        {
            2 => DeliveryStatus.SENT,
            3 => DeliveryStatus.DELIVERED,
            4 or 5 => DeliveryStatus.READ, // 5 = PLAYED (audio/video)
            _ => (DeliveryStatus?)null,
        };

        if (deliveryStatus is null)
        {
            logger.LogDebug("Ignoring unknown receipt status code {StatusCode} for {ExternalId}", evt.StatusCode, evt.ExternalId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();

        var message = await db.Messages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.ExternalId == evt.ExternalId, ct);

        if (message is null)
        {
            logger.LogDebug("No message found for ExternalId={ExternalId}, ignoring receipt", evt.ExternalId);
            return;
        }

        // Only update if status is "more advanced" (SENT < DELIVERED < READ)
        if (message.DeliveryStatus.HasValue && message.DeliveryStatus.Value >= deliveryStatus.Value)
        {
            logger.LogDebug("Message {MessageId} already at {CurrentStatus}, ignoring {NewStatus}",
                message.Id, message.DeliveryStatus, deliveryStatus);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        message.DeliveryStatus = deliveryStatus.Value;
        message.DeliveryUpdatedAt = now;

        // Create audit trail
        db.MessageStatusEvents.Add(new MessageStatusEvent
        {
            Id = Guid.CreateVersion7(),
            MessageId = message.Id,
            Status = deliveryStatus.Value,
            ProviderCode = evt.StatusCode.ToString(),
            OccurredAt = now,
        });

        // Update campaign recipient if this is a campaign message
        var campaignRecipient = await db.CampaignRecipients
            .FirstOrDefaultAsync(cr => cr.ExternalId == evt.ExternalId, ct);

        if (campaignRecipient is not null)
        {
            if (deliveryStatus == DeliveryStatus.DELIVERED && campaignRecipient.Status is "SENT")
            {
                campaignRecipient.Status = "DELIVERED";
                campaignRecipient.DeliveredAt = now;
            }
            else if (deliveryStatus == DeliveryStatus.READ && campaignRecipient.Status is "SENT" or "DELIVERED")
            {
                campaignRecipient.Status = "READ";
                campaignRecipient.ReadAt = now;
                if (campaignRecipient.DeliveredAt is null)
                    campaignRecipient.DeliveredAt = now;
            }
        }

        await db.SaveChangesAsync(ct);

        // Publish SignalR update for real-time UI
        try
        {
            var redisDb = redis.GetDatabase();
            var signalRPayload = JsonSerializer.Serialize(new
            {
                Event = "MessageStatusUpdate",
                InboxId = message.Conversation.InboxId.ToString(),
                ConversationId = message.ConversationId.ToString(),
                Payload = new
                {
                    messageId = message.Id.ToString(),
                    deliveryStatus = deliveryStatus.Value.ToString(),
                }
            });
            await redisDb.PublishAsync(RedisChannel.Literal("signalr:events"), signalRPayload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish status update SignalR event for message {MessageId}", message.Id);
        }

        logger.LogDebug("Updated delivery status for message {MessageId}: {Status}", message.Id, deliveryStatus);
    }

    // ── Send-message handling (messages sent from phone) ──────────────────

    private async Task HandleSendMessageWebhookAsync(EvolutionMessageWebhookReceivedEvent evt, CancellationToken ct)
    {
        var parsed = ParseIncomingPayload(evt.RawPayload);

        // send-message events are always outbound (fromMe = true), that's the point
        var phone = parsed.Phone;
        if (string.IsNullOrWhiteSpace(phone))
        {
            logger.LogDebug("SendMessage webhook has no phone, ignoring. ExternalId={ExternalId}", evt.ExternalId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();

        // Check if this message was already recorded (e.g., sent from NOC)
        var alreadyExists = await db.Messages.AnyAsync(m => m.ExternalId == evt.ExternalId, ct);
        if (alreadyExists)
        {
            logger.LogDebug("SendMessage with ExternalId={ExternalId} already exists, skipping", evt.ExternalId);
            return;
        }

        // Find contact by phone
        var contact = await db.Contacts.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (contact is null)
        {
            // Create contact for messages sent from phone to unknown numbers
            contact = new Contact
            {
                Id = Guid.CreateVersion7(),
                Phone = phone,
                Name = parsed.ContactName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.Contacts.Add(contact);
        }

        // Find existing conversation for this contact + inbox
        var conversation = await db.Conversations
            .Where(c => c.InboxId == evt.InboxId && c.ContactId == contact.Id
                && c.Status != ConversationStatus.RESOLVED && c.Status != ConversationStatus.ARCHIVED)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            // Try reopening a resolved one
            conversation = await db.Conversations
                .Where(c => c.InboxId == evt.InboxId && c.ContactId == contact.Id
                    && c.Status == ConversationStatus.RESOLVED)
                .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (conversation is not null)
            {
                conversation.Status = ConversationStatus.OPEN;
                conversation.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                conversation = CreateConversation(evt.InboxId, contact.Id, DateTimeOffset.UtcNow);
                db.Conversations.Add(conversation);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var messageType = ParseIncomingMessageType(parsed);
        var messageContent = !string.IsNullOrWhiteSpace(parsed.MediaCaption) ? parsed.MediaCaption : parsed.Content;

        var message = new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversation.Id,
            ExternalId = evt.ExternalId,
            Direction = MessageDirection.OUTBOUND,
            Type = messageType,
            Content = messageContent,
            MediaMimeType = parsed.MediaMimeType,
            MediaFilename = parsed.MediaFileName,
            DeliveryStatus = DeliveryStatus.SENT,
            ProviderMetadata = evt.RawPayload,
            CreatedAt = now,
        };
        db.Messages.Add(message);

        var preview = !string.IsNullOrWhiteSpace(messageContent)
            ? BuildPreview(messageContent)
            : BuildMediaPreview(messageType);

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = preview;
        conversation.LastMessageDirection = MessageDirection.OUTBOUND.ToString();
        conversation.LastOutboundAt = now;
        conversation.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsExternalIdUniqueViolation(ex))
        {
            logger.LogDebug("Duplicate ExternalId={ExternalId} for send-message, ignoring", evt.ExternalId);
            return;
        }

        await PublishSignalREventAsync(evt.InboxId, conversation.Id, message);
        logger.LogInformation("Recorded phone-sent message {MessageId} for conversation {ConversationId}",
            message.Id, conversation.Id);
    }

    private sealed record IncomingPayload(
        string Phone,
        string Content,
        string? ContactName,
        bool HasMedia,
        string? MediaKind,
        string? MediaMimeType,
        string? MediaCaption,
        string? MediaFileName,
        string? RawMessageJson,
        bool FromMe,
        string? RemoteJid,
        bool HasDirectPhoneAlternative);

    private sealed record EvolutionContactCandidate(
        string? RemoteJid,
        string? PushName,
        string? ProfilePicUrl);
}

