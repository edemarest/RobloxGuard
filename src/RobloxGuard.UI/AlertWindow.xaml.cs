using System.Windows;
using System.Windows.Threading;

namespace RobloxGuard.UI;

/// <summary>
/// Alert window that shows when an unsafe game is detected.
/// Features: Red border, pulsing animation, auto-closes after 3 seconds.
/// </summary>
public partial class AlertWindow : Window
{
    private DispatcherTimer? _countdownTimer;
    private int _remainingSeconds = 20;  // Changed from 5 to 20 seconds

    public AlertWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Play red flash animation on window background
        try
        {
            var storyboard = FindResource("RedFlash") as System.Windows.Media.Animation.Storyboard;
            if (storyboard != null)
            {
                storyboard.Begin(this);
            }
        }
        catch { }

        // Play text flashing animation on main message
        try
        {
            var textFlash = FindResource("TextFlash") as System.Windows.Media.Animation.Storyboard;
            if (textFlash != null)
            {
                textFlash.Begin(this);
            }
        }
        catch { }

        // Ensure window is focused
        Activate();
        Focus();

        // Start the countdown timer
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, args) =>
        {
            _remainingSeconds--;
            var countdownText = FindName("CountdownText") as System.Windows.Controls.TextBlock;
            if (countdownText != null && _remainingSeconds > 0)
            {
                countdownText.Text = $"Closing in {_remainingSeconds} second{(_remainingSeconds != 1 ? "s" : "")}...";
            }
            else if (_remainingSeconds <= 0)
            {
                _countdownTimer.Stop();
                Close();
            }
        };
        _countdownTimer.Start();

        Console.WriteLine($"[AlertWindow] Window loaded with BRAINDEAD CONTENT message, will close in {_remainingSeconds} seconds");
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer?.Stop();
        Console.WriteLine("[AlertWindow] Window closed");
        base.OnClosed(e);
    }
}
