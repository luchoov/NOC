// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NOC.Web.Hubs;

[Authorize]
public class NocHub : Hub
{
    public Task JoinInbox(string inboxId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"inbox:{inboxId}");

    public Task LeaveInbox(string inboxId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"inbox:{inboxId}");

    public Task JoinConversation(string conversationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");

    public Task LeaveConversation(string conversationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation:{conversationId}");
}
