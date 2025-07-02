using System.Security.Cryptography;
using System.Text;

namespace Secure_QR;

public static class EncryptionService
{
    // AES key and IV - in production, these should be securely generated and stored
    private static readonly byte[] AES_KEY = Encoding.UTF8.GetBytes("MySecureKey12345"); // 16 bytes for AES-128
    private static readonly byte[] AES_IV = Encoding.UTF8.GetBytes("MySecureIV123456"); // 16 bytes

    // RSA key pair - in production, these should be securely generated and stored
    private static RSA? _rsaProvider;
    
    static EncryptionService()
    {
        InitializeRSA();
    }

    private static void InitializeRSA()
    {
        try
        {
            _rsaProvider = RSA.Create(2048); // 2048-bit key
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RSA initialization failed: {ex.Message}");
        }
    }

    public static string EncryptAES(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data))
                return string.Empty;

            using var aes = Aes.Create();
            aes.Key = AES_KEY;
            aes.IV = AES_IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encryptedBytes = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
            
            // Prepend a marker to identify this as AES encrypted data
            string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            return $"AES:{encryptedBase64}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AES encryption failed: {ex.Message}");
            return $"[AES_ERROR]{data}"; // Fallback to original data with error marker
        }
    }

    public static string EncryptRSA(string data)
    {
        try
        {
            if (string.IsNullOrEmpty(data) || _rsaProvider == null)
                return string.IsNullOrEmpty(data) ? string.Empty : $"[RSA_ERROR]{data}";

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            
            // RSA can only encrypt data smaller than key size minus padding
            // For 2048-bit RSA with OAEP padding, max is ~190 bytes
            const int maxRSABytes = 190;
            
            if (dataBytes.Length > maxRSABytes)
            {
                // For larger data, use hybrid encryption (RSA + AES)
                return EncryptHybrid(data);
            }

            byte[] encryptedBytes = _rsaProvider.Encrypt(dataBytes, RSAEncryptionPadding.OaepSHA256);
            string encryptedBase64 = Convert.ToBase64String(encryptedBytes);
            return $"RSA:{encryptedBase64}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RSA encryption failed: {ex.Message}");
            return $"[RSA_ERROR]{data}"; // Fallback to original data with error marker
        }
    }

    private static string EncryptHybrid(string data)
    {
        try
        {
            if (_rsaProvider == null)
                return $"[HYBRID_ERROR]{data}";

            // Generate random AES key for this encryption
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();

            // Encrypt data with AES
            using var encryptor = aes.CreateEncryptor();
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] encryptedData = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);

            // Encrypt AES key with RSA
            byte[] keyAndIV = new byte[aes.Key.Length + aes.IV.Length];
            Array.Copy(aes.Key, 0, keyAndIV, 0, aes.Key.Length);
            Array.Copy(aes.IV, 0, keyAndIV, aes.Key.Length, aes.IV.Length);
            
            byte[] encryptedKey = _rsaProvider.Encrypt(keyAndIV, RSAEncryptionPadding.OaepSHA256);

            // Combine encrypted key and encrypted data
            string encryptedKeyBase64 = Convert.ToBase64String(encryptedKey);
            string encryptedDataBase64 = Convert.ToBase64String(encryptedData);
            
            return $"HYBRID:{encryptedKeyBase64}|{encryptedDataBase64}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hybrid encryption failed: {ex.Message}");
            return $"[HYBRID_ERROR]{data}";
        }
    }

    public static string DecryptAES(string encryptedData)
    {
        try
        {
            if (!encryptedData.StartsWith("AES:"))
                return encryptedData; // Not AES encrypted

            string base64Data = encryptedData.Substring(4); // Remove "AES:" prefix
            byte[] encryptedBytes = Convert.FromBase64String(base64Data);

            using var aes = Aes.Create();
            aes.Key = AES_KEY;
            aes.IV = AES_IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AES decryption failed: {ex.Message}");
            return $"[DECRYPT_ERROR]{encryptedData}";
        }
    }

    public static string DecryptRSA(string encryptedData)
    {
        try
        {
            if (_rsaProvider == null)
                return $"[RSA_UNAVAILABLE]{encryptedData}";

            if (encryptedData.StartsWith("RSA:"))
            {
                string base64Data = encryptedData.Substring(4); // Remove "RSA:" prefix
                byte[] encryptedBytes = Convert.FromBase64String(base64Data);
                byte[] decryptedBytes = _rsaProvider.Decrypt(encryptedBytes, RSAEncryptionPadding.OaepSHA256);
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            else if (encryptedData.StartsWith("HYBRID:"))
            {
                return DecryptHybrid(encryptedData);
            }
            
            return encryptedData; // Not RSA encrypted
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RSA decryption failed: {ex.Message}");
            return $"[DECRYPT_ERROR]{encryptedData}";
        }
    }

    private static string DecryptHybrid(string encryptedData)
    {
        try
        {
            if (_rsaProvider == null)
                return $"[HYBRID_UNAVAILABLE]{encryptedData}";

            string hybridData = encryptedData.Substring(7); // Remove "HYBRID:" prefix
            string[] parts = hybridData.Split('|');
            
            if (parts.Length != 2)
                return $"[HYBRID_FORMAT_ERROR]{encryptedData}";

            byte[] encryptedKey = Convert.FromBase64String(parts[0]);
            byte[] encryptedDataBytes = Convert.FromBase64String(parts[1]);

            // Decrypt AES key with RSA
            byte[] keyAndIV = _rsaProvider.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
            
            // Extract key and IV
            byte[] key = new byte[32]; // AES-256 key
            byte[] iv = new byte[16];  // AES IV
            Array.Copy(keyAndIV, 0, key, 0, Math.Min(32, keyAndIV.Length - 16));
            Array.Copy(keyAndIV, keyAndIV.Length - 16, iv, 0, 16);

            // Decrypt data with AES
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedDataBytes, 0, encryptedDataBytes.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Hybrid decryption failed: {ex.Message}");
            return $"[HYBRID_DECRYPT_ERROR]{encryptedData}";
        }
    }

    public static string Decrypt(string encryptedData)
    {
        if (string.IsNullOrEmpty(encryptedData))
            return string.Empty;

        if (encryptedData.StartsWith("AES:"))
            return DecryptAES(encryptedData);
        else if (encryptedData.StartsWith("RSA:") || encryptedData.StartsWith("HYBRID:"))
            return DecryptRSA(encryptedData);
        
        return encryptedData; // Not encrypted
    }

    // Method to get encryption info for debugging
    public static string GetEncryptionInfo()
    {
        var info = new StringBuilder();
        info.AppendLine($"AES Key Length: {AES_KEY.Length} bytes");
        info.AppendLine($"AES IV Length: {AES_IV.Length} bytes");
        info.AppendLine($"RSA Available: {_rsaProvider != null}");
        
        if (_rsaProvider != null)
        {
            info.AppendLine($"RSA Key Size: {_rsaProvider.KeySize} bits");
        }
        
        return info.ToString();
    }
}