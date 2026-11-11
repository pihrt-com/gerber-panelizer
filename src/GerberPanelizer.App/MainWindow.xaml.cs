using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GerberPanelizer.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(this);
    }

    private void OpenWebsite(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void WorkspaceDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void WorkspaceDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && DataContext is MainViewModel viewModel) viewModel.ImportFiles(paths);
        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel)
        {
            return;
        }

        if (((App)Application.Current).IsUpdateInstallerStarting)
        {
            return;
        }

        var localization = LocalizationService.Instance;
        var confirmation = MessageBox.Show(
            this,
            localization["ExitConfirmationText"],
            localization["ExitConfirmationTitle"],
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        e.Cancel = confirmation != MessageBoxResult.Yes;
    }

    protected override async void OnClosed(EventArgs e)
    {
        if (DataContext is MainViewModel viewModel) await viewModel.WegstrMachine.DisposeAsync();
        base.OnClosed(e);
    }
}
