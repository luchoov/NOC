// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Web.Campaigns;

public sealed record CreateCampaignRequest(
    Guid InboxId,
    string Name,
    string MessageTemplate,
    string? MediaUrl = null,
    int? MessagesPerMinute = null,
    int? DelayMinMs = null,
    int? DelayMaxMs = null,
    TimeOnly? SendWindowStart = null,
    TimeOnly? SendWindowEnd = null,
    string? SendWindowTz = null,
    DateTimeOffset? ScheduledAt = null,
    // Audience source — exactly one must be provided
    Guid? ContactListId = null,
    Guid? SegmentId = null,
    IReadOnlyList<Guid>? ContactIds = null);

public sealed record UpdateCampaignRequest(
    string? Name = null,
    string? MessageTemplate = null,
    int? MessagesPerMinute = null,
    DateTimeOffset? ScheduledAt = null);

public sealed record ScheduleCampaignRequest(
    DateTimeOffset ScheduledAt);

public sealed record CampaignResponse(
    Guid Id,
    Guid InboxId,
    string InboxName,
    string Name,
    string Status,
    string MessageTemplate,
    string? MediaUrl,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? PausedAt,
    string? PausedReason,
    int MessagesPerMinute,
    int TotalRecipients,
    int SentCount,
    int DeliveredCount,
    int ReadCount,
    int FailedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CampaignRecipientResponse(
    Guid Id,
    Guid ContactId,
    string Phone,
    string? ContactName,
    string Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt,
    DateTimeOffset? FailedAt,
    string? FailureReason);
