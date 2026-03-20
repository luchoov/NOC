// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using NOC.Shared.Domain.Enums;

namespace NOC.Web.Inboxes;

public sealed record CreateInboxRequest(
    string Name,
    ChannelType ChannelType,
    string PhoneNumber,
    JsonElement? Config = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? EvolutionInstanceName = null,
    bool AutoProvisionEvolution = true,
    bool AutoConnectEvolution = true);

public sealed record UpdateInboxRequest(
    string? Name = null,
    string? PhoneNumber = null,
    JsonElement? Config = null,
    bool? IsActive = null,
    BanStatus? BanStatus = null,
    string? BanReason = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? EvolutionInstanceName = null);

public sealed record ProvisionEvolutionRequest(bool AutoConnect = true);

public sealed record InboxResponse(
    Guid Id,
    string Name,
    ChannelType ChannelType,
    string PhoneNumber,
    JsonElement Config,
    short ConfigSchemaVer,
    bool IsActive,
    BanStatus BanStatus,
    DateTimeOffset? BannedAt,
    string? BanReason,
    string? EvolutionInstanceName,
    string? EvolutionSessionStatus,
    DateTimeOffset? EvolutionLastHeartbeat,
    Guid? ProxyOutboundId,
    bool HasAccessToken,
    bool HasRefreshToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateInboxResponse(
    InboxResponse Inbox,
    bool EvolutionProvisioned,
    JsonObject? EvolutionCreatePayload,
    JsonObject? EvolutionConnectPayload,
    string? EvolutionError);

public sealed record EvolutionOperationResponse(
    InboxResponse Inbox,
    string Operation,
    JsonObject Payload);

