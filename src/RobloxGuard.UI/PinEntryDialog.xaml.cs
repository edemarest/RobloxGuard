using System.Windows;
using RobloxGuard.Core;

namespace RobloxGuard.UI;

/// <summary>
/// PIN entry dialog for parent unlock.
/// </summary>
public partial class PinEntryDialog : Window
{
    public PinEntryDialog()
    {
        InitializeComponent();
        PinInput.Focus();
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var enteredPin = PinInput.Password;
        
        if (string.IsNullOrEmpty(enteredPin))
        {
            ErrorMessage.Text = "Please enter a PIN";
            return;
        }

        // Load config and verify PIN
        var config = ConfigManager.Load();
        
        if (string.IsNullOrEmpty(config.ParentPINHash))
        {
            ErrorMessage.Text = "Parent PIN not set up yet";
            return;
        }

        if (ConfigManager.VerifyPIN(enteredPin, config.ParentPINHash))
        {
            // Correct PIN
            DialogResult = true;
            Close();
        }
        else
        {
            // Wrong PIN
            ErrorMessage.Text = "❌ Incorrect PIN";
            PinInput.Clear();
            PinInput.Focus();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
