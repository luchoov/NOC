// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;

namespace NOC.Shared.Domain.Entities;

public class AuditEvent
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public required string ActorType { get; set; }
    public required string EventType { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Payload { get; set; } = "{}";
    public IPAddress? IpAddress { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    // Navigation
    public Agent? Actor { get; set; }
}
