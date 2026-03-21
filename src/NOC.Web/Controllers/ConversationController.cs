// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Conversations;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationController(NocDbContext db, AuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? inboxId = null,
        [FromQuery] ConversationStatus? status = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] DateTimeOffset? beforeLastMessageAt = null,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 100);

        var requesterRole = ResolveRole();
        if (requesterRole == AgentRole.AGENT && !inboxId.HasValue)
            return BadRequest(new { message = "Agents must filter by inboxId." });

        var query = db.Conversations
            .AsNoTracking()
            .Include(c => c.Contact)
            .AsQueryable();

        if (inboxId.HasValue)
        {
            if (!await HasInboxAccessAsync(inboxId.Value))
                return Forbid();

            query = query.Where(c => c.InboxId == inboxId.Value);
        }
        else if (requesterRole == AgentRole.AGENT)
        {
            var requesterAgentId = GetCurrentAgentId();
            if (!requesterAgentId.HasValue)
                return Forbid();

            var accessibleInboxIds = db.InboxAgents
                .Where(x => x.AgentId == requesterAgentId.Value)
                .Select(x => x.InboxId);
            query = query.Where(c => accessibleInboxIds.Contains(c.InboxId));
        }

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        if (assignedTo.HasValue)
            query = query.Where(c => c.AssignedTo == assignedTo.Value);

        if (beforeLastMessageAt.HasValue && beforeId.HasValue)
            query = query.Where(c => c.LastMessageAt != null && c.LastMessageAt < beforeLastMessageAt.Value);

        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(conversations.Select(MapToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var conversation = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        return Ok(MapToResponse(conversation));
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignConversationRequest request)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        var targetAgent = await db.Agents.FirstOrDefaultAsync(a => a.Id == request.AgentId && a.IsActive);
        if (targetAgent is null)
            return BadRequest(new { message = "Target agent does not exist or is inactive." });

        var requesterRole = ResolveRole();
        var canReassign = requesterRole is AgentRole.ADMIN or AgentRole.SUPERVISOR;
        var previousAssignee = conversation.AssignedTo;

        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE conversations
SET
    assigned_to = {request.AgentId},
    status = {ConversationStatus.ASSIGNED.ToString()},
    row_version = row_version + 1,
    updated_at = now()
WHERE
    id = {id}
    AND row_version = {request.ExpectedRowVersion}
    AND (assigned_to IS NULL OR {canReassign});");

        if (rowsAffected == 0)
            return Conflict(new { message = "Conversation was modified concurrently. Refresh and retry." });

        var updated = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Contact)
            .FirstAsync(c => c.Id == id);

        await auditService.LogAsync(
            "CONVERSATION_ASSIGNED",
            entityType: "CONVERSATION",
            entityId: id,
            payload: new
            {
                previousAssignee,
                newAssignee = request.AgentId,
                expectedRowVersion = request.ExpectedRowVersion,
                currentRowVersion = updated.RowVersion,
            });

        return Ok(MapToResponse(updated));
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateConversationStatusRequest request)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        if (request.Status == ConversationStatus.SNOOZED && request.SnoozedUntil is null)
            return BadRequest(new { message = "SnoozedUntil is required when status is SNOOZED." });

        var requesterAgentId = GetCurrentAgentId();
        var statusText = request.Status.ToString();
        var snoozedUntil = request.Status == ConversationStatus.SNOOZED ? request.SnoozedUntil : null;
        var rowsAffected = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE conversations
SET
    status = {statusText},
    snoozed_until = {snoozedUntil},
    resolved_at = CASE WHEN {statusText} = 'RESOLVED' THEN now() ELSE resolved_at END,
    closed_by = CASE WHEN {statusText} = 'RESOLVED' THEN {requesterAgentId} ELSE closed_by END,
    row_version = row_version + 1,
    updated_at = now()
WHERE
    id = {id}
    AND row_version = {request.ExpectedRowVersion};");

        if (rowsAffected == 0)
            return Conflict(new { message = "Conversation was modified concurrently. Refresh and retry." });

        var updated = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Contact)
            .FirstAsync(c => c.Id == id);

        await auditService.LogAsync(
            request.Status == ConversationStatus.RESOLVED ? "CONVERSATION_RESOLVED" : "CONVERSATION_STATUS_CHANGED",
            entityType: "CONVERSATION",
            entityId: id,
            payload: new
            {
                previousStatus = conversation.Status.ToString(),
                newStatus = request.Status.ToString(),
                request.SnoozedUntil,
                expectedRowVersion = request.ExpectedRowVersion,
                currentRowVersion = updated.RowVersion,
            });

        return Ok(MapToResponse(updated));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });
        if (!await HasInboxAccessAsync(conversation.InboxId))
            return Forbid();

        if (conversation.UnreadCount == 0)
            return NoContent();

        conversation.UnreadCount = 0;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN,SUPERVISOR")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var conversation = await db.Conversations
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conversation is null)
            return NotFound(new { message = "Conversation not found" });

        // Messages cascade via EF config
        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync();

        await auditService.LogAsync(
            "CONVERSATION_DELETED",
            entityType: "CONVERSATION",
            entityId: id,
            payload: new { conversation.ContactId, contactPhone = conversation.Contact.Phone });

        return NoContent();
    }

    private async Task<bool> HasInboxAccessAsync(Guid inboxId)
    {
        var role = ResolveRole();
        if (role is AgentRole.ADMIN or AgentRole.SUPERVISOR)
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

    private AgentRole ResolveRole()
    {
        if (User.IsInRole(nameof(AgentRole.ADMIN)))
            return AgentRole.ADMIN;
        if (User.IsInRole(nameof(AgentRole.SUPERVISOR)))
            return AgentRole.SUPERVISOR;
        return AgentRole.AGENT;
    }

    private static ConversationResponse MapToResponse(Conversation conversation)
    {
        return new ConversationResponse(
            conversation.Id,
            conversation.InboxId,
            conversation.ContactId,
            conversation.Contact.Phone,
            conversation.Contact.Name,
            conversation.AssignedTo,
            conversation.Status,
            conversation.Subject,
            conversation.LastMessageAt,
            conversation.LastMessagePreview,
            conversation.LastMessageDirection,
            conversation.UnreadCount,
            conversation.FirstResponseAt,
            conversation.ResolvedAt,
            conversation.SnoozedUntil,
            conversation.ReopenedCount,
            conversation.RowVersion,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }
}
