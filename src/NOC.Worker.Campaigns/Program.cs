// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Worker.Campaigns;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
