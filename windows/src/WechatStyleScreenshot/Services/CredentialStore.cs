using System.Security.Cryptography;
using System.Text.Json;

namespace WechatStyleScreenshot.Services;

public sealed class CredentialStore
{
    private const string TranslationCredential = "DeepSeekTranslation";
    public string FilePath { get; }

    public CredentialStore(string? filePath = null) => FilePath = filePath ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WechatStyleScreenshot", "secrets.dat");

    public bool HasCredential(ImageEditProviderKind provider) => !string.IsNullOrWhiteSpace(GetCredential(provider));

    public bool HasTranslationCredential() => !string.IsNullOrWhiteSpace(GetTranslationCredential());
    public string? GetTranslationCredential() => GetCredentialByName(TranslationCredential);
    public void SaveTranslationCredential(string key) => SaveCredentialByName(TranslationCredential, key);
    public void DeleteTranslationCredential() => DeleteCredentialByName(TranslationCredential);

    public string? GetCredential(ImageEditProviderKind provider)
    {
        return GetCredentialByName(provider.ToString());
    }

    private string? GetCredentialByName(string name)
    {
        string? key = Read().GetValueOrDefault(name);
        return IsPlaceholder(key) ? null : key;
    }

    public void SaveCredential(ImageEditProviderKind provider, string key)
    {
        SaveCredentialByName(provider.ToString(), key);
    }

    private void SaveCredentialByName(string name, string key)
    {
        if (IsPlaceholder(key)) throw new ArgumentException("A real API key is required.", nameof(key));
        Dictionary<string, string> values = Read();
        values[name] = key.Trim();
        Write(values);
    }

    public void DeleteCredential(ImageEditProviderKind provider)
    {
        DeleteCredentialByName(provider.ToString());
    }

    private void DeleteCredentialByName(string name)
    {
        Dictionary<string, string> values = Read();
        values.Remove(name);
        Write(values);
    }

    private static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string trimmed = value.Trim();
        return trimmed.All(c => c is '●' or '•' or '*');
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
