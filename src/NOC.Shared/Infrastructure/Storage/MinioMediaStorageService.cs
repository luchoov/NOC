// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace NOC.Shared.Infrastructure.Storage;

public sealed class MinioMediaStorageService(
    IMinioClient minio,
    MinioOptions options,
    ILogger<MinioMediaStorageService> logger) : IMediaStorageService
{
    public async Task<MediaUploadResult> UploadAsync(
        Stream stream,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken ct = default)
    {
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(contentLength)
            .WithContentType(contentType), ct);

        logger.LogDebug("Uploaded media to MinIO: {ObjectKey} ({ContentType}, {Size} bytes)",
            objectKey, contentType, contentLength);

        return new MediaUploadResult(objectKey, contentType, contentLength);
    }

    public async Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await minio.GetObjectAsync(new GetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);

        ms.Position = 0;
        return ms;
    }

    public async Task<string> GeneratePresignedUrlAsync(string objectKey, TimeSpan expiry, CancellationToken ct = default)
    {
        var url = await minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds));

        return url;
    }

    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        var exists = await minio.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(options.Bucket), ct);

        if (!exists)
        {
            await minio.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(options.Bucket), ct);
            logger.LogInformation("Created MinIO bucket: {Bucket}", options.Bucket);
        }
    }
}
