using System.Configuration;
using System.Windows;

namespace GerberPanelizer.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LocalizationService.Instance.SetLanguage(AppSettingsService.Load().Language);
    }
}
