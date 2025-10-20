using System.Windows;
using System.Windows.Threading;

namespace RobloxGuard.UI;

/// <summary>
/// Simple flashing red alert window that shows when an unsafe game is detected.
/// Auto-closes after 3 seconds.
/// </summary>
public partial class AlertWindow : Window
{
    private DispatcherTimer? _countdownTimer;
    private int _remainingSeconds = 3;

    public AlertWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Start the countdown timer
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, args) =>
        {
            _remainingSeconds--;
            if (_remainingSeconds > 0)
            {
                CountdownText.Text = $"This window will close in {_remainingSeconds} second{(_remainingSeconds != 1 ? "s" : "")}...";
            }
            else
            {
                _countdownTimer.Stop();
                Close();
            }
        };
        _countdownTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        base.OnClosed(e);
    }
}
