using System.ComponentModel.Design.Serialization;
using System.Text.Json.Serialization;
using LockBox.Core.Config;

namespace LockBox.Core.Models;

/// <summary>
/// Root JSON structure for a .box file. Contains the public key, algorithm,
/// key digest for lookup, and an empty list of files (for later use).
/// </summary>
public class LockBoxDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = Defaults.LockBoxName;

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "RSA-OAEP-2048";

    [JsonPropertyName("publicKeyPem")]
    public string PublicKeyPem { get; set; } = string.Empty;

    [JsonPropertyName("publicKeyDigest")]
    public string PublicKeyDigest { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<FileEntry> Files { get; set; } = new();
}
