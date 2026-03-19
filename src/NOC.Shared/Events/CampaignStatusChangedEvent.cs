// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record CampaignStatusChangedEvent : NocEvent
{
    public required string CampaignId { get; init; }
    public required string Status { get; init; }
    public string? Reason { get; init; }
}
