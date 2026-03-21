// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Shared.Infrastructure.Storage;
using StackExchange.Redis;
using NOC.Worker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<NocDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .UseSnakeCaseNamingConvention());

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
builder.Services.AddEvolutionApiClient(builder.Configuration);
builder.Services.AddMediaStorage(builder.Configuration);

var masterKeyBase64 = builder.Configuration["Encryption:MasterKey"];
if (!string.IsNullOrEmpty(masterKeyBase64))
{
    var masterKey = Convert.FromBase64String(masterKeyBase64);
    builder.Services.AddSingleton(new AesGcmEncryptor(masterKey));
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
