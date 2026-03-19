// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }

    // Provider message ID (for webhook deduplication)
    public string? ExternalId { get; set; }

    public MessageDirection Direction { get; set; }
    public MessageType Type { get; set; }

    // Content
    public string? Content { get; set; }

    // Media (URL points to MinIO, not external provider)
    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public long? MediaSizeBytes { get; set; }
    public string? MediaFilename { get; set; }

    // Templates (official channels)
    public string? TemplateName { get; set; }
    public string? TemplateParams { get; set; }

    // Delivery status (OUTBOUND only)
    public DeliveryStatus? DeliveryStatus { get; set; }
    public DateTimeOffset? DeliveryUpdatedAt { get; set; }

    // Sender (for OUTBOUND)
    public Guid? SentByAgentId { get; set; }
    public bool SentByAi { get; set; }

    // Private note (invisible to customer)
    public bool IsPrivateNote { get; set; }

    // Raw provider metadata (for debugging)
    public string ProviderMetadata { get; set; } = "{}";

    // Immutable — no UpdatedAt
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = null!;
    public Agent? SentByAgent { get; set; }
    public ICollection<MessageStatusEvent> StatusEvents { get; set; } = [];
}
