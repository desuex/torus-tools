using Avalonia.Controls;
using Avalonia.Interactivity;
using TorusTool.ViewModels;

namespace TorusTool.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenFileButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.OpenFile(StorageProvider);
        }
    }
}