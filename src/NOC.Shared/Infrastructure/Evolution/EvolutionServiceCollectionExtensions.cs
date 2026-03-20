// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace NOC.Shared.Infrastructure.Evolution;

public static class EvolutionServiceCollectionExtensions
{
    public static IServiceCollection AddEvolutionApiClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(EvolutionApiOptions.ConfigurationSectionName);

        var options = new EvolutionApiOptions
        {
            BaseUrl = configuration["EVOLUTION_API_URL"] ?? section["BaseUrl"] ?? string.Empty,
            ApiKey = configuration["EVOLUTION_API_KEY"] ?? section["ApiKey"] ?? string.Empty,
            TimeoutSeconds = section.GetValue<int?>("TimeoutSeconds") ?? 30,
            ApiKeyHeaderName = section["ApiKeyHeaderName"] ?? "apikey",
        };

        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient<IEvolutionApiClient, EvolutionApiClient>(client =>
            {
                var normalizedBaseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
                    ? options.BaseUrl
                    : $"{options.BaseUrl}/";

                client.BaseAddress = new Uri(normalizedBaseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Add(options.ApiKeyHeaderName, options.ApiKey);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(retryCount: 3, sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
    }
}
