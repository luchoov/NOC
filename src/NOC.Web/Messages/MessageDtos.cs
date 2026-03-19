// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using NOC.Shared.Domain.Enums;

namespace NOC.Web.Messages;

public sealed record SendMessageRequest(
    string Content,
    MessageType Type = MessageType.TEXT,
    bool IsPrivateNote = false);

public sealed record MessageResponse(
    Guid Id,
    Guid ConversationId,
    string? ExternalId,
    MessageDirection Direction,
    MessageType Type,
    string? Content,
    string? MediaUrl,
    DeliveryStatus? DeliveryStatus,
    DateTimeOffset? DeliveryUpdatedAt,
    Guid? SentByAgentId,
    bool SentByAi,
    bool IsPrivateNote,
    JsonElement ProviderMetadata,
    DateTimeOffset CreatedAt);

