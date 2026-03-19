// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NOC.Shared.Infrastructure.Evolution;

public sealed class EvolutionApiClient(HttpClient httpClient, ILogger<EvolutionApiClient> logger) : IEvolutionApiClient
{
    private const string CreateInstanceEndpoint = "instance/create";
    private const string ConnectInstanceEndpointTemplate = "instance/connect/{0}";
    private const string SendMessageEndpointTemplate = "message/sendText/{0}";
    private const string StatusEndpointTemplate = "instance/connectionState/{0}";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<EvolutionApiResponse> CreateInstanceAsync(
        EvolutionCreateInstanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = await SendAsync(HttpMethod.Post, CreateInstanceEndpoint, request, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> ConnectInstanceAsync(
        string instanceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(ConnectInstanceEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Get, endpoint, content: null, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> SendMessageAsync(
        string instanceName,
        EvolutionSendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(request);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(SendMessageEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Post, endpoint, request, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionInstanceStatusResponse> GetInstanceStatusAsync(
        string instanceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(StatusEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Get, endpoint, content: null, cancellationToken);

        var status = ExtractStatus(payload);
        return new EvolutionInstanceStatusResponse(instanceName, status, payload);
    }

    private async Task<JsonObject> SendAsync(
        HttpMethod method,
        string endpoint,
        object? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, endpoint);
        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = $"Evolution API request failed ({(int)response.StatusCode} {response.ReasonPhrase}) for endpoint '{endpoint}'.";
            logger.LogWarning(
                "Evolution API request failed. Endpoint: {Endpoint}, Status: {StatusCode}, Body: {ResponseBody}",
                endpoint,
                (int)response.StatusCode,
                Truncate(responseBody));
            throw new EvolutionApiException(message, response.StatusCode, responseBody);
        }

        return ParseJsonObject(responseBody);
    }

    private static JsonObject ParseJsonObject(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return new JsonObject();

        try
        {
            var node = JsonNode.Parse(responseBody);
            return node switch
            {
                JsonObject jsonObject => jsonObject,
                JsonArray jsonArray => new JsonObject { ["items"] = jsonArray },
                JsonValue jsonValue => new JsonObject { ["value"] = jsonValue },
                _ => new JsonObject()
            };
        }
        catch (JsonException)
        {
            return new JsonObject { ["raw"] = responseBody };
        }
    }

    private static string ExtractStatus(JsonObject payload)
    {
        var rawStatus =
            TryGetString(payload, "status") ??
            TryGetString(payload, "state") ??
            TryGetNestedString(payload, "instance", "status") ??
            TryGetNestedString(payload, "instance", "state") ??
            TryGetNestedString(payload, "instance", "connectionStatus");

        return NormalizeStatus(rawStatus);
    }

    private static string NormalizeStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus))
            return "UNKNOWN";

        return rawStatus.Trim().ToUpperInvariant() switch
        {
            "OPEN" or "CONNECTED" => "CONNECTED",
            "CLOSE" or "CLOSED" or "DISCONNECTED" => "DISCONNECTED",
            "CONNECTING" or "QR" or "QRCODE" or "QR_PENDING" => "QR_PENDING",
            var status => status,
        };
    }

    private static string? TryGetNestedString(JsonObject jsonObject, string parentProperty, string nestedProperty)
    {
        if (jsonObject[parentProperty] is not JsonObject nested)
            return null;

        return TryGetString(nested, nestedProperty);
    }

    private static string? TryGetString(JsonObject jsonObject, string propertyName)
    {
        if (jsonObject[propertyName] is not JsonValue value)
            return null;

        return value.TryGetValue<string>(out var result) ? result : null;
    }

    private static string Truncate(string value, int maxLength = 1_000)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
