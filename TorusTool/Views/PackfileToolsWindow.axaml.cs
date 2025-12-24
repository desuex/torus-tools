using Avalonia.Controls;
using TorusTool.ViewModels;

namespace TorusTool.Views;

public partial class PackfileToolsWindow : Window
{
    public PackfileToolsWindow()
    {
        InitializeComponent();
        DataContext = new PackfileToolsViewModel();
    }
}
