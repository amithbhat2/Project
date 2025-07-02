#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;
using QRCoder;

namespace Secure_QR;

public class QRCodeData
{
    public string Title { get; set; } = string.Empty;
    public string OriginalData { get; set; } = string.Empty;
    public string EncryptedData { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = false;
    public string EncryptionType { get; set; } = string.Empty;
    public ImageSource ImageSource { get; set; } = ImageSource.FromFile("placeholder.png");
}

public class MainViewModel : INotifyPropertyChanged
{
    public string DataInput { get; set; } = string.Empty;

    public bool UseAESEncryption { get; set; } = true;
    public bool UseRSAEncryption { get; set; } = false;
    public bool IsEncryptionEnabled { get; set; } = true;

    public ObservableCollection<QRCodeData> QRCodeImages { get; set; } = new();

    public ICommand GenerateQRCodesCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand ShareQRCommand { get; }
    public ICommand DecryptQRCommand { get; }

    public string StatusMessage { get; set; } = string.Empty;
    public double GenerationTime { get; set; }
    public bool ShowDebugInfo { get; set; } = true; // Enable debug info to show encryption details
    public string DebugInfo { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        GenerateQRCodesCommand = new Command(GenerateQRCodes);
        ClearAllCommand = new Command(ClearAll);
        ShareQRCommand = new Command<QRCodeData>(ShareQR);
        DecryptQRCommand = new Command<QRCodeData>(DecryptQR);
        
        // Initialize debug info with encryption details
        UpdateDebugInfo();
    }

    void GenerateQRCodes()
    {
        QRCodeImages.Clear();
        
        if (string.IsNullOrWhiteSpace(DataInput))
        {
            StatusMessage = "Please enter some data to generate QR code.";
            OnPropertyChanged(nameof(StatusMessage));
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            string originalData = DataInput;

            if (IsEncryptionEnabled)
            {
                // Generate 3 QR codes when encryption is enabled
                GenerateEncryptedQRCodes(originalData);
            }
            else
            {
                // Generate single QR code when encryption is disabled
                GenerateSingleQRCode(originalData, false, "None");
            }

            stopwatch.Stop();
            GenerationTime = stopwatch.Elapsed.TotalSeconds;
            
            int qrCount = QRCodeImages.Count;
            string encryptionMode = IsEncryptionEnabled ? (UseAESEncryption ? "AES" : "RSA") : "None";
            StatusMessage = $"Successfully generated {qrCount} QR code{(qrCount > 1 ? "s" : "")} with {encryptionMode} encryption!";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            StatusMessage = $"Generation failed: {ex.Message}";
            Debug.WriteLine($"QR Generation error: {ex}");
        }
        
        UpdateDebugInfo();
        OnPropertyChanged(nameof(QRCodeImages));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(GenerationTime));
        OnPropertyChanged(nameof(DebugInfo));
    }

