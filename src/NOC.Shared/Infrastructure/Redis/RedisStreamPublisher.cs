// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using StackExchange.Redis;

namespace NOC.Shared.Infrastructure.Redis;

public class RedisStreamPublisher
{
    private readonly IConnectionMultiplexer _redis;

    public RedisStreamPublisher(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task PublishAsync(string stream, string eventType, string payload, string? correlationId = null)
    {
        var db = _redis.GetDatabase();
        await db.StreamAddAsync(stream, [
            new NameValueEntry("type", eventType),
            new NameValueEntry("payload", payload),
            new NameValueEntry("correlation_id", correlationId ?? ""),
            new NameValueEntry("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString())
        ]);
    }
}
