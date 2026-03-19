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
    ILogger<EvolutionWebhookController> logger) : ControllerBase
{
    private const string MessagingIncomingStream = "stream:messaging:incoming";
    private const string StatusUpdatesStream = "stream:status:updates";

    [HttpPost("messages")]
    public async Task<IActionResult> ReceiveMessage(Guid inboxId, [FromBody] JsonElement payload)
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

    [HttpPost("status")]
    public async Task<IActionResult> ReceiveStatus(Guid inboxId, [FromBody] JsonElement payload)
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

