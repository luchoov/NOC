// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Web.Inboxes;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/inboxes")]
[Authorize(Roles = "ADMIN,SUPERVISOR")]
public class InboxController(
    NocDbContext db,
    IEvolutionApiClient evolutionApiClient,
    AuditService auditService,
    IServiceProvider serviceProvider,
    ILogger<InboxController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ChannelType? channelType = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);

        var query = db.Inboxes.AsNoTracking().AsQueryable();

        if (channelType.HasValue)
            query = query.Where(i => i.ChannelType == channelType.Value);

        if (isActive.HasValue)
            query = query.Where(i => i.IsActive == isActive.Value);

        var inboxes = await query
            .OrderBy(i => i.Name)
            .ThenBy(i => i.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(inboxes.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var inbox = await db.Inboxes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });

        return Ok(MapToResponse(inbox));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInboxRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Name is required" });
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { message = "PhoneNumber is required" });

        var now = DateTimeOffset.UtcNow;
        var inboxId = Guid.CreateVersion7();
        var instanceName = request.ChannelType == ChannelType.WHATSAPP_UNOFFICIAL
            ? request.EvolutionInstanceName ?? BuildEvolutionInstanceName(inboxId, request.PhoneNumber)
            : null;

        var inbox = new Inbox
        {
            Id = inboxId,
            Name = request.Name.Trim(),
            ChannelType = request.ChannelType,
            PhoneNumber = request.PhoneNumber.Trim(),
            Config = NormalizeConfig(request.Config),
            EvolutionInstanceName = instanceName,
            EvolutionSessionStatus = request.ChannelType == ChannelType.WHATSAPP_UNOFFICIAL ? "DISCONNECTED" : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var credentialUpdateError = ApplySecretUpdates(
            inbox,
            request.AccessToken,
            request.RefreshToken,
            out var credentialsUpdated);
        if (credentialUpdateError is not null)
            return credentialUpdateError;

        db.Inboxes.Add(inbox);
        await db.SaveChangesAsync();

        JsonObject? createPayload = null;
        JsonObject? connectPayload = null;
        string? evolutionError = null;
        var evolutionProvisioned = request.ChannelType != ChannelType.WHATSAPP_UNOFFICIAL;

        if (request.ChannelType == ChannelType.WHATSAPP_UNOFFICIAL && request.AutoProvisionEvolution)
        {
            var result = await TryProvisionEvolutionAsync(inbox, request.AutoConnectEvolution);
            createPayload = result.CreatePayload;
            connectPayload = result.ConnectPayload;
            evolutionError = result.Error;
            evolutionProvisioned = result.Success;
        }

        await auditService.LogAsync(
            "INBOX_CREATED",
            entityType: "INBOX",
            entityId: inbox.Id,
            payload: new { inbox.Name, ChannelType = inbox.ChannelType.ToString(), inbox.PhoneNumber });

        if (credentialsUpdated)
        {
            await auditService.LogAsync(
                "INBOX_CREDENTIALS_UPDATED",
                entityType: "INBOX",
                entityId: inbox.Id,
                payload: new { accessTokenUpdated = request.AccessToken is not null, refreshTokenUpdated = request.RefreshToken is not null });
        }

        var response = new CreateInboxResponse(
            MapToResponse(inbox),
            evolutionProvisioned,
            createPayload,
            connectPayload,
            evolutionError);

        return CreatedAtAction(nameof(GetById), new { id = inbox.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInboxRequest request)
    {
        var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });

        var originalBanStatus = inbox.BanStatus;

        if (!string.IsNullOrWhiteSpace(request.Name))
            inbox.Name = request.Name.Trim();
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            inbox.PhoneNumber = request.PhoneNumber.Trim();
        if (request.Config.HasValue)
            inbox.Config = NormalizeConfig(request.Config);
        if (request.IsActive.HasValue)
            inbox.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.EvolutionInstanceName))
            inbox.EvolutionInstanceName = request.EvolutionInstanceName.Trim();

        if (request.BanStatus.HasValue)
        {
            inbox.BanStatus = request.BanStatus.Value;
            if (request.BanStatus.Value == BanStatus.BANNED)
            {
                inbox.BannedAt ??= DateTimeOffset.UtcNow;
                inbox.BanReason = request.BanReason;
            }
            else
            {
                inbox.BannedAt = null;
                inbox.BanReason = request.BanReason;
            }
        }

        var credentialUpdateError = ApplySecretUpdates(
            inbox,
            request.AccessToken,
            request.RefreshToken,
            out var credentialsUpdated);
        if (credentialUpdateError is not null)
            return credentialUpdateError;

        inbox.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "INBOX_UPDATED",
            entityType: "INBOX",
            entityId: inbox.Id,
            payload: new
            {
                inbox.Name,
                inbox.PhoneNumber,
                inbox.IsActive,
                BanStatus = inbox.BanStatus.ToString(),
            });

        if (credentialsUpdated)
        {
            await auditService.LogAsync(
                "INBOX_CREDENTIALS_UPDATED",
                entityType: "INBOX",
                entityId: inbox.Id,
                payload: new { accessTokenUpdated = request.AccessToken is not null, refreshTokenUpdated = request.RefreshToken is not null });
        }

        if (originalBanStatus != BanStatus.BANNED && inbox.BanStatus == BanStatus.BANNED)
        {
            await auditService.LogAsync(
                "INBOX_BANNED",
                entityType: "INBOX",
                entityId: inbox.Id,
                payload: new { inbox.BanReason });
        }

        return Ok(MapToResponse(inbox));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });

        var hasConversations = await db.Conversations.AnyAsync(c => c.InboxId == id);
        var hasCampaigns = await db.Campaigns.AnyAsync(c => c.InboxId == id);

        if (hasConversations || hasCampaigns)
        {
            return Conflict(new
            {
                message = "Inbox has related operational data and cannot be deleted.",
                recommendation = "Use PUT /api/inboxes/{id} with isActive=false to deactivate."
            });
        }

        db.Inboxes.Remove(inbox);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "INBOX_DELETED",
            entityType: "INBOX",
            entityId: id,
            payload: new { inbox.Name, inbox.ChannelType });

        return NoContent();
    }

    [HttpPost("{id:guid}/provision-evolution")]
    public async Task<IActionResult> ProvisionEvolution(Guid id, [FromBody] ProvisionEvolutionRequest request)
    {
        var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });
        if (inbox.ChannelType != ChannelType.WHATSAPP_UNOFFICIAL)
            return BadRequest(new { message = "Evolution provisioning is only available for unofficial WhatsApp inboxes." });

        inbox.EvolutionInstanceName ??= BuildEvolutionInstanceName(inbox.Id, inbox.PhoneNumber);
        inbox.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var result = await TryProvisionEvolutionAsync(inbox, request.AutoConnect);
        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Evolution provisioning failed.",
                result.Error,
                inboxId = inbox.Id,
                inbox.EvolutionInstanceName,
            });
        }

        await auditService.LogAsync(
            "INBOX_EVOLUTION_PROVISIONED",
            entityType: "INBOX",
            entityId: inbox.Id,
            payload: new { inbox.EvolutionInstanceName, autoConnect = request.AutoConnect });

        return Ok(new EvolutionOperationResponse(
            MapToResponse(inbox),
            "provision-evolution",
            result.ConnectPayload ?? result.CreatePayload ?? new JsonObject()));
    }

    [HttpPost("{id:guid}/connect")]
    public async Task<IActionResult> Connect(Guid id)
    {
        var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });
        if (inbox.ChannelType != ChannelType.WHATSAPP_UNOFFICIAL)
            return BadRequest(new { message = "Connect is only available for unofficial WhatsApp inboxes." });

        inbox.EvolutionInstanceName ??= BuildEvolutionInstanceName(inbox.Id, inbox.PhoneNumber);

        try
        {
            var connectResponse = await evolutionApiClient.ConnectInstanceAsync(inbox.EvolutionInstanceName);
            inbox.EvolutionSessionStatus = "QR_PENDING";
            inbox.EvolutionLastHeartbeat = DateTimeOffset.UtcNow;
            inbox.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            await auditService.LogAsync(
                "INBOX_EVOLUTION_CONNECT_REQUESTED",
                entityType: "INBOX",
                entityId: inbox.Id,
                payload: new { inbox.EvolutionInstanceName });

            return Ok(new EvolutionOperationResponse(MapToResponse(inbox), "connect", connectResponse.Payload));
        }
        catch (EvolutionApiException ex)
        {
            logger.LogWarning(ex, "Evolution connect failed for inbox {InboxId}", inbox.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Evolution connect request failed.",
                ex.StatusCode,
                ex.Message,
            });
        }
    }

    [HttpGet("{id:guid}/status")]
    public async Task<IActionResult> GetEvolutionStatus(Guid id, [FromQuery] bool refresh = true)
    {
        var inbox = await db.Inboxes.FirstOrDefaultAsync(i => i.Id == id);
        if (inbox is null)
            return NotFound(new { message = "Inbox not found" });
        if (inbox.ChannelType != ChannelType.WHATSAPP_UNOFFICIAL)
            return BadRequest(new { message = "Status is only available for unofficial WhatsApp inboxes." });
        if (string.IsNullOrWhiteSpace(inbox.EvolutionInstanceName))
            return Conflict(new { message = "Inbox has no Evolution instance configured." });

        JsonObject payload;
        if (!refresh)
        {
            payload = new JsonObject
            {
                ["instanceName"] = inbox.EvolutionInstanceName,
                ["status"] = inbox.EvolutionSessionStatus ?? "UNKNOWN",
            };

            return Ok(new EvolutionOperationResponse(MapToResponse(inbox), "status", payload));
        }

        try
        {
            var statusResponse = await evolutionApiClient.GetInstanceStatusAsync(inbox.EvolutionInstanceName);
            inbox.EvolutionSessionStatus = statusResponse.Status;
            inbox.EvolutionLastHeartbeat = DateTimeOffset.UtcNow;
            inbox.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return Ok(new EvolutionOperationResponse(MapToResponse(inbox), "status", statusResponse.Payload));
        }
        catch (EvolutionApiException ex)
        {
            logger.LogWarning(ex, "Evolution status lookup failed for inbox {InboxId}", inbox.Id);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = "Evolution status lookup failed.",
                ex.StatusCode,
                ex.Message,
            });
        }
    }

    private async Task<(bool Success, JsonObject? CreatePayload, JsonObject? ConnectPayload, string? Error)> TryProvisionEvolutionAsync(
        Inbox inbox,
        bool autoConnect)
    {
        if (string.IsNullOrWhiteSpace(inbox.EvolutionInstanceName))
            return (false, null, null, "Inbox has no Evolution instance name.");

        try
        {
            var createResponse = await evolutionApiClient.CreateInstanceAsync(new EvolutionCreateInstanceRequest
            {
                InstanceName = inbox.EvolutionInstanceName,
            });

            JsonObject? connectPayload = null;
            if (autoConnect)
            {
                var connectResponse = await evolutionApiClient.ConnectInstanceAsync(inbox.EvolutionInstanceName);
                connectPayload = connectResponse.Payload;
                inbox.EvolutionSessionStatus = "QR_PENDING";
            }
            else
            {
                inbox.EvolutionSessionStatus = "DISCONNECTED";
            }

            inbox.EvolutionLastHeartbeat = DateTimeOffset.UtcNow;
            inbox.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return (true, createResponse.Payload, connectPayload, null);
        }
        catch (EvolutionApiException ex)
        {
            logger.LogWarning(ex, "Evolution provisioning failed for inbox {InboxId}", inbox.Id);

            inbox.EvolutionSessionStatus = "DISCONNECTED";
            inbox.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            return (false, null, null, ex.Message);
        }
    }

    private IActionResult? ApplySecretUpdates(
        Inbox inbox,
        string? accessToken,
        string? refreshToken,
        out bool credentialsUpdated)
    {
        credentialsUpdated = false;

        if (accessToken is null && refreshToken is null)
            return null;

        var hadAnySecret = !string.IsNullOrWhiteSpace(inbox.EncryptedAccessToken) ||
                           !string.IsNullOrWhiteSpace(inbox.EncryptedRefreshToken);

        var encryptor = serviceProvider.GetService<AesGcmEncryptor>();
        if (encryptor is null)
        {
            return Problem(
                title: "Encryption is not configured",
                detail: "Set ENCRYPTION_MASTER_KEY to update inbox credentials.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (accessToken is not null)
        {
            inbox.EncryptedAccessToken = encryptor.Encrypt(accessToken);
            credentialsUpdated = true;
        }

        if (refreshToken is not null)
        {
            inbox.EncryptedRefreshToken = encryptor.Encrypt(refreshToken);
            credentialsUpdated = true;
        }

        if (credentialsUpdated && hadAnySecret)
            inbox.SecretVersion++;

        return null;
    }

    private static InboxResponse MapToResponse(Inbox inbox)
    {
        return new InboxResponse(
            inbox.Id,
            inbox.Name,
            inbox.ChannelType,
            inbox.PhoneNumber,
            ParseConfig(inbox.Config),
            inbox.ConfigSchemaVer,
            inbox.IsActive,
            inbox.BanStatus,
            inbox.BannedAt,
            inbox.BanReason,
            inbox.EvolutionInstanceName,
            inbox.EvolutionSessionStatus,
            inbox.EvolutionLastHeartbeat,
            inbox.ProxyOutboundId,
            !string.IsNullOrWhiteSpace(inbox.EncryptedAccessToken),
            !string.IsNullOrWhiteSpace(inbox.EncryptedRefreshToken),
            inbox.CreatedAt,
            inbox.UpdatedAt);
    }

    private static string NormalizeConfig(JsonElement? config)
    {
        if (!config.HasValue)
            return "{}";
        if (config.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return "{}";
        return config.Value.GetRawText();
    }

    private static JsonElement ParseConfig(string config)
    {
        if (string.IsNullOrWhiteSpace(config))
            return ParseConfig("{}");

        try
        {
            using var document = JsonDocument.Parse(config);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var fallback = JsonDocument.Parse("{}");
            return fallback.RootElement.Clone();
        }
    }

    private static string BuildEvolutionInstanceName(Guid inboxId, string phoneNumber)
    {
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits))
            digits = inboxId.ToString("N")[..12];

        if (digits.Length > 15)
            digits = digits[^15..];

        return $"noc-{digits}-{inboxId.ToString("N")[..8]}";
    }
}
