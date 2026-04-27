using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace LockBox.Core.Models;

public class LockBoxKey
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("thumbprint")]
    public required string Thumbprint { get; set; }

    [JsonPropertyName("privateKey")]
    public required string PrivateKey {  get; set; }

    [JsonPropertyName("publicKey")]
    public required string PublicKey { get; set; }
}
