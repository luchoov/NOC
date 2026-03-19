// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Web.Conversations;

public sealed record AssignConversationRequest(Guid AgentId, int ExpectedRowVersion);

public sealed record UpdateConversationStatusRequest(
    ConversationStatus Status,
    int ExpectedRowVersion,
    DateTimeOffset? SnoozedUntil = null);

public sealed record ConversationResponse(
    Guid Id,
    Guid InboxId,
    Guid ContactId,
    string ContactPhone,
    string? ContactName,
    Guid? AssignedTo,
    ConversationStatus Status,
    string? Subject,
    DateTimeOffset? LastMessageAt,
    string? LastMessagePreview,
    string? LastMessageDirection,
    int UnreadCount,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? SnoozedUntil,
    short ReopenedCount,
    int RowVersion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

