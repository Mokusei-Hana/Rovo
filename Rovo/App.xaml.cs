using System.Windows;
using Rovo.Services;
using Rovo.ViewModels;
using Rovo.Views;

namespace Rovo;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = new MainWindow(new MainViewModel(new RobocopyService()));
        MainWindow.Show();
    }
}
