// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class OutboxEvent
{
    public Guid Id { get; set; }
    public required string Stream { get; set; }
    public required string EventType { get; set; }
    public short EventVersion { get; set; } = 1;
    public required string Payload { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationId { get; set; }

    public bool Published { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public short RetryCount { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
