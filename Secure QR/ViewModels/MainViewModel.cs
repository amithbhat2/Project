#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Runtime.InteropServices;
using ZXing;
using ZXing.Common;

namespace Secure_QR;

public class QRCodeData
{
    public string Title { get; set; } = string.Empty;
    public string OriginalData { get; set; } = string.Empty;
    public ImageSource ImageSource { get; set; } = ImageSource.FromFile("placeholder.png");
}

public class MainViewModel : INotifyPropertyChanged
{
    public string DataInput1 { get; set; } = string.Empty;
    public string DataInput2 { get; set; } = string.Empty;
    public string DataInput3 { get; set; } = string.Empty;

    public bool UseAESEncryption { get; set; } = true;
    public bool UseRSAEncryption { get; set; } = false;
    public bool IsEncryptionEnabled { get; set; } = true;

    public ObservableCollection<QRCodeData> QRCodeImages { get; set; } = new();

    public ICommand GenerateQRCodesCommand { get; }
    public ICommand ClearAllCommand { get; }

    public string StatusMessage { get; set; } = string.Empty;
    public double GenerationTime { get; set; }
    public bool ShowDebugInfo { get; set; } = false;
    public string DebugInfo { get; set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        GenerateQRCodesCommand = new Command(GenerateQRCodes);
        ClearAllCommand = new Command(ClearAll);
    }

    void GenerateQRCodes()
    {
        QRCodeImages.Clear();
        var inputs = new[] { DataInput1, DataInput2, DataInput3 };
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < inputs.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(inputs[i])) continue;

            string content = IsEncryptionEnabled ? Encrypt(inputs[i]) : inputs[i];

            var writer = new BarcodeWriterPixelData
            {
                Format = ZXing.BarcodeFormat.QR_CODE,
                Options = new EncodingOptions
                {
                    Width = 300,
                    Height = 300,
                    Margin = 1
                }
            };

            var pixelData = writer.Write(content);

            var stream = new MemoryStream(pixelData.Pixels);
            stream.Position = 0;

            var safeStream = new MemoryStream(pixelData.Pixels.ToArray()); // ensure memory safety

            QRCodeImages.Add(new QRCodeData
            {
                Title = $"QR Code {i + 1}",
                OriginalData = inputs[i],
                ImageSource = ImageSource.FromStream(() => safeStream)
            });
        }

        stopwatch.Stop();
        GenerationTime = stopwatch.Elapsed.TotalSeconds;
        StatusMessage = "QR Codes generated successfully!";
        OnPropertyChanged(nameof(QRCodeImages));
        OnPropertyChanged(nameof(StatusMessage));
        OnPropertyChanged(nameof(GenerationTime));
    }

    string Encrypt(string input)
    {
        if (UseAESEncryption)
            return EncryptionService.EncryptAES(input);
        if (UseRSAEncryption)
            return EncryptionService.EncryptRSA(input);
        return input;
    }

    void ClearAll()
    {
        DataInput1 = DataInput2 = DataInput3 = string.Empty;
        QRCodeImages.Clear();
        StatusMessage = "Cleared.";
        OnPropertyChanged(nameof(DataInput1));
        OnPropertyChanged(nameof(DataInput2));
        OnPropertyChanged(nameof(DataInput3));
        OnPropertyChanged(nameof(QRCodeImages));
        OnPropertyChanged(nameof(StatusMessage));
    }

    void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
