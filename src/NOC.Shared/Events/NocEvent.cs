// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record NocEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public required string EventType { get; init; }
    public int EventVersion { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public required string CorrelationId { get; init; }
    public string? CausationId { get; init; }
}
