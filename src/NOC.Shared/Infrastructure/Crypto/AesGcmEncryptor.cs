// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;

namespace NOC.Shared.Infrastructure.Crypto;

/// <summary>
/// AES-256-GCM encryption for provider secrets (inbox tokens, proxy passwords).
/// Master key is loaded from ENCRYPTION_MASTER_KEY environment variable (base64-encoded 32 bytes).
/// Format: [12-byte nonce][16-byte tag][ciphertext]
/// </summary>
public sealed class AesGcmEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _masterKey;

    public AesGcmEncryptor(byte[] masterKey)
    {
        if (masterKey.Length != 32)
            throw new ArgumentException("Master key must be exactly 32 bytes (256 bits).", nameof(masterKey));
        _masterKey = masterKey;
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combine: [nonce][tag][ciphertext]
        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        ciphertext.CopyTo(result, NonceSize + TagSize);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedBase64)
    {
        var combined = Convert.FromBase64String(encryptedBase64);

        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid encrypted data: too short.");

        var nonce = combined.AsSpan(0, NonceSize);
        var tag = combined.AsSpan(NonceSize, TagSize);
        var ciphertext = combined.AsSpan(NonceSize + TagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
