// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record EvolutionStatusWebhookReceivedEvent : NocEvent
{
    public required Guid InboxId { get; init; }
    public string? InstanceName { get; init; }
    public required string Status { get; init; }
    public required string RawPayload { get; init; }
}

