using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using Rovo.ViewModels;

namespace Rovo.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;
    private readonly DispatcherTimer outputTimer;
    private bool waitingToClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        outputTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background,
            (_, _) => viewModel.DrainOutput(), Dispatcher);
        Closed += (_, _) => outputTimer.Stop();
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e)
    {
        var selected = ChooseFolder("Choose source folder", viewModel.Source);
        if (selected is not null) viewModel.Source = selected;
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var selected = ChooseFolder("Choose destination folder", viewModel.Destination);
        if (selected is not null) viewModel.Destination = selected;
    }

    private string? ChooseFolder(string title, string current)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (System.IO.Directory.Exists(current)) dialog.InitialDirectory = current;
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!viewModel.IsRunning) return;
        e.Cancel = true;
        if (waitingToClose) return;

        var answer = MessageBox.Show(this,
            "A copy is running. Cancel the copy and close Rovo?\n\nFiles already copied will remain; an interrupted file may be incomplete.\nChoose No to keep copying.",
            "Copy in progress", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        waitingToClose = true;
        viewModel.CancelCommand.Execute(null);
        if (viewModel.StartCommand.ExecutionTask is { } runningTask) await runningTask;
        waitingToClose = false;
        Close();
    }

    private void OutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Preserve the user's position when they scroll back to inspect earlier output.
        if (OutputBox.SelectionLength == 0 && OutputBox.VerticalOffset >= OutputBox.ExtentHeight - OutputBox.ViewportHeight - 40)
            OutputBox.ScrollToEnd();
    }
}
