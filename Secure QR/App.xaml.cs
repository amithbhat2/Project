namespace Secure_QR;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        
        // Set the main page using dependency injection
        MainPage = new AppShell();
    }
}