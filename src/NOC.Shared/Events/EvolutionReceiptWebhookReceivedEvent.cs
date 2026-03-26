// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Events;

public record EvolutionReceiptWebhookReceivedEvent : NocEvent
{
    public required Guid InboxId { get; init; }
    public required string ExternalId { get; init; }
    public required int StatusCode { get; init; }
    public string? RemoteJid { get; init; }
    public string? InstanceName { get; init; }
}
