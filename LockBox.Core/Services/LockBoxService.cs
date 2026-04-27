using System.Security.Cryptography;
using System.Text.Json;
using LockBox.Core.Models;

namespace LockBox.Core.Services;

/// TODOs: 
/// Worth keeping file system things in the Core? And just move parts specific to app paths
/// Remove all references to the filesystem and move those to MAUI part
/// Consider if there is any actual value in doing these as async

/// <summary>
/// Service for creating, loading, and managing lockbox files and keys.
/// </summary>
public sealed class LockBoxService
{
    private const int RsaKeySizeBits = 2048;
    private const int AesKeySizeBytes = 32;
    private const int AesGcmNonceSizeBytes = 12;
    private const int AesGcmTagSizeBytes = 16;
    private const int RsaEncryptedBlobSize = AesKeySizeBytes + AesGcmNonceSizeBytes + AesGcmTagSizeBytes; // 60 bytes

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new RSA keypair and builds the .box document (JSON with public key and empty files list).
    /// The caller should save the .box content via FileSaver, then call SavePrivateKeyAsync on success.
    /// </summary>
    public Task<CreateLockBoxResult> CreateNewLockBoxAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var rsa = RSA.Create(RsaKeySizeBits);

            string publicKeyPem = rsa.ExportRSAPublicKeyPem();
            string privateKeyPem = rsa.ExportRSAPrivateKeyPem();
            byte[] publicKeyBytes = rsa.ExportRSAPublicKey();
            byte[] digest = SHA256.HashData(publicKeyBytes);
            string publicKeyDigestHex = Convert.ToHexString(digest).ToLowerInvariant();

            var document = new LockBoxDocument
            {
                Version = 1,
                Name = name,
                Algorithm = "RSA-OAEP-2048",
                PublicKeyPem = publicKeyPem,
                PublicKeyDigest = publicKeyDigestHex,
                Files = new List<FileEntry>()
            };

            byte[] boxContent = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);

            return new CreateLockBoxResult
            {
                BoxContent = boxContent,
                PrivateKeyPem = privateKeyPem,
                PublicKeyDigestHex = publicKeyDigestHex
            };
        }, cancellationToken);
    }

    /// <summary>
    /// Loads a lockbox document from a .box file path.
    /// </summary>
    public Task<LockBoxDocument> LoadDocumentFromFileAsync(string boxFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            string json = File.ReadAllText(boxFilePath);
            return DeserializeDocument(json);
        }, cancellationToken);
    }

    /// <summary>
    /// Loads a lockbox document from a stream (e.g. from FileResult.OpenReadAsync on Android).
    /// </summary>
    public async Task<LockBoxDocument> LoadDocumentFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync(cancellationToken);
        return DeserializeDocument(json);
    }

    private static LockBoxDocument DeserializeDocument(string json)
    {
        var doc = JsonSerializer.Deserialize<LockBoxDocument>(json)
            ?? throw new InvalidOperationException("Invalid lockbox file.");
        return doc;
    }

    /// <summary>
    /// Saves the lockbox document back to the .box file.
    /// </summary>
    public Task SaveDocumentAsync(LockBoxDocument document, string boxFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
            File.WriteAllBytes(boxFilePath, json);
        }, cancellationToken);
    }

    /// <summary>
    /// Adds a file to the lockbox: encrypts file contents with hybrid encryption (AES-GCM + RSA-OAEP)
    /// and appends a new FileEntry to the document. Caller must save the document after.
    /// </summary>
    public Task<FileEntry> AddFileToLockBoxAsync(LockBoxDocument document, string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] plaintext = File.ReadAllBytes(filePath);
            string filename = Path.GetFileName(filePath);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(document.PublicKeyPem);

            byte[] aesKey = new byte[AesKeySizeBytes];
            byte[] nonce = new byte[AesGcmNonceSizeBytes];
            RandomNumberGenerator.Fill(aesKey);
            RandomNumberGenerator.Fill(nonce);

            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[AesGcmTagSizeBytes];
            using (var aes = new AesGcm(aesKey, AesGcmTagSizeBytes))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
            }

            byte[] blobToEncrypt = new byte[RsaEncryptedBlobSize];
            Buffer.BlockCopy(aesKey, 0, blobToEncrypt, 0, AesKeySizeBytes);
            Buffer.BlockCopy(nonce, 0, blobToEncrypt, AesKeySizeBytes, AesGcmNonceSizeBytes);
            Buffer.BlockCopy(tag, 0, blobToEncrypt, AesKeySizeBytes + AesGcmNonceSizeBytes, AesGcmTagSizeBytes);

            byte[] encryptedBlob = rsa.Encrypt(blobToEncrypt, RSAEncryptionPadding.OaepSHA256);

            var entry = new FileEntry
            {
                Filename = filename,
                EncryptedContentBase64 = Convert.ToBase64String(ciphertext),
                DecryptInfo = Convert.ToBase64String(encryptedBlob)
            };
            document.Files.Add(entry);
            return entry;
        }, cancellationToken);
    }

    public void RemoveFileFromLockBox(LockBoxDocument lockBoxDocument, FileEntry fileEntry)
    {
        lockBoxDocument.Files.Remove(fileEntry);
    }

    /// <summary>
    /// Decrypts a file entry using the given private key PEM. Returns the raw file content.
    /// </summary>
    public Task<byte[]> DecryptFileContentAsync(FileEntry entry, string privateKeyPem, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] encryptedBlob = Convert.FromBase64String(entry.DecryptInfo);
            byte[] ciphertext = Convert.FromBase64String(entry.EncryptedContentBase64);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);
            byte[] blob = rsa.Decrypt(encryptedBlob, RSAEncryptionPadding.OaepSHA256);
            if (blob.Length != RsaEncryptedBlobSize)
                throw new InvalidOperationException("Invalid decrypt info.");

            byte[] aesKey = new byte[AesKeySizeBytes];
            byte[] nonce = new byte[AesGcmNonceSizeBytes];
            byte[] tag = new byte[AesGcmTagSizeBytes];
            Buffer.BlockCopy(blob, 0, aesKey, 0, AesKeySizeBytes);
            Buffer.BlockCopy(blob, AesKeySizeBytes, nonce, 0, AesGcmNonceSizeBytes);
            Buffer.BlockCopy(blob, AesKeySizeBytes + AesGcmNonceSizeBytes, tag, 0, AesGcmTagSizeBytes);

            byte[] plaintext = new byte[ciphertext.Length];
            using (var aes = new AesGcm(aesKey, AesGcmTagSizeBytes))
            {
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            return plaintext;
        }, cancellationToken);
    }
}
