// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record ConversationAssignedEvent : NocEvent
{
    public required string ConversationId { get; init; }
    public required string AgentId { get; init; }
    public string? PreviousAgentId { get; init; }
}
