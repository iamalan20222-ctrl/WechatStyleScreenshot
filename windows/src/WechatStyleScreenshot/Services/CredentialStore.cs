using System.Security.Cryptography;
using System.Text.Json;

namespace WechatStyleScreenshot.Services;

public sealed class CredentialStore
{
    public string FilePath { get; }

    public CredentialStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatStyleScreenshot", "secrets.dat");

    public bool HasCredential(ImageEditProviderKind provider) => !string.IsNullOrWhiteSpace(GetCredential(provider));

    public string? GetCredential(ImageEditProviderKind provider)
    {
        Dictionary<string, string> values = Read();
        return values.GetValueOrDefault(provider.ToString());
    }

    public void SaveCredential(ImageEditProviderKind provider, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("API key is required.", nameof(key));
        Dictionary<string, string> values = Read();
        values[provider.ToString()] = key.Trim();
        Write(values);
    }

    public void DeleteCredential(ImageEditProviderKind provider)
    {
        Dictionary<string, string> values = Read();
        values.Remove(provider.ToString());
        Write(values);
    }

    private Dictionary<string, string> Read()
    {
        if (!File.Exists(FilePath)) return new();
        byte[] encrypted = File.ReadAllBytes(FilePath);
        byte[] plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(plain) ?? new(); }
        catch (JsonException) { return new(); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private void Write(Dictionary<string, string> values)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(values);
        byte[] encrypted;
        try { encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        string temp = FilePath + ".tmp";
        try
        {
            File.WriteAllBytes(temp, encrypted);
            File.Move(temp, FilePath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
