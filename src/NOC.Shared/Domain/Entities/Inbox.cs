// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class Inbox
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public ChannelType ChannelType { get; set; }
    public required string PhoneNumber { get; set; }

    // Non-sensitive config (throttle, UI settings, etc.)
    public string Config { get; set; } = "{}";
    public short ConfigSchemaVer { get; set; } = 1;

    // Encrypted secrets (AES-256-GCM, key in env var)
    public string? EncryptedAccessToken { get; set; }
    public string? EncryptedRefreshToken { get; set; }
    public short SecretVersion { get; set; } = 1;

    // State
    public bool IsActive { get; set; } = true;
    public BanStatus BanStatus { get; set; } = BanStatus.OK;
    public DateTimeOffset? BannedAt { get; set; }
    public string? BanReason { get; set; }

    // Evolution session (unofficial channels only)
    public string? EvolutionInstanceName { get; set; }
    public string? EvolutionSessionStatus { get; set; }
    public DateTimeOffset? EvolutionLastHeartbeat { get; set; }

    // Proxy assignment
    public Guid? ProxyOutboundId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ProxyOutbound? ProxyOutbound { get; set; }
    public ICollection<InboxAgent> InboxAgents { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<Campaign> Campaigns { get; set; } = [];
}
