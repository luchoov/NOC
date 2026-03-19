// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using NOC.Shared.Infrastructure.Data;
using StackExchange.Redis;
using NOC.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<NocDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .UseSnakeCaseNamingConvention());

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
