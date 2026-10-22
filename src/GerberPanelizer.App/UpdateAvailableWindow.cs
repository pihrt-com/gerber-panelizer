using System.Windows;
using System.Windows.Controls;

namespace GerberPanelizer.App;

internal enum UpdateAction
{
    Cancel,
    Download,
    DownloadAndInstall,
    OpenReleasePage,
}

/// <summary>Lets the user either save the published installer or start it after a GitHub download.</summary>
internal sealed class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow(Version version, bool hasSetupInstaller)
    {
        Title = LocalizationService.Instance["UpdateCheck"];
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = System.Windows.Media.Brushes.White;

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = string.Format(LocalizationService.Instance["UpdateAvailable"], version), TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0, 0, 0, 18) });
        if (!hasSetupInstaller)
        {
            panel.Children.Add(new TextBlock { Text = LocalizationService.Instance["UpdateInstallerMissing"], TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.DarkGoldenrod, Margin = new Thickness(0, 0, 0, 18) });
        }

        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        if (hasSetupInstaller)
        {
            buttons.Children.Add(CreateButton("UpdateDownload", UpdateAction.Download));
            buttons.Children.Add(CreateButton("UpdateDownloadAndInstall", UpdateAction.DownloadAndInstall));
        }
        else
        {
            buttons.Children.Add(CreateButton("UpdateOpenReleasePage", UpdateAction.OpenReleasePage));
        }
        buttons.Children.Add(CreateButton("Cancel", UpdateAction.Cancel, isCancel: true));
        panel.Children.Add(buttons);
        Content = panel;
    }

    public UpdateAction Action { get; private set; } = UpdateAction.Cancel;

    private Button CreateButton(string resourceKey, UpdateAction action, bool isCancel = false)
    {
        var button = new Button { Content = LocalizationService.Instance[resourceKey], Margin = new Thickness(8, 0, 0, 0), IsCancel = isCancel, MinWidth = 100 };
        button.Click += (_, _) =>
        {
            Action = action;
            DialogResult = action != UpdateAction.Cancel;
        };
        return button;
    }
}
