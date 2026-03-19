// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Infrastructure.Evolution;

public interface IEvolutionApiClient
{
    Task<EvolutionApiResponse> CreateInstanceAsync(
        EvolutionCreateInstanceRequest request,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> ConnectInstanceAsync(
        string instanceName,
        CancellationToken cancellationToken = default);

    Task<EvolutionApiResponse> SendMessageAsync(
        string instanceName,
        EvolutionSendMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<EvolutionInstanceStatusResponse> GetInstanceStatusAsync(
        string instanceName,
        CancellationToken cancellationToken = default);
}

