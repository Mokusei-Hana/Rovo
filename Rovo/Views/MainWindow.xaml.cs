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
    private bool choosingFolder;
    private bool followOutput = true;
    private double outputVerticalOffset;
    private double outputHorizontalOffset;
    private int outputSelectionStart;
    private int outputSelectionLength;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        viewModel.PropertyChanging += ViewModel_PropertyChanging;
        outputTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background,
            (_, _) => viewModel.DrainOutput(), Dispatcher);
        Closed += (_, _) =>
        {
            outputTimer.Stop();
            viewModel.PropertyChanging -= ViewModel_PropertyChanging;
        };
    }

    private async void BrowseSource_Click(object sender, RoutedEventArgs e) => await BrowseFolderAsync(isSource: true);

    private async void BrowseDestination_Click(object sender, RoutedEventArgs e) => await BrowseFolderAsync(isSource: false);

    private async Task BrowseFolderAsync(bool isSource)
    {
        if (choosingFolder || !viewModel.IsIdle) return;
        choosingFolder = true;
        try
        {
            var current = isSource ? viewModel.Source : viewModel.Destination;
            var exists = await Task.Run(() => System.IO.Directory.Exists(current));
            // The window may have closed or a copy started while a network path was checked.
            if (!IsLoaded || !viewModel.IsIdle) return;
            var dialog = new OpenFolderDialog
            {
                Title = isSource ? "Choose source folder" : "Choose destination folder",
                Multiselect = false
            };
            if (exists) dialog.InitialDirectory = current;
            if (dialog.ShowDialog(this) != true) return;
            if (isSource) viewModel.Source = dialog.FolderName;
            else viewModel.Destination = dialog.FolderName;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or Win32Exception or ArgumentException)
        {
            if (IsLoaded)
                MessageBox.Show(this, $"Could not open the folder picker: {ex.Message}", "Choose folder", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            choosingFolder = false;
        }
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

    private void ViewModel_PropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.Output)) return;
        // Read the old viewport before WPF replaces the bound text, not in TextChanged.
        followOutput = OutputBox.SelectionLength == 0 &&
            OutputBox.VerticalOffset >= OutputBox.ExtentHeight - OutputBox.ViewportHeight - 1;
        outputVerticalOffset = OutputBox.VerticalOffset;
        outputHorizontalOffset = OutputBox.HorizontalOffset;
        outputSelectionStart = OutputBox.SelectionStart;
        outputSelectionLength = OutputBox.SelectionLength;
    }

    private void OutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var follow = followOutput;
        var vertical = outputVerticalOffset;
        var horizontal = outputHorizontalOffset;
        var start = outputSelectionStart;
        var length = outputSelectionLength;
        // Scroll after layout has updated the extent for the new text.
        Dispatcher.InvokeAsync(() =>
        {
            if (follow)
                OutputBox.ScrollToEnd();
            else
            {
                start = Math.Min(start, OutputBox.Text.Length);
                OutputBox.Select(start, Math.Min(length, OutputBox.Text.Length - start));
                OutputBox.ScrollToVerticalOffset(vertical);
            }
            OutputBox.ScrollToHorizontalOffset(horizontal);
        }, DispatcherPriority.Loaded);
    }
}
