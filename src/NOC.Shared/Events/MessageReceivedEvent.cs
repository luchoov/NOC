// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record MessageReceivedEvent : NocEvent
{
    public required string ConversationId { get; init; }
    public required string InboxId { get; init; }
    public required string ContactId { get; init; }
    public required string MessageId { get; init; }
    public required string Content { get; init; }
    public string? MediaUrl { get; init; }
    public List<ConversationTurn> History { get; init; } = [];
}

public record ConversationTurn
{
    public required string Role { get; init; }
    public required string Content { get; init; }
    public required string Timestamp { get; init; }
}
