using System.Text.Json.Serialization;

namespace LockBox.Models;

/// <summary>
/// Root JSON structure for a .box file. Contains the public key, algorithm,
/// key digest for lookup, and an empty list of files (for later use).
/// </summary>
public class LockBoxDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "RSA-OAEP-2048";

    [JsonPropertyName("publicKeyPem")]
    public string PublicKeyPem { get; set; } = string.Empty;

    [JsonPropertyName("publicKeyDigest")]
    public string PublicKeyDigest { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<FileEntry> Files { get; set; } = new();
}
