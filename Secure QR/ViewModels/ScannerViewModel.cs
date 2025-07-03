using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Secure_QR;

public class ScanHistoryItem
{
    public DateTime Timestamp { get; set; }
    public string Data { get; set; } = string.Empty;
    public string EncryptionType { get; set; } = "None";
    public bool IsEncrypted { get; set; }
}

public class ScannerViewModel : INotifyPropertyChanged
{
    private bool _isScanning = true;
    private bool _isFlashOn = false;
    private string _scanStatusMessage = "Ready to scan QR codes...";
    private string _scannedRawData = string.Empty;
    private string _decryptedData = string.Empty;
    private string _encryptionDetected = "None";
    private string _decryptionStatus = string.Empty;
    private bool _hasScannedData = false;
    private bool _isDataDecrypted = false;
    private bool _showDecryptionStatus = false;
    private bool _canDecrypt = false;
    private DateTime _lastScanTime = DateTime.MinValue;

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            _isScanning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScanButtonText));
            OnPropertyChanged(nameof(ScanButtonColor));
        }
    }

    public bool IsFlashOn
    {
        get => _isFlashOn;
        set
        {
            _isFlashOn = value;
            OnPropertyChanged();
        }
    }

    public string ScanStatusMessage
    {
        get => _scanStatusMessage;
        set
        {
            _scanStatusMessage = value;
            OnPropertyChanged();
        }
    }

    public string ScanButtonText => IsScanning ? "Pause" : "Scan";
    public string ScanButtonColor => IsScanning ? "Orange" : "Green";

    public string ScannedRawData
    {
        get => _scannedRawData;
        set
        {
            _scannedRawData = value;
            OnPropertyChanged();
        }
    }

    public string DecryptedData
    {
        get => _decryptedData;
        set
        {
            _decryptedData = value;
            OnPropertyChanged();
        }
    }

    public string EncryptionDetected
    {
        get => _encryptionDetected;
        set
        {
            _encryptionDetected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EncryptionStatusColor));
        }
    }

    public string EncryptionStatusColor => EncryptionDetected == "None" ? "Gray" : "Green";

    public string DecryptionStatus
    {
        get => _decryptionStatus;
        set
        {
            _decryptionStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DecryptionStatusColor));
        }
    }

    public string DecryptionStatusColor => DecryptionStatus.Contains("Success") ? "Green" : "Red";

    public bool HasScannedData
    {
        get => _hasScannedData;
        set
        {
            _hasScannedData = value;
            OnPropertyChanged();
        }
    }

    public bool IsDataDecrypted
    {
        get => _isDataDecrypted;
        set
        {
            _isDataDecrypted = value;
            OnPropertyChanged();
        }
    }

    public bool ShowDecryptionStatus
    {
        get => _showDecryptionStatus;
        set
        {
            _showDecryptionStatus = value;
            OnPropertyChanged();
        }
    }

    public bool CanDecrypt
    {
        get => _canDecrypt;
        set
        {
            _canDecrypt = value;
            OnPropertyChanged();
        }
    }

    public bool IsDesktop => DeviceInfo.Platform == DevicePlatform.WinUI || 
                           DeviceInfo.Platform == DevicePlatform.MacCatalyst;

    public bool IsMobile => !IsDesktop;

    public ObservableCollection<ScanHistoryItem> ScanHistory { get; } = new();
    public bool HasScanHistory => ScanHistory.Count > 0;

    // Commands
    public ICommand ToggleScanningCommand { get; }
    public ICommand ToggleFlashCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand CopyToClipboardCommand { get; }
    public ICommand DecryptDataCommand { get; }
    public ICommand ClearResultsCommand { get; }
    public ICommand TestScanCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ScannerViewModel()
    {
        ToggleScanningCommand = new Command(ToggleScanning);
        ToggleFlashCommand = new Command(ToggleFlash);
        BackCommand = new Command(async () => await GoBack());
        CopyToClipboardCommand = new Command(async () => await CopyToClipboard());
        DecryptDataCommand = new Command(DecryptData);
        ClearResultsCommand = new Command(ClearResults);
        TestScanCommand = new Command(PerformTestScan);
    }

    public async Task<bool> RequestCameraPermissionAsync()
    {
        try
        {
            // On desktop, we don't need real camera permissions
            if (IsDesktop)
            {
                return true;
            }

            var status = await Permissions.RequestAsync<Permissions.Camera>();
            
            if (status != PermissionStatus.Granted)
            {
                ScanStatusMessage = "Camera permission required for QR scanning";
                IsScanning = false;
                return false;
            }
            
            ScanStatusMessage = "Camera ready - scanning for QR codes...";
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Permission request failed: {ex.Message}");
            ScanStatusMessage = $"Permission error: {ex.Message}";
            return false;
        }
    }

    public void StartScanning()
    {
        IsScanning = true;
        ScanStatusMessage = "Scanning active - point camera at QR code";
    }

    public void StopScanning()
    {
        IsScanning = false;
        ScanStatusMessage = "Scanning paused";
    }

    public void ProcessScannedBarcode(string barcodeValue, string format)
    {
        try
        {
            // Prevent duplicate scans too quickly
            if (DateTime.Now - _lastScanTime < TimeSpan.FromSeconds(2) && 
                barcodeValue == ScannedRawData)
            {
                return;
            }
            
            _lastScanTime = DateTime.Now;
            
            // Update raw data
            ScannedRawData = barcodeValue;
            HasScannedData = true;
            
            // Detect encryption type
            string encryptionType = DetectEncryptionType(barcodeValue);
            EncryptionDetected = encryptionType;
            CanDecrypt = encryptionType != "None";
            
            // Auto-decrypt if encrypted
            if (CanDecrypt)
            {
                DecryptData();
            }
            else
            {
                DecryptedData = barcodeValue;
                IsDataDecrypted = true;
                DecryptionStatus = "No encryption detected - displaying raw data";
                ShowDecryptionStatus = true;
            }
            
            // Add to history
            AddToScanHistory(barcodeValue, encryptionType, CanDecrypt);
            
            // Update status
            ScanStatusMessage = $"QR Code detected! Type: {format}, Encryption: {encryptionType}";
            
            // Briefly pause scanning to prevent multiple rapid scans
            _ = Task.Delay(1500).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (IsScanning)
                {
                    ScanStatusMessage = "Ready for next scan...";
                }
            }));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing barcode: {ex.Message}");
            ScanStatusMessage = $"Error processing QR code: {ex.Message}";
        }
    }

    private void PerformTestScan()
    {
        // Test with different encrypted data
        var testData = new[]
        {
            "AES:U2FsdGVkX19zZWNyZXRfdGVzdF9kYXRhX2hlcmU=",
            "RSA:lG2J2xYXJ5J3XK1...", // shortened for example
            "Hello unencrypted world!",
            "HYBRID:encryptedKey|encryptedData"
        };
        
        var randomData = testData[new Random().Next(testData.Length)];
        ProcessScannedBarcode(randomData, "QR_CODE");
    }

    private string DetectEncryptionType(string data)
    {
        if (string.IsNullOrEmpty(data))
            return "None";
            
        if (data.StartsWith("AES:"))
            return "AES";
        else if (data.StartsWith("RSA:"))
            return "RSA";
        else if (data.StartsWith("HYBRID:"))
            return "RSA Hybrid";
        else
            return "None";
    }

    private void AddToScanHistory(string data, string encryptionType, bool isEncrypted)
    {
        var historyItem = new ScanHistoryItem
        {
            Timestamp = DateTime.Now,
            Data = data.Length > 50 ? data.Substring(0, 50) + "..." : data,
            EncryptionType = encryptionType,
            IsEncrypted = isEncrypted
        };
        
        // Add to beginning of list
        ScanHistory.Insert(0, historyItem);
        
        // Keep only last 10 items
        while (ScanHistory.Count > 10)
        {
            ScanHistory.RemoveAt(ScanHistory.Count - 1);
        }
        
        OnPropertyChanged(nameof(HasScanHistory));
    }

    private void ToggleScanning()
    {
        IsScanning = !IsScanning;
        ScanStatusMessage = IsScanning ? "Scanning resumed" : "Scanning paused";
    }

    private void ToggleFlash()
    {
        IsFlashOn = !IsFlashOn;
        ScanStatusMessage = IsFlashOn ? "Flash enabled" : "Flash disabled";
    }

    private async Task GoBack()
    {
        try
        {
            StopScanning();
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }

    private async Task CopyToClipboard()
    {
        try
        {
            string dataToCopy = IsDataDecrypted ? DecryptedData : ScannedRawData;
            await Clipboard.SetTextAsync(dataToCopy);
            ScanStatusMessage = "Data copied to clipboard!";
            
            // Reset message after delay
            await Task.Delay(2000);
            if (ScanStatusMessage == "Data copied to clipboard!")
            {
                ScanStatusMessage = "Ready to scan...";
            }
        }
        catch (Exception ex)
        {
            ScanStatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private void DecryptData()
    {
        try
        {
            if (string.IsNullOrEmpty(ScannedRawData))
            {
                DecryptionStatus = "No data to decrypt";
                ShowDecryptionStatus = true;
                return;
            }

            string decrypted = EncryptionService.Decrypt(ScannedRawData);
            
            if (decrypted.Contains("ERROR"))
            {
                DecryptionStatus = $"Decryption failed: {decrypted}";
                DecryptedData = "Decryption failed - see status";
                IsDataDecrypted = false;
            }
            else
            {
                DecryptedData = decrypted;
                IsDataDecrypted = true;
                
                // Check if decryption actually changed the data
                if (decrypted == ScannedRawData)
                {
                    DecryptionStatus = "Data was not encrypted";
                }
                else
                {
                    DecryptionStatus = $"Decryption successful using {EncryptionDetected}";
                }
            }
            
            ShowDecryptionStatus = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Decryption error: {ex.Message}");
            DecryptionStatus = $"Decryption error: {ex.Message}";
            ShowDecryptionStatus = true;
            IsDataDecrypted = false;
        }
    }

    private void ClearResults()
    {
        ScannedRawData = string.Empty;
        DecryptedData = string.Empty;
        EncryptionDetected = "None";
        DecryptionStatus = string.Empty;
        HasScannedData = false;
        IsDataDecrypted = false;
        ShowDecryptionStatus = false;
        CanDecrypt = false;
        
        ScanStatusMessage = "Results cleared - ready to scan";
    }

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}