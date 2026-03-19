// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Web.Messages;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:guid}/messages")]
[Authorize]
public class MessageController(
    NocDbContext db,
    IEvolutionApiClient evolutionApiClient,
    AuditService auditService,
    ILogger<MessageController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        Guid conversationId,
        [FromQuery] DateTimeOffset? beforeCreatedAt = null,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int limit = 50,
        [FromQuery] bool includePrivateNotes = true)
    {
        limit = Math.Clamp(limit, 1, 100);

        var conversation = await db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        IQueryable<Message> query;
        if (beforeCreatedAt.HasValue && beforeId.HasValue)
        {
            query = db.Messages
                .FromSqlInterpolated($@"
SELECT *
FROM messages
WHERE conversation_id = {conversationId}
  AND (created_at, id) < ({beforeCreatedAt.Value}, {beforeId.Value})
ORDER BY created_at DESC, id DESC
LIMIT {limit}")
                .AsNoTracking();
        }
        else
        {
            query = db.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(limit);
        }

        if (!includePrivateNotes)
            query = query.Where(m => !m.IsPrivateNote);

        var messages = await query.ToListAsync();
        return Ok(messages.Select(MapToResponse));
    }

    [HttpPost]
    public async Task<IActionResult> Send(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Content is required." });

        var conversation = await db.Conversations
            .Include(c => c.Inbox)
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        var senderAgentId = GetCurrentAgentId();
        var now = DateTimeOffset.UtcNow;

        var messageType = request.IsPrivateNote ? MessageType.INTERNAL_NOTE : request.Type;
        var message = new Message
        {
            Id = Guid.CreateVersion7(),
            ConversationId = conversationId,
            Direction = MessageDirection.OUTBOUND,
            Type = messageType,
            Content = request.Content.Trim(),
            IsPrivateNote = request.IsPrivateNote,
            SentByAgentId = senderAgentId,
            CreatedAt = now,
            ProviderMetadata = "{}",
        };

        if (!request.IsPrivateNote)
        {
            if (conversation.Inbox.ChannelType == ChannelType.WHATSAPP_OFFICIAL)
            {
                return StatusCode(StatusCodes.Status501NotImplemented, new
                {
                    message = "Official channel outbound is not implemented yet."
                });
            }

            if (string.IsNullOrWhiteSpace(conversation.Inbox.EvolutionInstanceName))
                return Conflict(new { message = "Inbox has no Evolution instance configured." });

            try
            {
                var evolutionResponse = await evolutionApiClient.SendMessageAsync(
                    conversation.Inbox.EvolutionInstanceName,
                    new EvolutionSendMessageRequest
                    {
                        Number = conversation.Contact.Phone,
                        Text = request.Content.Trim(),
                    });

                message.ExternalId = ExtractExternalId(evolutionResponse.Payload);
                message.DeliveryStatus = DeliveryStatus.SENT;
                message.DeliveryUpdatedAt = DateTimeOffset.UtcNow;
                message.ProviderMetadata = evolutionResponse.Payload.ToJsonString();
            }
            catch (EvolutionApiException ex)
            {
                logger.LogWarning(ex, "Evolution send failed for conversation {ConversationId}", conversationId);
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "Failed to send message through Evolution API.",
                    ex.StatusCode,
                    ex.Message,
                });
            }
        }

        db.Messages.Add(message);

        conversation.LastMessageAt = now;
        conversation.LastMessagePreview = BuildPreview(message.Content);
        conversation.LastMessageDirection = MessageDirection.OUTBOUND.ToString();
        conversation.LastOutboundAt = now;
        conversation.UpdatedAt = now;

        await db.SaveChangesAsync();

        await auditService.LogAsync(
            request.IsPrivateNote ? "CONVERSATION_INTERNAL_NOTE_CREATED" : "MESSAGE_SENT",
            entityType: "MESSAGE",
            entityId: message.Id,
            payload: new
            {
                message.ConversationId,
                message.Type,
                message.ExternalId,
                message.IsPrivateNote,
            });

        return Ok(MapToResponse(message));
    }

    private async Task<bool> HasInboxAccessAsync(Guid inboxId)
    {
        if (User.IsInRole(nameof(AgentRole.ADMIN)) || User.IsInRole(nameof(AgentRole.SUPERVISOR)))
            return true;

        var requesterAgentId = GetCurrentAgentId();
        if (!requesterAgentId.HasValue)
            return false;

        return await db.InboxAgents.AnyAsync(ia => ia.InboxId == inboxId && ia.AgentId == requesterAgentId.Value);
    }

    private Guid? GetCurrentAgentId()
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var agentId) ? agentId : null;
    }

    private static string? ExtractExternalId(JsonObject payload)
    {
        return TryGet(payload, "key", "id")
            ?? TryGet(payload, "data", "key", "id")
            ?? TryGet(payload, "id")
            ?? TryGet(payload, "messageId");
    }

    private static string? TryGet(JsonObject root, params string[] path)
    {
        JsonNode? current = root;
        foreach (var segment in path)
        {
            if (current is not JsonObject obj || obj[segment] is null)
                return null;
            current = obj[segment];
        }

        return current switch
        {
            JsonValue value when value.TryGetValue<string>(out var result) => result,
            JsonValue value => value.ToJsonString().Trim('"'),
            _ => null
        };
    }

    private static string BuildPreview(string? content, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;
        if (content.Length <= maxLength)
            return content;
        return content[..maxLength];
    }

    private static JsonElement ParseJsonOrDefault(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ParseJsonOrDefault("{}");

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var fallback = JsonDocument.Parse("{}");
            return fallback.RootElement.Clone();
        }
    }

    private static MessageResponse MapToResponse(Message message)
    {
        return new MessageResponse(
            message.Id,
            message.ConversationId,
            message.ExternalId,
            message.Direction,
            message.Type,
            message.Content,
            message.MediaUrl,
            message.DeliveryStatus,
            message.DeliveryUpdatedAt,
            message.SentByAgentId,
            message.SentByAi,
            message.IsPrivateNote,
            ParseJsonOrDefault(message.ProviderMetadata),
            message.CreatedAt);
    }
}

