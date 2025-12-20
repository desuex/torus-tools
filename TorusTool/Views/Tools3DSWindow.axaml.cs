using Avalonia.Controls;
using TorusTool.ViewModels;

namespace TorusTool.Views;

public partial class Tools3DSWindow : Window
{
    public Tools3DSWindow()
    {
        InitializeComponent();
        DataContext = new Tools3DSViewModel();
    }
}
