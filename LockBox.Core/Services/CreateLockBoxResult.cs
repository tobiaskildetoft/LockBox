namespace LockBox.Core.Services;

/// <summary>
/// Result of creating a new lockbox. Contains the .box file content and key material
/// so the UI can save the .box via FileSaver and then persist the private key.
/// </summary>
public sealed class CreateLockBoxResult
{
    public byte[] BoxContent { get; init; } = Array.Empty<byte>();
    public string PrivateKeyPem { get; init; } = string.Empty;
    public string PublicKeyDigestHex { get; init; } = string.Empty;
}
