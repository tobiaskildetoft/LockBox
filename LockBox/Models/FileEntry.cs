using System.Text.Json.Serialization;

namespace LockBox.Models;

/// <summary>
/// Placeholder for a file entry in the lockbox. Later will contain filename,
/// encrypted content, and metadata needed to decrypt with the private key.
/// </summary>
public class FileEntry
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("encryptedContentBase64")]
    public string EncryptedContentBase64 { get; set; } = string.Empty;

    [JsonPropertyName("decryptInfo")]
    public string DecryptInfo { get; set; } = string.Empty;
}
