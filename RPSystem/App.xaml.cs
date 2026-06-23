// App.xaml.cs
namespace ChemCalculationAndManagementApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // This is the important change
            MainPage = new AppShell();
        }
    }
}