// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class CampaignRecipient
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContactId { get; set; }
    public required string Phone { get; set; }

    // QUEUED | CLAIMED | SENT | DELIVERED | READ | FAILED | RETRY_PENDING
    public string Status { get; set; } = "QUEUED";

    // Lease for concurrent batch claiming
    public DateTimeOffset? ClaimedAt { get; set; }
    public string? ClaimedBy { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    // Tracking
    public string? ExternalId { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string? FailureReason { get; set; }
    public short RetryCount { get; set; }

    // Navigation
    public Campaign Campaign { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
}
