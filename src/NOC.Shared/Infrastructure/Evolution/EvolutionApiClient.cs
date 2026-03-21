// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Infrastructure.Evolution;

public sealed class EvolutionApiClient(
    HttpClient httpClient,
    EvolutionApiOptions apiOptions,
    ILogger<EvolutionApiClient> logger) : IEvolutionApiClient
{
    private const string CreateInstanceEndpoint = "instance/create";
    private const string ConnectInstanceEndpointTemplate = "instance/connect/{0}";
    private const string SendMessageEndpointTemplate = "message/sendText/{0}";
    private const string FindContactsEndpointTemplate = "chat/findContacts/{0}";
    private const string SetWebhookEndpointTemplate = "webhook/set/{0}";
    private const string FindWebhookEndpointTemplate = "webhook/find/{0}";
    private const string StatusEndpointTemplate = "instance/connectionState/{0}";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<EvolutionApiResponse> CreateInstanceAsync(
        EvolutionCreateInstanceRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = await SendAsync(HttpMethod.Post, CreateInstanceEndpoint, request, proxy, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> ConnectInstanceAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(ConnectInstanceEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Get, endpoint, content: null, proxy, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> SendMessageAsync(
        string instanceName,
        EvolutionSendMessageRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(request);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(SendMessageEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Post, endpoint, request, proxy, cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> FindContactsAsync(
        string instanceName,
        EvolutionFindContactsRequest? request = null,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(FindContactsEndpointTemplate, encodedName);
        var payload = await SendAsync(
            HttpMethod.Post,
            endpoint,
            request ?? new EvolutionFindContactsRequest(),
            proxy,
            cancellationToken);
        return new EvolutionApiResponse(payload);
    }

    public async Task<EvolutionApiResponse> SetWebhookAsync(
        string instanceName,
        EvolutionSetWebhookRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        ArgumentNullException.ThrowIfNull(request);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(SetWebhookEndpointTemplate, encodedName);
        var compatRequest = new EvolutionSetWebhookCompatRequest
        {
            Webhook = new EvolutionSetWebhookCompatPayload
            {
                Url = request.Url,
                Events = request.Events,
                Enabled = true,
                WebhookByEvents = request.WebhookByEvents,
                WebhookBase64 = request.WebhookBase64,
            }
        };

        try
        {
            var payload = await SendAsync(HttpMethod.Post, endpoint, compatRequest, proxy, cancellationToken);
            return new EvolutionApiResponse(payload);
        }
        catch (EvolutionApiException ex) when (CanFallbackToFlatWebhookPayload(ex))
        {
            var payload = await SendAsync(HttpMethod.Post, endpoint, request, proxy, cancellationToken);
            return new EvolutionApiResponse(payload);
        }
    }

    public async Task<EvolutionWebhookConfigurationResponse> GetWebhookAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(FindWebhookEndpointTemplate, encodedName);
        var (payload, isNullPayload) = await SendNullableAsync(HttpMethod.Get, endpoint, content: null, proxy, cancellationToken);

        var url = ExtractWebhookUrl(payload);
        var events = ExtractWebhookEvents(payload);

        return new EvolutionWebhookConfigurationResponse(
            instanceName,
            !isNullPayload && !string.IsNullOrWhiteSpace(url),
            url,
            events,
            payload);
    }

    public async Task<EvolutionInstanceStatusResponse> GetInstanceStatusAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);

        var encodedName = Uri.EscapeDataString(instanceName);
        var endpoint = string.Format(StatusEndpointTemplate, encodedName);
        var payload = await SendAsync(HttpMethod.Get, endpoint, content: null, proxy, cancellationToken);

        var status = ExtractStatus(payload);
        return new EvolutionInstanceStatusResponse(instanceName, status, payload);
    }

    private async Task<JsonObject> SendAsync(
        HttpMethod method,
        string endpoint,
        object? content,
        EvolutionProxyOptions? proxy,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, endpoint, content);
        var (response, responseBody) = await SendHttpRequestAsync(request, proxy, cancellationToken);

        using (response)
        {
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
        }

        return ParseJsonObject(responseBody);
    }

    private async Task<(JsonObject Payload, bool IsNullPayload)> SendNullableAsync(
        HttpMethod method,
        string endpoint,
        object? content,
        EvolutionProxyOptions? proxy,
        CancellationToken cancellationToken)
    {
        using var request = BuildRequest(method, endpoint, content);
        var (response, responseBody) = await SendHttpRequestAsync(request, proxy, cancellationToken);

        using (response)
        {
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
        }

        if (string.IsNullOrWhiteSpace(responseBody) || string.Equals(responseBody.Trim(), "null", StringComparison.OrdinalIgnoreCase))
            return (new JsonObject(), true);

        return (ParseJsonObject(responseBody), false);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, object? content)
    {
        var request = new HttpRequestMessage(method, endpoint);
        if (content is not null)
            request.Content = JsonContent.Create(content, options: SerializerOptions);

        return request;
    }

    private async Task<(HttpResponseMessage Response, string ResponseBody)> SendHttpRequestAsync(
        HttpRequestMessage request,
        EvolutionProxyOptions? proxy,
        CancellationToken cancellationToken)
    {
        if (proxy is null)
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response, responseBody);
        }

        using var proxyClient = CreateProxyHttpClient(proxy);
        using var proxiedRequest = CloneRequest(request, proxyClient.BaseAddress);
        var proxiedResponse = await proxyClient.SendAsync(proxiedRequest, cancellationToken);
        var proxiedResponseBody = await proxiedResponse.Content.ReadAsStringAsync(cancellationToken);
        return (proxiedResponse, proxiedResponseBody);
    }

    private HttpClient CreateProxyHttpClient(EvolutionProxyOptions proxy)
    {
        var handler = new HttpClientHandler
        {
            Proxy = BuildWebProxy(proxy),
            UseProxy = true,
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = httpClient.BaseAddress,
            Timeout = httpClient.Timeout,
        };

        client.DefaultRequestHeaders.Add(apiOptions.ApiKeyHeaderName, apiOptions.ApiKey);
        return client;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, Uri? baseAddress)
    {
        var targetUri = request.RequestUri;
        if (targetUri is not null && !targetUri.IsAbsoluteUri && baseAddress is not null)
            targetUri = new Uri(baseAddress, targetUri);

        var clone = new HttpRequestMessage(request.Method, targetUri);
        if (request.Content is not null)
            clone.Content = new StringContent(request.Content.ReadAsStringAsync().GetAwaiter().GetResult(), System.Text.Encoding.UTF8, request.Content.Headers.ContentType?.MediaType);

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    private static IWebProxy BuildWebProxy(EvolutionProxyOptions proxy)
    {
        var scheme = proxy.Protocol switch
        {
            ProxyProtocol.HTTPS => "https",
            ProxyProtocol.SOCKS5 => "socks5",
            _ => "http",
        };

        var webProxy = new WebProxy($"{scheme}://{proxy.Host}:{proxy.Port}");
        if (!string.IsNullOrWhiteSpace(proxy.Username))
            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);

        return webProxy;
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

    private static string? ExtractWebhookUrl(JsonObject payload)
    {
        return TryGetString(payload, "url")
            ?? TryGetNestedString(payload, "webhook", "url")
            ?? TryGetNestedString(payload, "data", "url");
    }

    private static IReadOnlyList<string> ExtractWebhookEvents(JsonObject payload)
    {
        return TryGetStringArray(payload, "events")
            ?? TryGetNestedStringArray(payload, "webhook", "events")
            ?? TryGetNestedStringArray(payload, "data", "events")
            ?? [];
    }

    private static string? TryGetNestedString(JsonObject jsonObject, string parentProperty, string nestedProperty)
    {
        if (jsonObject[parentProperty] is not JsonObject nested)
            return null;

        return TryGetString(nested, nestedProperty);
    }

    private static IReadOnlyList<string>? TryGetNestedStringArray(JsonObject jsonObject, string parentProperty, string nestedProperty)
    {
        if (jsonObject[parentProperty] is not JsonObject nested)
            return null;

        return TryGetStringArray(nested, nestedProperty);
    }

    private static string? TryGetString(JsonObject jsonObject, string propertyName)
    {
        if (jsonObject[propertyName] is not JsonValue value)
            return null;

        return value.TryGetValue<string>(out var result) ? result : null;
    }

    private static IReadOnlyList<string>? TryGetStringArray(JsonObject jsonObject, string propertyName)
    {
        if (jsonObject[propertyName] is not JsonArray array)
            return null;

        var values = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is JsonValue value && value.TryGetValue<string>(out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
                values.Add(stringValue);
        }

        return values;
    }

    private static string Truncate(string value, int maxLength = 1_000)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }

    private static bool CanFallbackToFlatWebhookPayload(EvolutionApiException exception)
    {
        if (exception.StatusCode is not System.Net.HttpStatusCode.BadRequest)
            return false;

        var responseBody = exception.ResponseBody ?? string.Empty;
        return !responseBody.Contains("instance requires property \"webhook\"", StringComparison.OrdinalIgnoreCase);
    }
}
