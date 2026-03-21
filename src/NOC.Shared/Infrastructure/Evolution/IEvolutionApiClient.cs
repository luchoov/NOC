// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Infrastructure.Evolution;

public interface IEvolutionApiClient
{
    Task<EvolutionApiResponse> CreateInstanceAsync(
        EvolutionCreateInstanceRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> ConnectInstanceAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> SendMessageAsync(
        string instanceName,
        EvolutionSendMessageRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> FindContactsAsync(
        string instanceName,
        EvolutionFindContactsRequest? request = null,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> SetWebhookAsync(
        string instanceName,
        EvolutionSetWebhookRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionWebhookConfigurationResponse> GetWebhookAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionInstanceStatusResponse> GetInstanceStatusAsync(
        string instanceName,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionMediaDownloadResponse> DownloadMediaAsync(
        string instanceName,
        EvolutionMediaDownloadRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> SendMediaMessageAsync(
        string instanceName,
        EvolutionSendMediaRequest request,
        EvolutionProxyOptions? proxy = null,
        CancellationToken cancellationToken = default);
}

