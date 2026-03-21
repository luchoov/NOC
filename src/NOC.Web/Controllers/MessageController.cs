// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Web.Hubs;
using NOC.Shared.Infrastructure.Storage;
using NOC.Web.Messages;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/conversations/{conversationId:guid}/messages")]
[Authorize]
public class MessageController(
    NocDbContext db,
    IEvolutionApiClient evolutionApiClient,
    IMediaStorageService mediaStorage,
    AuditService auditService,
    IHubContext<NocHub> hubContext,
    IServiceProvider serviceProvider,
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
    public async Task<IActionResult> Send(Guid conversationId, [FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "Content is required." });

        var conversation = await db.Conversations
            .Include(c => c.Inbox)
                .ThenInclude(i => i.ProxyOutbound)
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

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
                var proxyOptions = BuildEvolutionProxyOptions(conversation.Inbox.ProxyOutbound);
                var recipientResolution = await ResolveOutboundRecipientAsync(conversation, proxyOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(recipientResolution.FailureMessage))
                {
                    return Conflict(new
                    {
                        message = recipientResolution.FailureMessage,
                        remoteJid = recipientResolution.RemoteJid,
                    });
                }

                if (recipientResolution.PersistContactPhone &&
                    !string.Equals(conversation.Contact.Phone, recipientResolution.Number, StringComparison.Ordinal) &&
                    !await db.Contacts.AsNoTracking().AnyAsync(
                        contact => contact.Id != conversation.Contact.Id && contact.Phone == recipientResolution.Number,
                        cancellationToken))
                {
                    conversation.Contact.Phone = recipientResolution.Number;
                    conversation.Contact.UpdatedAt = now;
                }

                var evolutionResponse = await evolutionApiClient.SendMessageAsync(
                    conversation.Inbox.EvolutionInstanceName,
                    new EvolutionSendMessageRequest
                    {
                        Number = recipientResolution.Number,
                        Text = request.Content.Trim(),
                    },
                    proxyOptions,
                    cancellationToken);

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
                    statusCode = ex.StatusCode,
                    detail = ex.Message,
                    providerResponse = ex.ResponseBody,
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

        // Push outbound message to SignalR (conversation + inbox groups)
        var msgResponse = MapToResponse(message);
        await hubContext.Clients.Group($"conversation:{conversationId}")
            .SendAsync("MessageReceived", conversationId.ToString(), msgResponse, cancellationToken);
        await hubContext.Clients.Group($"inbox:{conversation.InboxId}")
            .SendAsync("MessageReceived", conversationId.ToString(), msgResponse, cancellationToken);

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

        return Ok(msgResponse);
    }

    [HttpGet("{messageId:guid}/media")]
    public async Task<IActionResult> GetMedia(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        var message = await db.Messages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId, cancellationToken);

        if (message is null)
            return NotFound(new { message = "Message not found" });

        var conversation = await db.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(message.MediaUrl))
            return NotFound(new { message = "No media attached" });

        try
        {
            var stream = await mediaStorage.DownloadAsync(message.MediaUrl, cancellationToken);
            var contentType = message.MediaMimeType ?? "application/octet-stream";
            var fileName = message.MediaFilename ?? "media";

            // Inline for viewable types (images, audio, video), attachment for documents
            var isInline = contentType.StartsWith("image/") || contentType.StartsWith("audio/") || contentType.StartsWith("video/");

            Response.Headers["Cache-Control"] = "private, max-age=3600";

            return File(stream, contentType, fileName, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download media for message {MessageId}", messageId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Failed to retrieve media" });
        }
    }

    [HttpPost("media")]
    [RequestSizeLimit(20 * 1024 * 1024)] // 20 MB
    public async Task<IActionResult> SendMedia(
        Guid conversationId,
        IFormFile file,
        [FromForm] string? caption = null,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "File is empty." });

        var conversation = await db.Conversations
            .Include(c => c.Inbox)
                .ThenInclude(i => i.ProxyOutbound)
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(conversation.Inbox.EvolutionInstanceName))
            return Conflict(new { message = "Inbox has no Evolution instance configured." });

        var senderAgentId = GetCurrentAgentId();
        var now = DateTimeOffset.UtcNow;
        var messageId = Guid.CreateVersion7();
        var mimeType = file.ContentType ?? "application/octet-stream";
        var messageType = InferMessageType(mimeType);
        var fileName = file.FileName ?? "media";
        var objectKey = $"{conversation.InboxId}/{now:yyyy/MM}/{messageId}/{fileName}";

        // Upload to MinIO
        await using var stream = file.OpenReadStream();
        await mediaStorage.UploadAsync(stream, objectKey, mimeType, file.Length, cancellationToken);

        // Generate presigned URL for Evolution API
        var presignedUrl = await mediaStorage.GeneratePresignedUrlAsync(objectKey, TimeSpan.FromMinutes(15), cancellationToken);

        // Send via Evolution API
        try
        {
            var proxyOptions = BuildEvolutionProxyOptions(conversation.Inbox.ProxyOutbound);
            var recipientResolution = await ResolveOutboundRecipientAsync(conversation, proxyOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(recipientResolution.FailureMessage))
                return Conflict(new { message = recipientResolution.FailureMessage });

            var evolutionMediaType = messageType switch
            {
                MessageType.IMAGE => "image",
                MessageType.VIDEO => "video",
                MessageType.AUDIO => "audio",
                _ => "document",
            };

            var evolutionResponse = await evolutionApiClient.SendMediaMessageAsync(
                conversation.Inbox.EvolutionInstanceName,
                new EvolutionSendMediaRequest
                {
                    Number = recipientResolution.Number,
                    MediaType = evolutionMediaType,
                    Media = presignedUrl,
                    Caption = caption,
                    FileName = fileName,
                },
                proxyOptions,
                cancellationToken);

            var message = new Message
            {
                Id = messageId,
                ConversationId = conversationId,
                Direction = MessageDirection.OUTBOUND,
                Type = messageType,
                Content = caption,
                MediaUrl = objectKey,
                MediaMimeType = mimeType,
                MediaFilename = fileName,
                MediaSizeBytes = file.Length,
                SentByAgentId = senderAgentId,
                DeliveryStatus = DeliveryStatus.SENT,
                DeliveryUpdatedAt = DateTimeOffset.UtcNow,
                ExternalId = ExtractExternalId(evolutionResponse.Payload),
                ProviderMetadata = evolutionResponse.Payload.ToJsonString(),
                IsPrivateNote = false,
                CreatedAt = now,
            };

            db.Messages.Add(message);

            conversation.LastMessageAt = now;
            conversation.LastMessagePreview = caption ?? $"[{evolutionMediaType}]";
            conversation.LastMessageDirection = MessageDirection.OUTBOUND.ToString();
            conversation.LastOutboundAt = now;
            conversation.UpdatedAt = now;

            await db.SaveChangesAsync(cancellationToken);

            var msgResponse = MapToResponse(message);
            await hubContext.Clients.Group($"conversation:{conversationId}")
                .SendAsync("MessageReceived", conversationId.ToString(), msgResponse, cancellationToken);
            await hubContext.Clients.Group($"inbox:{conversation.InboxId}")
                .SendAsync("MessageReceived", conversationId.ToString(), msgResponse, cancellationToken);

            return Ok(msgResponse);
        }
        catch (EvolutionApiException ex)
        {
            logger.LogWarning(ex, "Evolution send media failed for conversation {ConversationId}", conversationId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Failed to send media through Evolution API.",
                detail = ex.Message,
            });
        }
    }

    private static MessageType InferMessageType(string mimeType)
    {
        if (mimeType.StartsWith("image/webp")) return MessageType.STICKER;
        if (mimeType.StartsWith("image/")) return MessageType.IMAGE;
        if (mimeType.StartsWith("video/")) return MessageType.VIDEO;
        if (mimeType.StartsWith("audio/")) return MessageType.AUDIO;
        return MessageType.DOCUMENT;
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

    private EvolutionProxyOptions? BuildEvolutionProxyOptions(ProxyOutbound? proxy)
    {
        if (proxy is null)
            return null;

        string? password = null;
        if (!string.IsNullOrWhiteSpace(proxy.EncryptedPassword))
        {
            var encryptor = serviceProvider.GetService<AesGcmEncryptor>();
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

    private async Task<EvolutionRecipientResolution> ResolveOutboundRecipientAsync(
        Conversation conversation,
        EvolutionProxyOptions? proxyOptions,
        CancellationToken cancellationToken)
    {
        var fallbackNumber = NormalizePhone(conversation.Contact.Phone);
        if (string.IsNullOrWhiteSpace(fallbackNumber))
        {
            return new EvolutionRecipientResolution(
                string.Empty,
                false,
                null,
                "El contacto no tiene un numero valido para enviar mensajes.");
        }

        var latestInboundPayload = await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.Id && m.Direction == MessageDirection.INBOUND)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.ProviderMetadata)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(latestInboundPayload))
            return new EvolutionRecipientResolution(fallbackNumber, false, null, null);

        var metadata = ParseJsonObject(latestInboundPayload);
        if (metadata is null)
            return new EvolutionRecipientResolution(fallbackNumber, false, null, null);

        var directNumber = ExtractDirectWhatsappNumber(metadata);
        if (!string.IsNullOrWhiteSpace(directNumber))
        {
            return new EvolutionRecipientResolution(
                directNumber,
                !string.Equals(directNumber, fallbackNumber, StringComparison.Ordinal),
                ExtractInboundRemoteJid(metadata),
                null);
        }

        var remoteJid = ExtractInboundRemoteJid(metadata);
        var normalizedRemoteJid = NormalizePhone(remoteJid);
        if (!LooksLikeLidJid(remoteJid) || !string.Equals(fallbackNumber, normalizedRemoteJid, StringComparison.Ordinal))
            return new EvolutionRecipientResolution(fallbackNumber, false, remoteJid, null);

        var resolvedNumber = await TryResolveLidNumberFromContactsAsync(
            conversation.Inbox.EvolutionInstanceName!,
            remoteJid!,
            metadata,
            proxyOptions,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(resolvedNumber))
        {
            logger.LogInformation(
                "Resolved WhatsApp LID recipient for conversation {ConversationId}. RemoteJid={RemoteJid}, Number={Number}",
                conversation.Id,
                remoteJid,
                resolvedNumber);

            return new EvolutionRecipientResolution(
                resolvedNumber,
                !string.Equals(resolvedNumber, fallbackNumber, StringComparison.Ordinal),
                remoteJid,
                null);
        }

        return new EvolutionRecipientResolution(
            fallbackNumber,
            false,
            remoteJid,
            "No pudimos resolver el numero real de WhatsApp para este contacto. El ultimo inbound llego con un identificador @lid y Evolution no expuso el numero final.");
    }

    private async Task<string?> TryResolveLidNumberFromContactsAsync(
        string instanceName,
        string remoteJid,
        JsonObject inboundMetadata,
        EvolutionProxyOptions? proxyOptions,
        CancellationToken cancellationToken)
    {
        var payload = await evolutionApiClient.FindContactsAsync(
            instanceName,
            request: null,
            proxy: proxyOptions,
            cancellationToken: cancellationToken);

        var contacts = ExtractEvolutionContacts(payload.Payload);
        if (contacts.Count == 0)
            return null;

        var inboundName =
            TryGet(inboundMetadata, "data", "pushName")
            ?? TryGet(inboundMetadata, "pushName")
            ?? string.Empty;

        var lidContact = contacts.FirstOrDefault(contact =>
            string.Equals(contact.RemoteJid, remoteJid, StringComparison.OrdinalIgnoreCase));

        var candidateContacts = contacts
            .Where(contact => IsResolvableWhatsappJid(contact.RemoteJid))
            .ToList();

        string? resolved = null;
        if (!string.IsNullOrWhiteSpace(lidContact?.ProfilePicUrl))
        {
            resolved = SelectUniqueNumber(candidateContacts.Where(contact =>
                string.Equals(contact.ProfilePicUrl, lidContact.ProfilePicUrl, StringComparison.Ordinal) &&
                string.Equals(contact.PushName, lidContact.PushName ?? inboundName, StringComparison.Ordinal)));

            resolved ??= SelectUniqueNumber(candidateContacts.Where(contact =>
                string.Equals(contact.ProfilePicUrl, lidContact.ProfilePicUrl, StringComparison.Ordinal)));
        }

        if (resolved is null && !string.IsNullOrWhiteSpace(lidContact?.PushName ?? inboundName))
        {
            var targetName = lidContact?.PushName ?? inboundName;
            resolved = SelectUniqueNumber(candidateContacts.Where(contact =>
                string.Equals(contact.PushName, targetName, StringComparison.Ordinal)));
        }

        return resolved;
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
                TryGet(jsonObject, "remoteJid"),
                TryGet(jsonObject, "pushName"),
                TryGet(jsonObject, "profilePicUrl")));
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

    private static string? ExtractDirectWhatsappNumber(JsonObject metadata)
    {
        return NormalizePhone(
            TryGet(metadata, "data", "key", "remoteJidAlt")
            ?? TryGet(metadata, "key", "remoteJidAlt")
            ?? TryGet(metadata, "data", "remoteJidAlt")
            ?? TryGet(metadata, "remoteJidAlt")
            ?? TryGet(metadata, "data", "senderPn")
            ?? TryGet(metadata, "senderPn")
            ?? TryGet(metadata, "data", "participantPn")
            ?? TryGet(metadata, "participantPn"));
    }

    private static string? ExtractInboundRemoteJid(JsonObject metadata)
    {
        return TryGet(metadata, "data", "key", "remoteJid")
            ?? TryGet(metadata, "key", "remoteJid")
            ?? TryGet(metadata, "data", "remoteJid")
            ?? TryGet(metadata, "remoteJid");
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

    private static string NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = value.Split('@')[0];
        return new string(cleaned.Where(char.IsDigit).ToArray());
    }

    private static JsonObject? ParseJsonObject(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            return JsonNode.Parse(rawJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
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
            message.MediaMimeType,
            message.MediaFilename,
            message.MediaSizeBytes,
            message.DeliveryStatus,
            message.DeliveryUpdatedAt,
            message.SentByAgentId,
            message.SentByAi,
            message.IsPrivateNote,
            ParseJsonOrDefault(message.ProviderMetadata),
            message.CreatedAt);
    }

    private sealed record EvolutionRecipientResolution(
        string Number,
        bool PersistContactPhone,
        string? RemoteJid,
        string? FailureMessage);

    private sealed record EvolutionContactCandidate(
        string? RemoteJid,
        string? PushName,
        string? ProfilePicUrl);
}

