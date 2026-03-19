using System.Security.Cryptography;
using System.Text.Json;

namespace LockBox.Services;

/// <summary>
/// Service for creating, loading, and managing lockbox files and keys.
/// </summary>
public sealed class PrivateKeyFileService
{
    /// <summary>
    /// Saves the private key to the app data directory with filename {publicKeyDigestHex}.key
    /// so it can be found later when the public key (or .box file) is known.
    /// Path is resolved on the calling thread (e.g. UI thread) to avoid platform crashes.
    /// </summary>
    public Task SavePrivateKeyAsync(string privateKeyPem, string publicKeyDigestHex, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(FileSystem.Current.AppDataDirectory, $"{publicKeyDigestHex}.key");
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.WriteAllText(path, privateKeyPem);
        }, cancellationToken);
    }

    /// <summary>
    /// Tries to load the private key for the given public key digest from app data.
    /// Returns null if the key file is not found.
    /// Path is resolved on the calling thread to avoid platform crashes.
    /// </summary>
    public Task<string?> TryGetPrivateKeyAsync(string publicKeyDigestHex, CancellationToken cancellationToken = default)
    {
        string path = Path.Combine(FileSystem.Current.AppDataDirectory, $"{publicKeyDigestHex}.key");
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
                return (string?)null;
            return File.ReadAllText(path);
        }, cancellationToken);
    }
}
