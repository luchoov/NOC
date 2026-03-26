// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Events;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Outbox;
using StackExchange.Redis;

namespace NOC.Web.Controllers;

[ApiController]
[Route("webhooks/evolution/{inboxId:guid}")]
[AllowAnonymous]
public class EvolutionWebhookController(
    NocDbContext db,
    OutboxWriter outboxWriter,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<EvolutionWebhookController> logger) : ControllerBase
{
    private const string MessagingIncomingStream = "stream:messaging:incoming";
    private const string StatusUpdatesStream = "stream:status:updates";
    private const string ReceiptsStream = "stream:status:receipts";
    private const string WebhookTokenQueryKey = "token";
    private const string WebhookTokenHeader = "X-Noc-Webhook-Token";

    [HttpPost]
    [HttpPost("{eventSlug}")]
    public async Task<IActionResult> Receive(Guid inboxId, string? eventSlug, [FromBody] JsonElement payload)
    {
        var authError = EnsureWebhookAuthorized(inboxId);
        if (authError is not null)
            return authError;

        var normalizedEvent = NormalizeEventSlug(eventSlug, payload);

        logger.LogInformation("Evolution webhook received: event={Event}, slug={Slug}, inbox={InboxId}",
            normalizedEvent, eventSlug, inboxId);

        return normalizedEvent switch
        {
            "messages-upsert" or "messages-set" => await ReceiveMessageInternal(inboxId, payload),
            "messages-update" => await ReceiveReceiptInternal(inboxId, payload),
            "send-message" => await ReceiveSendMessageInternal(inboxId, payload),
            "presence-update" => await ReceivePresenceInternal(inboxId, payload),
            "connection-update" or "qrcode-updated" => await ReceiveStatusInternal(inboxId, payload),
            _ => Ok(new { status = "ignored", inboxId, eventType = normalizedEvent ?? "unknown" }),
        };
    }

    [HttpPost("messages")]
    public async Task<IActionResult> ReceiveMessage(Guid inboxId, [FromBody] JsonElement payload)
    {
        var authError = EnsureWebhookAuthorized(inboxId);
        if (authError is not null)
            return authError;

        return await ReceiveMessageInternal(inboxId, payload);
    }

    [HttpPost("status")]
    public async Task<IActionResult> ReceiveStatus(Guid inboxId, [FromBody] JsonElement payload)
    {
        var authError = EnsureWebhookAuthorized(inboxId);
        if (authError is not null)
            return authError;

        return await ReceiveStatusInternal(inboxId, payload);
    }

    private async Task<IActionResult> ReceiveMessageInternal(Guid inboxId, JsonElement payload)
    {
        var inbox = await GetValidUnofficialInboxAsync(inboxId);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found or not unofficial WhatsApp." });

        var payloadRaw = payload.GetRawText();
        var externalId = ExtractExternalId(payload) ?? ComputePayloadFingerprint(payloadRaw);
        var dedupKey = $"dedup:evolution:message:{inboxId}:{externalId}";

        if (!await TryAcquireDedupAsync(dedupKey, TimeSpan.FromHours(24)))
            return Ok(new { status = "duplicate_ignored", inboxId, externalId });

        var webhookEvent = new EvolutionMessageWebhookReceivedEvent
        {
            EventType = "EVOLUTION_MESSAGE_WEBHOOK_RECEIVED",
            CorrelationId = HttpContext.TraceIdentifier,
            InboxId = inboxId,
            ExternalId = externalId,
            InstanceName = inbox.EvolutionInstanceName,
            RawPayload = payloadRaw,
        };

        outboxWriter.Enqueue(MessagingIncomingStream, webhookEvent);
        await db.SaveChangesAsync();

        return Ok(new { status = "accepted", inboxId, externalId });
    }

    private async Task<IActionResult> ReceiveStatusInternal(Guid inboxId, JsonElement payload)
    {
        var inbox = await GetValidUnofficialInboxAsync(inboxId);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found or not unofficial WhatsApp." });

        var payloadRaw = payload.GetRawText();
        var normalizedStatus = NormalizeStatus(ExtractStatus(payload));
        var dedupKey = $"dedup:evolution:status:{inboxId}:{ComputePayloadFingerprint(payloadRaw)}";

        if (!await TryAcquireDedupAsync(dedupKey, TimeSpan.FromMinutes(10)))
            return Ok(new { status = "duplicate_ignored", inboxId, normalizedStatus });

        var webhookEvent = new EvolutionStatusWebhookReceivedEvent
        {
            EventType = "EVOLUTION_STATUS_WEBHOOK_RECEIVED",
            CorrelationId = HttpContext.TraceIdentifier,
            InboxId = inboxId,
            InstanceName = inbox.EvolutionInstanceName,
            Status = normalizedStatus,
            RawPayload = payloadRaw,
        };

        outboxWriter.Enqueue(StatusUpdatesStream, webhookEvent);

        inbox.EvolutionSessionStatus = normalizedStatus;
        inbox.EvolutionLastHeartbeat = DateTimeOffset.UtcNow;
        inbox.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new { status = "accepted", inboxId, normalizedStatus });
    }

    /// <summary>
    /// Handles messages-update (delivery receipts: sent, delivered, read).
    /// Evolution API status codes: 2=SENT, 3=DELIVERED, 4=READ, 5=PLAYED
    /// </summary>
    private async Task<IActionResult> ReceiveReceiptInternal(Guid inboxId, JsonElement payload)
    {
        var inbox = await GetValidUnofficialInboxAsync(inboxId);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found or not unofficial WhatsApp." });

        // data can be a single object or array
        var dataElement = payload.TryGetProperty("data", out var data) ? data : payload;
        var items = dataElement.ValueKind == JsonValueKind.Array
            ? dataElement.EnumerateArray().ToList()
            : [dataElement];

        var enqueued = 0;
        foreach (var item in items)
        {
            var externalId = TryGetPath(item, "key", "id");
            var statusCodeStr = TryGetPath(item, "update", "status");
            var remoteJid = TryGetPath(item, "key", "remoteJid");

            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(statusCodeStr))
                continue;

            if (!int.TryParse(statusCodeStr, out var statusCode) || statusCode < 2)
                continue;

            var dedupKey = $"dedup:evolution:receipt:{inboxId}:{externalId}:{statusCode}";
            if (!await TryAcquireDedupAsync(dedupKey, TimeSpan.FromHours(1)))
                continue;

            outboxWriter.Enqueue(ReceiptsStream, new EvolutionReceiptWebhookReceivedEvent
            {
                EventType = "EVOLUTION_RECEIPT_WEBHOOK_RECEIVED",
                CorrelationId = HttpContext.TraceIdentifier,
                InboxId = inboxId,
                ExternalId = externalId,
                StatusCode = statusCode,
                RemoteJid = remoteJid,
                InstanceName = inbox.EvolutionInstanceName,
            });
            enqueued++;
        }

        if (enqueued > 0)
            await db.SaveChangesAsync();

        return Ok(new { status = "accepted", inboxId, receiptsEnqueued = enqueued });
    }

    /// <summary>
    /// Handles send-message (messages sent from the phone, not from NOC).
    /// Enqueues as a regular message event with fromPhone flag for the worker to handle.
    /// </summary>
    private async Task<IActionResult> ReceiveSendMessageInternal(Guid inboxId, JsonElement payload)
    {
        var inbox = await GetValidUnofficialInboxAsync(inboxId);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found or not unofficial WhatsApp." });

        var payloadRaw = payload.GetRawText();
        var externalId = ExtractExternalId(payload) ?? ComputePayloadFingerprint(payloadRaw);
        var dedupKey = $"dedup:evolution:send:{inboxId}:{externalId}";

        if (!await TryAcquireDedupAsync(dedupKey, TimeSpan.FromHours(24)))
            return Ok(new { status = "duplicate_ignored", inboxId, externalId });

        var webhookEvent = new EvolutionMessageWebhookReceivedEvent
        {
            EventType = "EVOLUTION_SEND_MESSAGE_WEBHOOK_RECEIVED",
            CorrelationId = HttpContext.TraceIdentifier,
            InboxId = inboxId,
            ExternalId = externalId,
            InstanceName = inbox.EvolutionInstanceName,
            RawPayload = payloadRaw,
        };

        outboxWriter.Enqueue(MessagingIncomingStream, webhookEvent);
        await db.SaveChangesAsync();

        return Ok(new { status = "accepted", inboxId, externalId });
    }

    /// <summary>
    /// Handles presence-update (typing indicators).
    /// Published directly to SignalR without persistence.
    /// </summary>
    private async Task<IActionResult> ReceivePresenceInternal(Guid inboxId, JsonElement payload)
    {
        var remoteJid = TryGetPath(payload, "data", "id")
            ?? TryGetPath(payload, "id");
        var presenceStatus = TryGetPath(payload, "data", "presences", remoteJid ?? "", "lastKnownPresence")
            ?? TryGetPath(payload, "data", "status")
            ?? TryGetPath(payload, "presences", remoteJid ?? "", "lastKnownPresence");

        if (string.IsNullOrWhiteSpace(remoteJid))
            return Ok(new { status = "ignored", reason = "no_remote_jid" });

        // Extract phone from JID (e.g. "5491134567890@s.whatsapp.net" → "5491134567890")
        var phone = remoteJid.Split('@')[0];

        try
        {
            var redisDb = redis.GetDatabase();
            var eventPayload = JsonSerializer.Serialize(new
            {
                Event = "PresenceUpdate",
                InboxId = inboxId.ToString(),
                Payload = new
                {
                    phone,
                    presence = presenceStatus ?? "unavailable",
                }
            });
            await redisDb.PublishAsync(RedisChannel.Literal("signalr:events"), eventPayload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish presence update via Redis");
        }

        return Ok(new { status = "accepted", inboxId, phone, presence = presenceStatus });
    }

    private static string? NormalizeEventSlug(string? routeEventSlug, JsonElement payload)
    {
        if (!string.IsNullOrWhiteSpace(routeEventSlug))
            return NormalizeSlug(routeEventSlug);

        var payloadEvent =
            TryGetPath(payload, "event")
            ?? TryGetPath(payload, "eventType");

        return NormalizeSlug(payloadEvent);
    }

    private static string? NormalizeSlug(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return raw
            .Trim()
            .Replace('_', '-')
            .Replace('.', '-')
            .ToLowerInvariant();
    }

    private async Task<Inbox?> GetValidUnofficialInboxAsync(Guid inboxId)
    {
        return await db.Inboxes.FirstOrDefaultAsync(i =>
            i.Id == inboxId &&
            i.ChannelType == ChannelType.WHATSAPP_UNOFFICIAL);
    }

    private async Task<bool> TryAcquireDedupAsync(string key, TimeSpan ttl)
    {
        var redisDb = redis.GetDatabase();

        var acquired = await redisDb.StringSetAsync(
            key,
            "1",
            ttl,
            when: When.NotExists);

        if (!acquired)
            logger.LogDebug("Duplicate Evolution webhook ignored for key {DedupKey}", key);

        return acquired;
    }

    private static string ComputePayloadFingerprint(string payload)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private IActionResult? EnsureWebhookAuthorized(Guid inboxId)
    {
        var expectedToken = configuration["NOC_EVOLUTION_WEBHOOK_SECRET"]
            ?? configuration["Noc:EvolutionWebhookSecret"];

        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            logger.LogError(
                "Rejected Evolution webhook for inbox {InboxId} because NOC_EVOLUTION_WEBHOOK_SECRET is not configured.",
                inboxId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Evolution webhook secret is not configured.",
            });
        }

        var providedToken = Request.Query[WebhookTokenQueryKey].FirstOrDefault()
            ?? Request.Headers[WebhookTokenHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(providedToken) || !FixedTimeEquals(providedToken, expectedToken))
        {
            logger.LogWarning("Rejected Evolution webhook for inbox {InboxId} due to invalid token.", inboxId);
            return Unauthorized(new { message = "Invalid webhook token." });
        }

        return null;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string? ExtractExternalId(JsonElement payload)
    {
        return TryGetPath(payload, "data", "key", "id")
            ?? TryGetPath(payload, "data", "id")
            ?? TryGetPath(payload, "id")
            ?? TryGetPath(payload, "key", "id")
            ?? TryGetPath(payload, "messageId")
            ?? TryGetPath(payload, "externalId");
    }

    private static string? ExtractStatus(JsonElement payload)
    {
        return TryGetPath(payload, "status")
            ?? TryGetPath(payload, "state")
            ?? TryGetPath(payload, "instance", "status")
            ?? TryGetPath(payload, "instance", "state");
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return "UNKNOWN";

        return rawStatus.Trim().ToUpperInvariant() switch
        {
            "OPEN" or "CONNECTED" => "CONNECTED",
            "CLOSE" or "CLOSED" or "DISCONNECTED" => "DISCONNECTED",
            "CONNECTING" or "QR" or "QRCODE" or "QR_PENDING" => "QR_PENDING",
            var status => status,
        };
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

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }
}

