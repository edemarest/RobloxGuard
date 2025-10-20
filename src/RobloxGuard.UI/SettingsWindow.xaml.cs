using System.Windows;
using System.Windows.Controls;
using RobloxGuard.Core;

namespace RobloxGuard.UI;

/// <summary>
/// Settings window for RobloxGuard configuration.
/// </summary>
public partial class SettingsWindow : Window
{
    private RobloxGuardConfig _config = null!;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _config = ConfigManager.Load();

        // Load PIN status
        if (string.IsNullOrEmpty(_config.ParentPINHash))
        {
            PinStatusText.Text = "NOT SET";
            PinStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red
        }

        // Load mode
        if (_config.WhitelistMode)
            WhitelistMode.IsChecked = true;
        else
            BlacklistMode.IsChecked = true;

        // Load blocklist
        RefreshBlocklist();

        // Load settings
        OverlayCheckBox.IsChecked = _config.OverlayEnabled;
        WatcherCheckBox.IsChecked = true; // Default to enabled

        // Show config path
        ConfigPathText.Text = $"Config path: {ConfigManager.GetConfigPath()}";
    }

    private void RefreshBlocklist()
    {
        BlocklistBox.Items.Clear();
        foreach (var placeId in _config.Blocklist.OrderBy(p => p))
        {
            BlocklistBox.Items.Add(placeId.ToString());
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(PlaceIdInput.Text, out var placeId))
        {
            MessageBox.Show("Please enter a valid Place ID (number)", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_config.Blocklist.Contains(placeId))
        {
            MessageBox.Show("This Place ID is already blocked", "Duplicate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _config.Blocklist.Add(placeId);
        PlaceIdInput.Text = "";
        RefreshBlocklist();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (BlocklistBox.SelectedItem is string selected && long.TryParse(selected, out var placeId))
        {
            _config.Blocklist.Remove(placeId);
            RefreshBlocklist();
        }
    }

    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        _config.WhitelistMode = WhitelistMode.IsChecked == true;
    }

    private void SetPinButton_Click(object sender, RoutedEventArgs e)
    {
        var newPin = NewPinInput.Password;
        var confirmPin = ConfirmPinInput.Password;

        PinErrorMessage.Text = "";

        if (string.IsNullOrEmpty(newPin))
        {
            PinErrorMessage.Text = "PIN cannot be empty";
            return;
        }

        if (newPin.Length < 4)
        {
            PinErrorMessage.Text = "PIN must be at least 4 characters";
            return;
        }

        if (newPin != confirmPin)
        {
            PinErrorMessage.Text = "PINs do not match";
            return;
        }

        // Hash and save
        _config.ParentPINHash = ConfigManager.HashPIN(newPin);
        NewPinInput.Clear();
        ConfirmPinInput.Clear();

        PinStatusText.Text = "SET";
        PinStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // Green
        
        MessageBox.Show("PIN saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenAppDataButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = ConfigManager.GetAppDataPath();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _config.OverlayEnabled = OverlayCheckBox.IsChecked == true;
        ConfigManager.Save(_config);
        MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
