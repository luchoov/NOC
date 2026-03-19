// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid InboxId { get; set; }
    public Guid ContactId { get; set; }
    public Guid? AssignedTo { get; set; }
    public ConversationStatus Status { get; set; } = ConversationStatus.OPEN;
    public string? Subject { get; set; }

    // Denormalized for inbox tray performance
    public DateTimeOffset? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public string? LastMessageDirection { get; set; }
    public DateTimeOffset? LastInboundAt { get; set; }
    public DateTimeOffset? LastOutboundAt { get; set; }
    public int UnreadCount { get; set; }

    // Operation metrics
    public DateTimeOffset? FirstResponseAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }
    public short ReopenedCount { get; set; }

    // Optimistic locking
    public int RowVersion { get; set; }

    // AI
    public bool AiHandled { get; set; }
    public DateTimeOffset? AiEscalatedAt { get; set; }

    // Audit
    public Guid? ClosedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Inbox Inbox { get; set; } = null!;
    public Contact Contact { get; set; } = null!;
    public Agent? AssignedAgent { get; set; }
    public Agent? ClosedByAgent { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}
