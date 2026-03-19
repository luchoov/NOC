// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record ConversationCreatedEvent : NocEvent
{
    public required string ConversationId { get; init; }
    public required string InboxId { get; init; }
    public required string ContactId { get; init; }
}
