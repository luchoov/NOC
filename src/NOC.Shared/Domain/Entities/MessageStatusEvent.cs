// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class MessageStatusEvent
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public DeliveryStatus Status { get; set; }
    public string? ProviderCode { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    // Navigation
    public Message Message { get; set; } = null!;
}
