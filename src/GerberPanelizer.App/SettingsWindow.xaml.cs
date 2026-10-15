using System.Windows;

namespace GerberPanelizer.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settings, this);
    }
}
