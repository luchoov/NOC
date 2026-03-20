// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Infrastructure.Evolution;

public sealed class EvolutionApiOptions
{
    public const string ConfigurationSectionName = "EvolutionApi";

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
    public string ApiKeyHeaderName { get; init; } = "apikey";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            throw new InvalidOperationException(
                "Evolution API base URL is required. Configure EVOLUTION_API_URL or EvolutionApi:BaseUrl.");

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("Evolution API base URL must be an absolute URI.");

        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException(
                "Evolution API key is required. Configure EVOLUTION_API_KEY or EvolutionApi:ApiKey.");

        if (TimeoutSeconds is <= 0 or > 120)
            throw new InvalidOperationException("Evolution API timeout must be between 1 and 120 seconds.");
    }
}

