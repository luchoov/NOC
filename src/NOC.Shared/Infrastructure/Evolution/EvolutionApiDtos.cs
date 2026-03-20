// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace NOC.Shared.Infrastructure.Evolution;

public sealed record EvolutionCreateInstanceRequest
{
    [JsonPropertyName("instanceName")]
    public required string InstanceName { get; init; }

    [JsonPropertyName("integration")]
    public string Integration { get; init; } = "WHATSAPP-BAILEYS";

    [JsonPropertyName("qrcode")]
    public bool IncludeQrCode { get; init; } = true;

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record EvolutionSendMessageRequest
{
    [JsonPropertyName("number")]
    public required string Number { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record EvolutionApiResponse(JsonObject Payload);

public sealed record EvolutionInstanceStatusResponse(
    string InstanceName,
    string Status,
    JsonObject Payload)
{
    public bool IsConnected => string.Equals(Status, "CONNECTED", StringComparison.OrdinalIgnoreCase);
}

