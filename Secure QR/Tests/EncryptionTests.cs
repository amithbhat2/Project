using System.Diagnostics;

namespace Secure_QR;

public static class EncryptionTests
{
    public static void RunAllTests()
    {
        Debug.WriteLine("=== Starting Encryption Tests ===");
        
        TestAESEncryption();
        TestRSAEncryption();
        TestHybridEncryption();
        TestEdgeCases();
        
        Debug.WriteLine("=== Encryption Tests Completed ===");
    }

    private static void TestAESEncryption()
    {
        Debug.WriteLine("\n--- AES Encryption Test ---");
        
        var testCases = new[]
        {
            "Hello World!",
            "This is a longer test string with special characters: !@#$%^&*()",
            "短い日本語テスト", // Short Japanese text
            "12345678901234567890", // Numbers
            "" // Empty string
        };

        foreach (string testData in testCases)
        {
            try
            {
                string encrypted = EncryptionService.EncryptAES(testData);
                string decrypted = EncryptionService.DecryptAES(encrypted);
                
                bool success = decrypted == testData;
                Debug.WriteLine($"AES Test - Input: '{testData}' | Success: {success}");
                
                if (!success)
                {
                    Debug.WriteLine($"  Expected: '{testData}'");
                    Debug.WriteLine($"  Got: '{decrypted}'");
                }
                else
                {
                    Debug.WriteLine($"  Encrypted: {encrypted.Substring(0, Math.Min(50, encrypted.Length))}...");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AES Test FAILED - Input: '{testData}' | Error: {ex.Message}");
            }
        }
    }

    private static void TestRSAEncryption()
    {
        Debug.WriteLine("\n--- RSA Encryption Test ---");
        
        var testCases = new[]
        {
            "Short text",
            "Medium length text that should still work with RSA direct encryption",
            "This is a much longer text that will require hybrid encryption because RSA can only handle limited data sizes directly and this text exceeds that limit significantly"
        };

        foreach (string testData in testCases)
        {
            try
            {
                string encrypted = EncryptionService.EncryptRSA(testData);
                string decrypted = EncryptionService.DecryptRSA(encrypted);
                
                bool success = decrypted == testData;
                Debug.WriteLine($"RSA Test - Input length: {testData.Length} chars | Success: {success}");
                
                if (!success)
                {
                    Debug.WriteLine($"  Expected: '{testData.Substring(0, Math.Min(30, testData.Length))}...'");
                    Debug.WriteLine($"  Got: '{decrypted.Substring(0, Math.Min(30, decrypted.Length))}...'");
                }
                else
                {
                    string encType = encrypted.StartsWith("HYBRID:") ? "Hybrid" : "Direct RSA";
                    Debug.WriteLine($"  Encryption type: {encType}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RSA Test FAILED - Input length: {testData.Length} | Error: {ex.Message}");
            }
        }
    }

    private static void TestHybridEncryption()
    {
        Debug.WriteLine("\n--- Hybrid Encryption Test ---");
        
        string longText = string.Join(" ", Enumerable.Repeat("This is a test of hybrid encryption with a very long string.", 10));
        
        try
        {
            string encrypted = EncryptionService.EncryptRSA(longText);
            string decrypted = EncryptionService.DecryptRSA(encrypted);
            
            bool success = decrypted == longText;
            bool isHybrid = encrypted.StartsWith("HYBRID:");
            
            Debug.WriteLine($"Hybrid Test - Input: {longText.Length} chars | Hybrid: {isHybrid} | Success: {success}");
            
            if (!success)
            {
                Debug.WriteLine($"  Decryption failed for long text");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Hybrid Test FAILED - Error: {ex.Message}");
        }
    }

    private static void TestEdgeCases()
    {
        Debug.WriteLine("\n--- Edge Cases Test ---");
        
        // Test null and empty
        try
        {
            string emptyAES = EncryptionService.EncryptAES("");
            string emptyRSA = EncryptionService.EncryptRSA("");
            Debug.WriteLine($"Empty string - AES: '{emptyAES}' | RSA: '{emptyRSA}'");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Empty string test failed: {ex.Message}");
        }

        // Test unicode
        try
        {
            string unicode = "🚀 Unicode test with emoji and special chars: αβγδε";
            string encryptedAES = EncryptionService.EncryptAES(unicode);
            string decryptedAES = EncryptionService.DecryptAES(encryptedAES);
            
            bool unicodeSuccess = decryptedAES == unicode;
            Debug.WriteLine($"Unicode test - Success: {unicodeSuccess}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Unicode test failed: {ex.Message}");
        }

        // Test decrypt non-encrypted data
        try
        {
            string plainText = "This is not encrypted";
            string result = EncryptionService.Decrypt(plainText);
            bool correctPassthrough = result == plainText;
            Debug.WriteLine($"Non-encrypted passthrough - Success: {correctPassthrough}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Non-encrypted passthrough test failed: {ex.Message}");
        }
    }

    public static string GetTestSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("Encryption Test Summary:");
        summary.AppendLine("- AES: 128-bit CBC mode with PKCS7 padding");
        summary.AppendLine("- RSA: 2048-bit with OAEP SHA-256 padding");
        summary.AppendLine("- Hybrid: RSA + AES for large data");
        summary.AppendLine("- Supports Unicode and special characters");
        summary.AppendLine("- Graceful error handling with fallbacks");
        
        return summary.ToString();
    }
}