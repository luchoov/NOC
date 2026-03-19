// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record MessageStatusChangedEvent : NocEvent
{
    public required string MessageId { get; init; }
    public required string Status { get; init; }
    public string? ProviderCode { get; init; }
}
