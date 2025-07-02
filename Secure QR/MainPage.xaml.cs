namespace Secure_QR;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        BindingContext = viewModel;
        
        // Run encryption tests on startup (only in debug mode)
#if DEBUG
        Task.Run(() => EncryptionTests.RunAllTests());
        
        // Test QR generation after a short delay
        Task.Delay(1000).ContinueWith(_ => 
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Set some test data
                viewModel.DataInput = "Hello, Secure QR!";
                viewModel.IsEncryptionEnabled = true;
                viewModel.UseAESEncryption = true;
                
                // Test QR generation
                try
                {
                    viewModel.GenerateQRCodesCommand.Execute(null);
                    System.Diagnostics.Debug.WriteLine("Test QR generation completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Test QR generation failed: {ex.Message}");
                }
            });
        });
#endif
    }
}