// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Infrastructure.Storage;

public interface IMediaStorageService
{
    Task<MediaUploadResult> UploadAsync(
        Stream stream,
        string objectKey,
        string contentType,
        long contentLength,
        CancellationToken ct = default);

    Task<Stream> DownloadAsync(string objectKey, CancellationToken ct = default);

    Task<string> GeneratePresignedUrlAsync(string objectKey, TimeSpan expiry, CancellationToken ct = default);

    Task EnsureBucketAsync(CancellationToken ct = default);
}

public sealed record MediaUploadResult(string ObjectKey, string ContentType, long SizeBytes);
