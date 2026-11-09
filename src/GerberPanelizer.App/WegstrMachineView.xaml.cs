using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GerberPanelizer.Core.Machines.Wegstr;

namespace GerberPanelizer.App;

public partial class WegstrMachineView : UserControl
{
    private Key? activeJogKey;

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

    private void ControlKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.IsRepeat || activeJogKey is not null || IsEditingControlFocused() || DataContext is not WegstrMachineViewModel viewModel || !viewModel.CanJog) return;
        if (!TryParseKey(eventArgs.Key, out var axis, out var sign)) return;
        activeJogKey = eventArgs.Key;
        viewModel.BeginJog(axis, sign);
        eventArgs.Handled = true;
    }

    private void ControlKeyUp(object sender, KeyEventArgs eventArgs)
    {
        if (activeJogKey != eventArgs.Key) return;
        EndKeyboardJog();
        eventArgs.Handled = true;
    }

    private void ControlLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs eventArgs)
    {
        if (!IsKeyboardFocusWithin) EndKeyboardJog();
    }

    private void ControlUnloaded(object sender, RoutedEventArgs eventArgs) => EndKeyboardJog();

    private void EndKeyboardJog()
    {
        if (activeJogKey is null) return;
        activeJogKey = null;
        if (DataContext is WegstrMachineViewModel viewModel) viewModel.EndJog();
    }

    private static bool IsEditingControlFocused() => Keyboard.FocusedElement is TextBoxBase or ComboBox or Slider;

    private static bool TryParseKey(Key key, out WegstrAxis axis, out int sign)
    {
        (axis, sign) = key switch
        {
            Key.Left => (WegstrAxis.X, -1),
            Key.Right => (WegstrAxis.X, 1),
            Key.Up => (WegstrAxis.Y, 1),
            Key.Down => (WegstrAxis.Y, -1),
            Key.Add or Key.OemPlus => (WegstrAxis.Z, 1),
            Key.Subtract or Key.OemMinus => (WegstrAxis.Z, -1),
            _ => (default, 0),
        };
        return sign != 0;
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
