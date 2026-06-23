namespace RPSystem
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            System.Diagnostics.Debug.WriteLine("[AppShell] ===== APPSHELL CONSTRUCTOR =====");
            System.Diagnostics.Debug.WriteLine("[AppShell] Registering navigation routes...");

            // Register routes for pages kept in the standalone RP app
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(RpSystemPage), typeof(RpSystemPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));

            System.Diagnostics.Debug.WriteLine("[AppShell] Routes registered successfully");
            System.Diagnostics.Debug.WriteLine("[AppShell] ===== APPSHELL CONSTRUCTOR END =====");
        }
    }
}
