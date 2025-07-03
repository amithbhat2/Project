using ZXing.Net.Maui;

namespace Secure_QR;

public partial class ScannerPage : ContentPage
{
    private ScannerViewModel _viewModel;

    public ScannerPage()
    {
        InitializeComponent();
        _viewModel = new ScannerViewModel();
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Request camera permission when page appears
        var permissionStatus = await _viewModel.RequestCameraPermissionAsync();
        
        if (DeviceInfo.DeviceType == DeviceType.Physical || permissionStatus)
        {
            // Start scanning automatically on real devices
            _viewModel.StartScanning();
        }
        else
        {
            // For desktop/emulator testing
            if (DeviceInfo.Platform == DevicePlatform.WinUI || 
                DeviceInfo.Platform == DevicePlatform.MacCatalyst)
            {
                // Update status message
                _viewModel.ScanStatusMessage = "Desktop mode - use Test Scan button";
                
                // Simulate a scan after delay if in debug mode
                #if DEBUG
                await Task.Delay(2000);
                _viewModel.ProcessScannedBarcode(
                    "AES:U2FsdGVkX19zZWNyZXRfdGVzdF9kYXRhX2hlcmU=", 
                    "QR_CODE");
                #endif
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Stop scanning when leaving page to save battery
        _viewModel.StopScanning();
    }

    void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        // Handle barcode detection
        if (e.Results?.Any() == true)
        {
            var barcode = e.Results.First();
            
            // Process the scanned barcode on the main thread
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _viewModel.ProcessScannedBarcode(barcode.Value, barcode.Format.ToString());
            });
        }
    }

    // Handle hardware back button on Android
    protected override bool OnBackButtonPressed()
    {
        _viewModel.BackCommand.Execute(null);
        return true; // Handled
    }
}