namespace Secure_QR;

public static class EncryptionService
{
    public static string EncryptAES(string data)
    {
        // Placeholder - you’ll plug in real AES later
        return "[AES]" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }

    public static string EncryptRSA(string data)
    {
        // Placeholder - simulate RSA for now
        return "[RSA]" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data));
    }
}
