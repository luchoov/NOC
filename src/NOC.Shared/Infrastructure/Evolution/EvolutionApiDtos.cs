// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NOC.Shared.Domain.Enums;

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

public sealed record EvolutionFindContactsRequest
{
    [JsonPropertyName("where")]
    public EvolutionFindContactsWhere? Where { get; init; }
}

public sealed record EvolutionFindContactsWhere
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

public sealed record EvolutionProxyOptions(
    ProxyProtocol Protocol,
    string Host,
    int Port,
    string? Username = null,
    string? Password = null);

public sealed record EvolutionSetWebhookRequest
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("events")]
    public required IReadOnlyList<string> Events { get; init; }

    [JsonPropertyName("webhook_by_events")]
    public bool WebhookByEvents { get; init; } = true;

    [JsonPropertyName("webhook_base64")]
    public bool WebhookBase64 { get; init; } = false;
}

public sealed record EvolutionSetWebhookCompatRequest
{
    [JsonPropertyName("webhook")]
    public required EvolutionSetWebhookCompatPayload Webhook { get; init; }
}

public sealed record EvolutionSetWebhookCompatPayload
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("events")]
    public required IReadOnlyList<string> Events { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("webhook_by_events")]
    public bool WebhookByEvents { get; init; } = true;

    [JsonPropertyName("webhook_base64")]
    public bool WebhookBase64 { get; init; } = false;
}

public sealed record EvolutionApiResponse(JsonObject Payload);

public sealed record EvolutionWebhookConfigurationResponse(
    string InstanceName,
    bool IsConfigured,
    string? Url,
    IReadOnlyList<string> Events,
    JsonObject Payload);

public sealed record EvolutionMediaDownloadRequest
{
    [JsonPropertyName("message")]
    public required JsonObject Message { get; init; }

    [JsonPropertyName("convertToMp4")]
    public bool ConvertToMp4 { get; init; }
}

public sealed record EvolutionMediaDownloadResponse(
    string? Base64,
    string? MimeType,
    string? FileName);

public sealed record EvolutionSendMediaRequest
{
    [JsonPropertyName("number")]
    public required string Number { get; init; }

    [JsonPropertyName("mediatype")]
    public required string MediaType { get; init; } // image, video, audio, document

    [JsonPropertyName("media")]
    public required string Media { get; init; } // URL or base64

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }
}

public sealed record EvolutionInstanceStatusResponse(
    string InstanceName,
    string Status,
    JsonObject Payload)
{
    public bool IsConnected => string.Equals(Status, "CONNECTED", StringComparison.OrdinalIgnoreCase);
}

