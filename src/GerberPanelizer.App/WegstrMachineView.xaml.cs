using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GerberPanelizer.Core.Machines.Wegstr;

namespace GerberPanelizer.App;

public partial class WegstrMachineView : UserControl
{
    public WegstrMachineView() => InitializeComponent();

    private void JogButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not ButtonBase button || button.Tag is not string direction || DataContext is not WegstrMachineViewModel viewModel) return;
        if (!TryParseDirection(direction, out var axis, out var sign)) return;
        button.CaptureMouse();
        viewModel.BeginJog(axis, sign);
        eventArgs.Handled = true;
    }

    private void JogButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (DataContext is WegstrMachineViewModel viewModel) viewModel.EndJog();
        if (sender is UIElement element && element.IsMouseCaptured) element.ReleaseMouseCapture();
        eventArgs.Handled = true;
    }

    private void JogButtonLostCapture(object sender, MouseEventArgs eventArgs)
    {
        if (DataContext is WegstrMachineViewModel viewModel) viewModel.EndJog();
    }

    private static bool TryParseDirection(string direction, out WegstrAxis axis, out int sign)
    {
        axis = direction[0] switch
        {
            'X' => WegstrAxis.X,
            'Y' => WegstrAxis.Y,
            'Z' => WegstrAxis.Z,
            _ => default,
        };
        sign = direction.Length == 2 && direction[1] == '+' ? 1 : -1;
        return direction.Length == 2 && direction[0] is 'X' or 'Y' or 'Z' && direction[1] is '+' or '-';
    }
}
