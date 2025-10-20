using System;
using System.Net.Http;
using System.Windows;
using RobloxGuard.Core;

namespace RobloxGuard.UI;

/// <summary>
/// Block UI window shown when a game is blocked.
/// </summary>
public partial class BlockWindow : Window
{
    private readonly long _placeId;
    private bool _unlocked = false;

    public BlockWindow(long placeId)
    {
        InitializeComponent();
        _placeId = placeId;
        
        PlaceIdText.Text = placeId.ToString();
        
        // Try to fetch game name from Roblox API
        LoadGameName();
    }

    private async void LoadGameName()
    {
        try
        {
            var gameName = await FetchGameName(_placeId);
            Dispatcher.Invoke(() => 
            {
                GameNameText.Text = gameName ?? "Unknown Game";
            });
        }
        catch
        {
            Dispatcher.Invoke(() => 
            {
                GameNameText.Text = "Unknown Game (offline)";
            });
        }
    }

    private async Task<string?> FetchGameName(long placeId)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var url = $"https://api.roblox.com/Marketplace/ProductDetails?assetId={placeId}";
            var response = await client.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            
            // Simple JSON parsing for Name field
            var nameStart = content.IndexOf("\"Name\":\"", StringComparison.Ordinal);
            if (nameStart == -1)
                return null;

            nameStart += 8; // Length of "\"Name\":\""
            var nameEnd = content.IndexOf("\"", nameStart, StringComparison.Ordinal);
            
            if (nameEnd > nameStart)
                return content.Substring(nameStart, nameEnd - nameStart);
        }
        catch
        {
            // API unavailable or network error
        }

        return null;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Open Roblox home/favorites
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://www.roblox.com/home",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
        
        Close();
    }

    private void RequestButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Open email client with request template
            var mailto = new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"mailto:?subject=Roblox%20Game%20Access%20Request&body=I%20would%20like%20to%20play%20the%20game%20with%20ID%20{_placeId}%20on%20Roblox.",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(mailto);
        }
        catch { }
        
        Close();
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        // Show PIN entry dialog
        var pinDialog = new PinEntryDialog();
        if (pinDialog.ShowDialog() == true)
        {
            // Dialog verified the PIN and unlocked the game
            _unlocked = true;
            Close();
        }
    }

    /// <summary>
    /// Gets whether the user unlocked the game with correct PIN.
    /// </summary>
    public bool IsUnlocked => _unlocked;
}
