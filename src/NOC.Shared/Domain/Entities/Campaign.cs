// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class Campaign
{
    public Guid Id { get; set; }
    public Guid InboxId { get; set; }
    public required string Name { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.DRAFT;
    public required string MessageTemplate { get; set; }
    public string? MediaUrl { get; set; }

    // Scheduling
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public string? PausedReason { get; set; }

    // Throttle config
    public int MessagesPerMinute { get; set; } = 10;
    public int DelayMinMs { get; set; } = 2000;
    public int DelayMaxMs { get; set; } = 8000;

    // Time window (optional)
    public TimeOnly? SendWindowStart { get; set; }
    public TimeOnly? SendWindowEnd { get; set; }
    public string? SendWindowTz { get; set; }

    // Denormalized counters (reconcile periodically)
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ReadCount { get; set; }
    public int FailedCount { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Inbox Inbox { get; set; } = null!;
    public Agent? CreatedByAgent { get; set; }
    public ICollection<CampaignRecipient> Recipients { get; set; } = [];
}
