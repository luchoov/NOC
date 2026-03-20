// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public short PasswordVersion { get; set; } = 1;
    public AgentRole Role { get; set; } = AgentRole.AGENT;
    public bool IsActive { get; set; } = true;
    public string? DisabledReason { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset PasswordUpdatedAt { get; set; }

    // Refresh token (hashed)
    public string? RefreshTokenHash { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<InboxAgent> InboxAgents { get; set; } = [];
}