    void GenerateEncryptedQRCodes(string originalData)
    {
        string encryptionType = UseAESEncryption ? "AES" : "RSA";
        
        // Generate 3 different encrypted versions of the same data
        for (int i = 1; i <= 3; i++)
        {
            try
            {
                // Create slightly different data for each QR code to make them unique
                string dataVariation = $"{originalData} (Copy {i})";
                string encryptedData = Encrypt(dataVariation);
                
                // Validate encryption worked
                if (encryptedData.Contains("ERROR"))
                {
                    StatusMessage = $"Encryption failed for QR {i} - check debug info for details";
                    OnPropertyChanged(nameof(StatusMessage));
                    continue;
                }

                GenerateSingleQRCode(dataVariation, true, encryptionType, encryptedData, i);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error generating QR code {i}: {ex.Message}");
                StatusMessage = $"Failed to generate QR code {i}: {ex.Message}";
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    void GenerateSingleQRCode(string originalData, bool isEncrypted, string encryptionType, string? encryptedData = null, int qrNumber = 1)
    {
        try
        {
            string contentForQR = encryptedData ?? originalData;
            
            // Use QRCoder instead of ZXing for better image generation
            using var qrGenerator = new QRCoder.QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(contentForQR, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new QRCoder.PngByteQRCode(qrCodeData);
            byte[] qrCodeBytes = qrCode.GetGraphic(20);

            string title = isEncrypted ? $"QR Code {qrNumber} ({encryptionType})" : $"QR Code ({encryptionType})";

            QRCodeImages.Add(new QRCodeData
            {
                Title = title,
                OriginalData = originalData,
                EncryptedData = contentForQR,
                IsEncrypted = isEncrypted,
                EncryptionType = encryptionType,
                ImageSource = ImageSource.FromStream(() => new MemoryStream(qrCodeBytes))
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating QR code: {ex.Message}");
            throw;
        }
    }

    string Encrypt(string input)
    {
        try
        {
            if (UseAESEncryption)
                return EncryptionService.EncryptAES(input);
            if (UseRSAEncryption)
                return EncryptionService.EncryptRSA(input);
            return input;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Encryption error: {ex.Message}");
            return $"[ENCRYPT_ERROR]{input}";
        }
    }

    void ShareQR(QRCodeData? qrData)
    {
        if (qrData == null) return;
        
        try
        {
            // In a real app, you'd use the platform's sharing API
            // For now, just show info
            StatusMessage = $"Sharing: {qrData.Title} - Original: {qrData.OriginalData.Substring(0, Math.Min(20, qrData.OriginalData.Length))}...";
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Share failed: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    void DecryptQR(QRCodeData? qrData)
    {
        if (qrData == null) return;

        try
        {
            if (!qrData.IsEncrypted)
            {
                StatusMessage = $"QR Code is not encrypted. Original data: {qrData.OriginalData}";
            }
            else
            {
                string decryptedData = EncryptionService.Decrypt(qrData.EncryptedData);
                
                if (decryptedData.Contains("ERROR"))
                {
                    StatusMessage = $"Decryption failed: {decryptedData}";
                }
                else
                {
                    bool matches = decryptedData == qrData.OriginalData;
                    StatusMessage = $"Decrypted ({qrData.EncryptionType}): {decryptedData} - Match: {matches}";
                }
            }
            
            OnPropertyChanged(nameof(StatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Decrypt error: {ex.Message}";
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    void ClearAll()
    {
        DataInput = string.Empty;
        QRCodeImages.Clear();
        StatusMessage = "Cleared all data and QR codes.";
        UpdateDebugInfo();
        
        OnPropertyChanged(nameof(DataInput));
        OnPropertyChanged(nameof(QRCodeImages));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(DebugInfo));
    }

    void UpdateDebugInfo()
    {
        var info = new System.Text.StringBuilder();
        info.AppendLine($"Encryption Enabled: {IsEncryptionEnabled}");
        info.AppendLine($"Encryption Mode: {(UseAESEncryption ? "AES" : "RSA")}");
        info.AppendLine($"Generated QR Codes: {QRCodeImages.Count}");
        info.AppendLine($"Last Generation Time: {GenerationTime:F3}s");
        info.AppendLine();
        info.AppendLine("Encryption Service Info:");
        info.Append(EncryptionService.GetEncryptionInfo());
        
        if (QRCodeImages.Any())
        {
            info.AppendLine();
            info.AppendLine("QR Code Details:");
            foreach (var qr in QRCodeImages)
            {
                info.AppendLine($"- {qr.Title}: {qr.EncryptionType}, Encrypted: {qr.IsEncrypted}");
                if (qr.IsEncrypted)
                {
                    info.AppendLine($"  Encrypted Length: {qr.EncryptedData.Length} chars");
                    info.AppendLine($"  Original Length: {qr.OriginalData.Length} chars");
                }
            }
        }
        
        DebugInfo = info.ToString();
    }

    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}