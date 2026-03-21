// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace NOC.Shared.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddMediaStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new MinioOptions();
        configuration.GetSection("MinIO").Bind(options);
        services.AddSingleton(options);

        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey)
                .WithSSL(options.UseSSL)
                .Build());

        services.AddSingleton<IMediaStorageService, MinioMediaStorageService>();

        return services;
    }
}
