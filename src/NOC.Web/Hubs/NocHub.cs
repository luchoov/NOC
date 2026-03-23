// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure.Data;

namespace NOC.Web.Hubs;

[Authorize]
public class NocHub(NocDbContext db) : Hub
{
    public async Task JoinInbox(string inboxId)
    {
        if (!Guid.TryParse(inboxId, out var parsedInboxId))
            throw new HubException("Invalid inbox id.");

        await EnsureInboxAccessAsync(parsedInboxId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"inbox:{inboxId}");
    }

    public Task LeaveInbox(string inboxId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"inbox:{inboxId}");

    public async Task JoinConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var parsedConversationId))
            throw new HubException("Invalid conversation id.");

        var inboxId = await db.Conversations
            .AsNoTracking()
            .Where(conversation => conversation.Id == parsedConversationId)
            .Select(conversation => (Guid?)conversation.InboxId)
            .FirstOrDefaultAsync();

        if (!inboxId.HasValue)
            throw new HubException("Conversation not found.");

        await EnsureInboxAccessAsync(inboxId.Value);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
    }

    public Task LeaveConversation(string conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    private async Task EnsureInboxAccessAsync(Guid inboxId)
    {
        if (ResolveRole() is AgentRole.ADMIN or AgentRole.SUPERVISOR)
            return;

        var agentId = GetCurrentAgentId();
        if (!agentId.HasValue)
            throw new HubException("Unauthorized.");

        var hasAccess = await db.InboxAgents
            .AsNoTracking()
            .AnyAsync(inboxAgent => inboxAgent.InboxId == inboxId && inboxAgent.AgentId == agentId.Value);

        if (!hasAccess)
            throw new HubException("Forbidden.");
    }

    private Guid? GetCurrentAgentId()
    {
        var raw = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var agentId) ? agentId : null;
    }

    private AgentRole ResolveRole()
    {
        if (Context.User?.IsInRole(nameof(AgentRole.ADMIN)) == true)
            return AgentRole.ADMIN;
        if (Context.User?.IsInRole(nameof(AgentRole.SUPERVISOR)) == true)
            return AgentRole.SUPERVISOR;
        return AgentRole.AGENT;
    }
}
