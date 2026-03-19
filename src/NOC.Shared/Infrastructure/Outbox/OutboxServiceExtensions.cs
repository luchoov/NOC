// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.DependencyInjection;
using NOC.Shared.Infrastructure.Redis;
using StackExchange.Redis;

namespace NOC.Shared.Infrastructure.Outbox;

public static class OutboxServiceExtensions
{
    public static IServiceCollection AddOutbox(this IServiceCollection services, string redisConnectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<RedisStreamPublisher>();
        services.AddScoped<OutboxWriter>();
        services.AddHostedService<OutboxPublisherService>();

        return services;
    }
}
