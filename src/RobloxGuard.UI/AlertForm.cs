using System.Drawing;
using System.Windows.Forms;

namespace RobloxGuard.UI;

/// <summary>
/// Windows Forms-based alert window (no WPF dependency issues).
/// Styled with red alert, pulsing effects, and flashing text.
/// </summary>
public partial class AlertForm : Form
{
    private int _secondsRemaining = 20;
    private System.Windows.Forms.Timer? _countdownTimer;
    private System.Windows.Forms.Timer? _flashTimer;
    private Label? _mainMessageLabel;
    private bool _isRedState = true;

    public AlertForm()
    {
        InitializeComponent();
        SetupUI();
    }

    private void SetupUI()
    {
        // Window properties
        this.Text = "🛑 RobloxGuard Alert";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.TopMost = true;
        this.ShowInTaskbar = true;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ControlBox = false;
        this.Size = new Size(700, 500);
        this.BackColor = Color.FromArgb(26, 26, 26); // Dark background
        this.ForeColor = Color.White;
        this.Icon = null;
        this.Padding = new Padding(0);
        this.Margin = new Padding(0);

        // Add red border effect
        var borderPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 26, 26),
            Padding = new Padding(8)
        };
        
        var borderInner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Red,
            Padding = new Padding(2)
        };
        
        var mainContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(26, 26, 26),
            Padding = new Padding(20)
        };

        borderInner.Controls.Add(mainContent);
        borderPanel.Controls.Add(borderInner);
        this.Controls.Add(borderPanel);

        // Create main content container
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            AutoScroll = false,
            Padding = new Padding(10),
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        
        // Set row styles
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 25)); // Emojis
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 35)); // Main text
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 15)); // Description
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 15)); // Extra info
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 10)); // Countdown

        mainContent.Controls.Add(container);

        // Emoji row
        var emojiLabel = new Label
        {
            Text = "🧠  ❌",
            Font = new Font("Arial", 80, FontStyle.Bold),
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        container.Controls.Add(emojiLabel, 0, 0);

        // Main message (pulsing)
        _mainMessageLabel = new Label
        {
            Text = "BRAINDEAD\nCONTENT\nDETECTED",
            Font = new Font("Arial", 56, FontStyle.Bold),
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        container.Controls.Add(_mainMessageLabel, 0, 1);

        // Description
        var descLabel = new Label
        {
            Text = "This game has been blocked by your parent.",
            Font = new Font("Arial", 14),
            ForeColor = Color.FromArgb(200, 200, 200),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        container.Controls.Add(descLabel, 0, 2);

        // Extra info
        var infoLabel = new Label
        {
            Text = "The Roblox process was closed.",
            Font = new Font("Arial", 12),
            ForeColor = Color.FromArgb(150, 150, 150),
            TextAlign = ContentAlignment.TopCenter,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        container.Controls.Add(infoLabel, 0, 3);

        // Countdown label
        var countdownLabel = new Label
        {
            Name = "CountdownLabel",
            Text = "Closing in 20 seconds...",
            Font = new Font("Arial", 11),
            ForeColor = Color.FromArgb(153, 153, 153),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            BackColor = Color.FromArgb(26, 26, 26)
        };
        container.Controls.Add(countdownLabel, 0, 4);

        // Start flashing timer for main message
        _flashTimer = new System.Windows.Forms.Timer();
        _flashTimer.Interval = 600;
        _flashTimer.Tick += (s, e) =>
        {
            if (_mainMessageLabel != null)
            {
                _isRedState = !_isRedState;
                _mainMessageLabel.ForeColor = _isRedState ? Color.Red : Color.FromArgb(102, 0, 0);
            }
        };
        _flashTimer.Start();

        // Start countdown timer
        _countdownTimer = new System.Windows.Forms.Timer();
        _countdownTimer.Interval = 1000;
        _countdownTimer.Tick += (s, e) =>
        {
            _secondsRemaining--;
            countdownLabel.Text = $"Closing in {_secondsRemaining} second{(_secondsRemaining != 1 ? "s" : "")}...";
            
            if (_secondsRemaining <= 0)
            {
                _countdownTimer.Stop();
                _flashTimer?.Stop();
                this.Close();
            }
        };
        _countdownTimer.Start();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.ResumeLayout(false);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _countdownTimer?.Stop();
        _countdownTimer?.Dispose();
        _flashTimer?.Stop();
        _flashTimer?.Dispose();
        base.OnFormClosing(e);
    }
}
