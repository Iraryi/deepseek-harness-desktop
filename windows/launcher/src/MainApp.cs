using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal static class Program
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main()
    {
        try { SetProcessDPIAware(); } catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AppConfig config = AppConfig.Load();
        string instanceSuffix = BuildInstanceSuffix();
        string mutexName = "Local\\DeepSeekHarness.Desktop." + instanceSuffix;
        bool createdNew;
        using (Mutex instanceMutex = new Mutex(true, mutexName, out createdNew))
        {
            if (!createdNew)
            {
                string message = config.Language == "zh-CN"
                    ? "DeepSeek Harness 已经在运行，可能已缩至系统托盘。\r\n本次不会启动第二个实例；如需打开，请点击系统托盘图标。"
                    : "DeepSeek Harness is already running, possibly in the system tray.\r\nA second instance will not be started; use the system tray icon to reopen it.";
                MessageBox.Show(message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!config.FirstRunCompleted)
            {
                string configApp = Path.Combine(AppPaths.ExeDir, "dsh-config.exe");
                if (File.Exists(configApp))
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(configApp, "--first-run");
                    startInfo.WorkingDirectory = AppPaths.ExeDir;
                    Process.Start(startInfo);
                }
                else
                {
                    MessageBox.Show("First-time setup requires dsh-config.exe next to dsh.exe.",
                        "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            Application.Run(new MainForm(config));
        }
    }

    private static string BuildInstanceSuffix()
    {
        string value = Environment.GetEnvironmentVariable("DEEPSEEK_HARNESS_INSTANCE_SCOPE");
        if (string.IsNullOrEmpty(value)) return "DEFAULT";
        value = value.ToUpperInvariant();
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return hash.ToString("X8");
        }
    }
}

internal sealed class LoadingOverlay : Control
{
    private readonly string _style;
    private readonly System.Windows.Forms.Timer _timer;
    private string _stage;
    private float _progress;
    private float _targetProgress;
    private float _angle;
    private bool _completing;
    private bool _error;
    private bool _dismissed;
    private int _completeTicks;

    public event EventHandler Dismissed;

    public LoadingOverlay(string style)
    {
        _style = style;
        _stage = "Preparing application";
        _progress = 4F;
        _targetProgress = 8F;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(8, 22, 52);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _timer = new System.Windows.Forms.Timer();
        _timer.Interval = 33;
        _timer.Tick += delegate
        {
            _angle += 1.8F;
            if (_angle >= 360F) _angle -= 360F;
            if (_progress < _targetProgress)
            {
                float increment = Math.Max(0.12F, (_targetProgress - _progress) * 0.055F);
                _progress = Math.Min(_targetProgress, _progress + increment);
            }
            if (_completing && _progress >= 99.7F)
            {
                _completeTicks++;
                if (_completeTicks >= 9)
                {
                    _timer.Stop();
                    _dismissed = true;
                    Visible = false;
                    EventHandler handler = Dismissed;
                    if (handler != null) handler(this, EventArgs.Empty);
                    return;
                }
            }
            Invalidate();
        };

        Visible = style != "off";
        if (Visible) _timer.Start();
    }

    public void SetStage(string stage, float progress)
    {
        if (_style == "off" || _dismissed) return;
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)delegate { SetStage(stage, progress); });
            return;
        }
        _stage = string.IsNullOrEmpty(stage) ? "Working" : stage;
        _targetProgress = Math.Max(_targetProgress, Math.Min(96F, progress));
        _error = false;
        Invalidate();
    }

    public void Complete(string stage)
    {
        if (_style == "off")
        {
            EventHandler handler = Dismissed;
            if (handler != null) handler(this, EventArgs.Empty);
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)delegate { Complete(stage); });
            return;
        }
        _stage = string.IsNullOrEmpty(stage) ? "Interface ready" : stage;
        _targetProgress = 100F;
        _completing = true;
        _error = false;
        Invalidate();
    }

    public void ShowError(string stage)
    {
        if (_style == "off") return;
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)delegate { ShowError(stage); });
            return;
        }
        _stage = string.IsNullOrEmpty(stage) ? "Startup stopped" : stage;
        _targetProgress = Math.Max(_targetProgress, 72F);
        _error = true;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (_style == "progress") DrawProgressLoader(e.Graphics);
        else DrawWhaleLoader(e.Graphics);
    }

    private void DrawWhaleLoader(Graphics graphics)
    {
        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        using (LinearGradientBrush background = new LinearGradientBrush(bounds,
            Color.FromArgb(6, 24, 59), Color.FromArgb(8, 54, 94), 90F))
        {
            graphics.FillRectangle(background, bounds);
        }

        int glowSize = Math.Max(420, Math.Min(bounds.Width, bounds.Height));
        Rectangle glowBounds = new Rectangle(
            bounds.Width / 2 - glowSize / 2,
            bounds.Height / 2 - glowSize / 2 - 40,
            glowSize,
            glowSize);
        using (GraphicsPath glowPath = new GraphicsPath())
        {
            glowPath.AddEllipse(glowBounds);
            using (PathGradientBrush glow = new PathGradientBrush(glowPath))
            {
                glow.CenterColor = Color.FromArgb(105, 78, 194, 224);
                glow.SurroundColors = new Color[] { Color.FromArgb(0, 8, 36, 72) };
                graphics.FillPath(glow, glowPath);
            }
        }

        for (int i = 0; i < 18; i++)
        {
            float bubbleX = (i * 137 % Math.Max(1, bounds.Width - 30)) + 15F;
            float bubbleY = (i * 83 % Math.Max(1, bounds.Height - 90)) + 45F;
            float bubbleSize = 2F + (i % 4);
            using (Brush bubble = new SolidBrush(Color.FromArgb(28 + (i % 3) * 10, 212, 244, 255)))
            {
                graphics.FillEllipse(bubble, bubbleX, bubbleY, bubbleSize, bubbleSize);
            }
        }

        float centerX = bounds.Width / 2F;
        float centerY = bounds.Height / 2F - 48F;
        float orbit = Math.Max(74F, Math.Min(bounds.Width, bounds.Height) * 0.095F);
        float whaleScale = Math.Max(0.72F, Math.Min(1.35F, Math.Min(bounds.Width, bounds.Height) / 720F));
        double radians = _angle * Math.PI / 180.0;
        PointF first = new PointF(
            centerX + (float)Math.Cos(radians) * orbit,
            centerY + (float)Math.Sin(radians) * orbit);
        PointF second = new PointF(
            centerX - (float)Math.Cos(radians) * orbit,
            centerY - (float)Math.Sin(radians) * orbit);
        DrawWhale(graphics, first, _angle + 90F, whaleScale, Color.FromArgb(238, 222, 248, 255));
        DrawWhale(graphics, second, _angle + 270F, whaleScale * 0.92F, Color.FromArgb(220, 118, 211, 240));

        float barY;
        using (StringFormat centered = new StringFormat())
        {
            centered.Alignment = StringAlignment.Center;
            centered.LineAlignment = StringAlignment.Center;
            using (Font titleFont = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold))
            using (Font stageFont = new Font("Consolas", 10F, FontStyle.Regular))
            using (Brush titleBrush = new SolidBrush(Color.FromArgb(244, 250, 255)))
            using (Brush stageBrush = new SolidBrush(_error ? Color.FromArgb(255, 181, 181) : Color.FromArgb(202, 225, 239)))
            {
                float titleY = centerY + orbit + 72F;
                float titleHeight = Math.Max(46F, titleFont.GetHeight(graphics) + 10F);
                float stageY = titleY + titleHeight + 2F;
                float stageHeight = Math.Max(30F, stageFont.GetHeight(graphics) + 8F);
                graphics.DrawString("DeepSeek Harness", titleFont, titleBrush,
                    new RectangleF(30F, titleY, bounds.Width - 60F, titleHeight), centered);
                graphics.DrawString(BuildStageLine(), stageFont, stageBrush,
                    new RectangleF(30F, stageY, bounds.Width - 60F, stageHeight), centered);
                barY = stageY + stageHeight + 20F;
            }
        }

        float barWidth = Math.Min(520F, Math.Max(220F, bounds.Width - 100F));
        float barX = centerX - barWidth / 2F;
        using (Brush track = new SolidBrush(Color.FromArgb(45, 226, 243, 252)))
        using (Brush fill = new SolidBrush(_error ? Color.FromArgb(230, 238, 107, 107) : Color.FromArgb(230, 121, 214, 239)))
        {
            graphics.FillRectangle(track, barX, barY, barWidth, 3F);
            graphics.FillRectangle(fill, barX, barY, barWidth * _progress / 100F, 3F);
        }
    }

    private void DrawProgressLoader(Graphics graphics)
    {
        Rectangle bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        using (LinearGradientBrush background = new LinearGradientBrush(bounds,
            Color.FromArgb(7, 11, 24), Color.FromArgb(14, 28, 48), 90F))
        {
            graphics.FillRectangle(background, bounds);
        }

        float cardWidth = Math.Min(720F, Math.Max(420F, bounds.Width - 80F));
        float cardHeight = 270F;
        RectangleF card = new RectangleF(
            bounds.Width / 2F - cardWidth / 2F,
            bounds.Height / 2F - cardHeight / 2F,
            cardWidth,
            cardHeight);
        using (GraphicsPath cardPath = RoundedRectangle(card, 18F))
        using (Brush shadow = new SolidBrush(Color.FromArgb(72, 0, 0, 0)))
        using (Brush surface = new SolidBrush(Color.FromArgb(242, 16, 25, 42)))
        using (Pen border = new Pen(Color.FromArgb(70, 117, 169, 218)))
        {
            RectangleF shadowRect = card;
            shadowRect.Offset(0F, 10F);
            using (GraphicsPath shadowPath = RoundedRectangle(shadowRect, 18F)) graphics.FillPath(shadow, shadowPath);
            graphics.FillPath(surface, cardPath);
            graphics.DrawPath(border, cardPath);
        }

        float left = card.Left + 38F;
        float top = card.Top + 30F;
        using (Font monoSmall = new Font("Consolas", 9F, FontStyle.Regular))
        using (Font monoLarge = new Font("Consolas", 15F, FontStyle.Bold))
        using (Brush muted = new SolidBrush(Color.FromArgb(132, 157, 184)))
        using (Brush primary = new SolidBrush(Color.FromArgb(223, 235, 248)))
        using (Brush accent = new SolidBrush(_error ? Color.FromArgb(255, 138, 138) : Color.FromArgb(98, 213, 190)))
        {
            graphics.DrawString("DSH / EMBEDDED WEB RUNTIME", monoSmall, muted, left, top);
            graphics.DrawString("BOOT SEQUENCE", monoLarge, primary, left, top + 30F);
            graphics.DrawString(BuildStageLine(), monoSmall, accent,
                new RectangleF(left, top + 86F, cardWidth - 76F, 24F));

            const int segments = 28;
            float gap = 5F;
            float segmentWidth = (cardWidth - 76F - gap * (segments - 1)) / segments;
            int active = (int)Math.Round(segments * _progress / 100F);
            for (int i = 0; i < segments; i++)
            {
                Color color = i < active
                    ? (_error ? Color.FromArgb(228, 230, 94, 94) : Color.FromArgb(228, 74, 203, 179))
                    : Color.FromArgb(55, 124, 145, 169);
                using (Brush segment = new SolidBrush(color))
                {
                    graphics.FillRectangle(segment, left + i * (segmentWidth + gap), top + 126F, segmentWidth, 12F);
                }
            }

            graphics.DrawString(((int)_progress).ToString("00") + "%", monoSmall, primary,
                new RectangleF(left, top + 153F, cardWidth - 76F, 20F));
            graphics.DrawString("Local service  •  WebView2  •  Web interface", monoSmall, muted,
                new RectangleF(left, top + 190F, cardWidth - 76F, 24F));
        }
    }

    private string BuildStageLine()
    {
        int step = Math.Max(1, Math.Min(5, (int)Math.Ceiling(_progress / 20F)));
        return "[BOOT " + step.ToString("00") + "/05] " + _stage;
    }

    private static void DrawWhale(Graphics graphics, PointF center, float rotation, float scale, Color color)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(center.X, center.Y);
        graphics.RotateTransform(rotation);
        graphics.ScaleTransform(scale, scale);

        using (GraphicsPath body = new GraphicsPath())
        {
            body.StartFigure();
            body.AddBezier(53F, 0F, 45F, -17F, 22F, -23F, -8F, -16F);
            body.AddBezier(-8F, -16F, -29F, -12F, -47F, -4F, -54F, -1F);
            body.AddBezier(-54F, -1F, -54F, 1F, -47F, 4F, -8F, 16F);
            body.AddBezier(-8F, 16F, 22F, 23F, 45F, 17F, 53F, 0F);
            body.CloseFigure();

            using (Brush bodyBrush = new SolidBrush(color)) graphics.FillPath(bodyBrush, body);
        }

        using (GraphicsPath fins = new GraphicsPath())
        {
            fins.AddBezier(13F, -11F, 4F, -29F, -8F, -39F, -1F, -11F);
            fins.CloseFigure();
            fins.StartFigure();
            fins.AddBezier(13F, 11F, 4F, 29F, -8F, 39F, -1F, 11F);
            fins.CloseFigure();
            using (Brush finBrush = new SolidBrush(Color.FromArgb(Math.Max(0, color.A - 26), color.R, color.G, color.B)))
            {
                graphics.FillPath(finBrush, fins);
            }
        }

        using (GraphicsPath tail = new GraphicsPath())
        {
            tail.StartFigure();
            tail.AddBezier(-44F, -2F, -62F, -10F, -72F, -25F, -65F, -2F);
            tail.AddBezier(-65F, -2F, -62F, 0F, -62F, 0F, -65F, 2F);
            tail.AddBezier(-65F, 2F, -72F, 25F, -62F, 10F, -44F, 2F);
            tail.AddBezier(-44F, 2F, -46F, 1F, -46F, -1F, -44F, -2F);
            tail.CloseFigure();
            using (Brush tailBrush = new SolidBrush(color)) graphics.FillPath(tailBrush, tail);
        }

        using (Pen highlight = new Pen(Color.FromArgb(76, 255, 255, 255), 1.2F))
        {
            graphics.DrawBezier(highlight, 34F, 0F, 15F, -3F, -10F, -2F, -38F, 0F);
        }
        graphics.Restore(state);
    }

    private static GraphicsPath RoundedRectangle(RectangleF rectangle, float radius)
    {
        float diameter = radius * 2F;
        GraphicsPath path = new GraphicsPath();
        path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180F, 90F);
        path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270F, 90F);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0F, 90F);
        path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90F, 90F);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class MainForm : Form
{
    private const string BakedRepo = @"C:\Users\65428\Documents\Codex\2026-08-14\new-chat\deepseek-harness";

    private AppConfig _cfg;
    private Panel _toolPanel;
    private Panel _logPanel;
    private TextBox _logBox;
    private WebView2 _webView;
    private Label _toolbarTitle;
    private Label _statusText;
    private Button _startButton;
    private Button _stopButton;
    private Button _refreshButton;
    private Button _openBrowserButton;
    private Button _configButton;
    private Button _logButton;
    private Button _exitButton;
    private Button _trayButton;
    private System.Windows.Forms.Timer _revealTimer;
    private LoadingOverlay _loadingOverlay;
    private NotifyIcon _trayIcon;

    private Process _proc;
    private bool _shuttingDown;
    private bool _exitRequested;
    private bool _coreReady;
    private bool _portReady;
    private bool _fullscreen;
    private bool _toolbarSticky;
    private bool _forceToolbarVisible;
    private bool _toolbarTargetVisible;
    private bool _layingOutToolbar;
    private bool _runtimeErrorShown;
    private bool _webViewInitializing;
    private bool _trayHidePending;
    private int _activePort;
    private string _activeUrl;
    private int _toolbarTargetTop;
    private int _toolbarHideTicks;
    private Keys _toolbarKey;
    private Keys _toolbarMods;
    private Keys _fullscreenKey;
    private Keys _fullscreenMods;

    public MainForm()
        : this(AppConfig.Load())
    {
    }

    public MainForm(AppConfig config)
    {
        _cfg = config ?? AppConfig.Load();
        _activePort = _cfg.Port;
        _activeUrl = _cfg.Url;
        if (!TryParseHotkey(_cfg.ToolbarHotkey, out _toolbarKey, out _toolbarMods)) { _toolbarKey = Keys.F8; _toolbarMods = Keys.None; }
        if (!TryParseHotkey(_cfg.FullscreenHotkey, out _fullscreenKey, out _fullscreenMods)) { _fullscreenKey = Keys.F11; _fullscreenMods = Keys.None; }
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { Icon = SystemIcons.Application; }
        BuildUi();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        Keys modifiers = keyData & Keys.Modifiers;
        if (key == _toolbarKey && modifiers == _toolbarMods)
        {
            ToggleToolbarSticky();
            return true;
        }
        if (key == _fullscreenKey && modifiers == _fullscreenMods)
        {
            ToggleFullscreen();
            return true;
        }
        if (key == Keys.Escape && modifiers == Keys.None && _fullscreen)
        {
            _fullscreen = false;
            ApplyLaunchMode("window");
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private void BuildUi()
    {
        Text = "DeepSeek Harness";
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);

        _webView = new WebView2();
        _webView.Dock = DockStyle.Fill;
        _webView.DefaultBackgroundColor = Color.White;
        _webView.CreationProperties = new CoreWebView2CreationProperties();
        _webView.CreationProperties.UserDataFolder = AppPaths.WebView2Dir;
        _webView.Visible = _cfg.LoadingStyle == "off";
        Controls.Add(_webView);

        _logPanel = new Panel();
        _logPanel.Dock = DockStyle.Top;
        _logPanel.Height = 160;
        _logPanel.Visible = false;
        Controls.Add(_logPanel);

        _logBox = new TextBox();
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = Color.FromArgb(24, 27, 36);
        _logBox.ForeColor = Color.FromArgb(200, 205, 220);
        _logBox.Font = new Font("Consolas", 9F);
        _logPanel.Controls.Add(_logBox);

        _toolPanel = new Panel();
        _toolPanel.Dock = DockStyle.None;
        _toolPanel.Height = 60;
        _toolPanel.Location = new Point(0, 0);
        _toolPanel.Width = ClientSize.Width;
        _toolPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _toolPanel.BackColor = Color.FromArgb(250, 251, 254);
        Controls.Add(_toolPanel);

        _toolbarTitle = new Label();
        _toolbarTitle.Text = "DeepSeek Harness";
        _toolbarTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
        _toolbarTitle.AutoSize = false;
        _toolbarTitle.TextAlign = ContentAlignment.MiddleLeft;
        _toolbarTitle.ForeColor = Color.FromArgb(30, 41, 99);
        _toolPanel.Controls.Add(_toolbarTitle);

        _statusText = new Label();
        _statusText.Text = "Not started";
        _statusText.AutoSize = false;
        _statusText.AutoEllipsis = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        _statusText.ForeColor = Color.FromArgb(120, 120, 120);
        _toolPanel.Controls.Add(_statusText);

        int x = 0;
        _startButton = MakeToolButton("Start", ref x);
        _stopButton = MakeToolButton("Stop", ref x);
        _refreshButton = MakeToolButton("Reload", ref x);
        _openBrowserButton = MakeToolButton("In browser", ref x);
        _configButton = MakeToolButton("Config", ref x);
        _logButton = MakeToolButton("Log", ref x);
        _exitButton = MakeToolButton("Exit", ref x);
        _startButton.Click += delegate { StartServer(); };
        _stopButton.Click += delegate { StopServer(); };
        _refreshButton.Click += delegate { RefreshPage(); };
        _openBrowserButton.Click += delegate { OpenInBrowser(); };
        _configButton.Click += delegate { OpenConfigApp(); };
        _logButton.Click += delegate { ToggleLog(); };
        _exitButton.Click += delegate { ExitApplication(); };

        if (_cfg.ShowTrayButton && _cfg.CloseAction == "exit")
        {
            _trayButton = new Button();
            _trayButton.Text = "To tray";
            _trayButton.Size = new Size(126, 42);
            _trayButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _trayButton.FlatStyle = FlatStyle.Flat;
            _trayButton.FlatAppearance.BorderColor = Color.FromArgb(160, 177, 207);
            _trayButton.UseVisualStyleBackColor = true;
            _trayButton.Click += delegate { MinimizeToTray(); };
            _toolPanel.Controls.Add(_trayButton);
        }
        _toolPanel.Resize += delegate { PositionTrayButton(); };
        Resize += delegate { PositionTrayButton(); };

        _toolbarSticky = !_cfg.ToolbarAutoHide;
        _toolbarTargetVisible = _toolbarSticky;
        PositionTrayButton();
        RequestToolbar(_toolbarSticky, true);

        SetupTrayIcon();

        _loadingOverlay = new LoadingOverlay(_cfg.LoadingStyle);
        _loadingOverlay.Dismissed += delegate { RevealInterface(); };
        Controls.Add(_loadingOverlay);
        if (_loadingOverlay.Visible) _loadingOverlay.BringToFront();

        _revealTimer = new System.Windows.Forms.Timer();
        _revealTimer.Interval = 16;
        _revealTimer.Tick += delegate
        {
            UpdateToolbarReveal();
            AnimateToolbar();
        };
        _revealTimer.Start();

        FormClosing += delegate(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_exitRequested && _cfg.CloseAction == "tray")
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }
            _exitRequested = true;
            StopServer();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        };
        Load += delegate { ApplyLaunchMode(_cfg.LaunchMode); };
        Shown += delegate
        {
            PositionTrayButton();
            SetLoadingStage("Preparing runtime", 12F);
            BeginInvoke((MethodInvoker)StartServer);
            BeginInvoke((MethodInvoker)delegate { InitWebView(); });
        };
        SetButtons();
    }

    private Button MakeToolButton(string text, ref int x)
    {
        Button b = new Button();
        b.Text = text;
        b.Location = new Point(x, 9);
        int width = Math.Max(72, Math.Min(132, TextRenderer.MeasureText(text, Font).Width + 42));
        b.Size = new Size(width, 42);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = Color.FromArgb(180, 190, 215);
        b.UseVisualStyleBackColor = true;
        _toolPanel.Controls.Add(b);
        x += width + 8;
        return b;
    }

    private void SetupTrayIcon()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem showItem = new ToolStripMenuItem("Show DeepSeek Harness");
        ToolStripMenuItem configItem = new ToolStripMenuItem("Open Config");
        ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
        showItem.Click += delegate { RestoreFromTray(); };
        configItem.Click += delegate { OpenConfigApp(); };
        exitItem.Click += delegate { ExitApplication(); };
        menu.Items.Add(showItem);
        menu.Items.Add(configItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon();
        _trayIcon.Icon = Icon == null ? SystemIcons.Application : Icon;
        _trayIcon.Text = "DeepSeek Harness";
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += delegate { RestoreFromTray(); };
    }

    private void PositionTrayButton()
    {
        if (_layingOutToolbar) return;
        _layingOutToolbar = true;
        try
        {
            int previousHeight = _toolPanel.Height;
            _toolPanel.Width = ClientSize.Width;

            const int left = 14;
            const int top = 8;
            const int buttonHeight = 42;
            const int gap = 8;
            int titleWidth = Math.Max(176, TextRenderer.MeasureText(_toolbarTitle.Text, _toolbarTitle.Font).Width + 10);
            _toolbarTitle.SetBounds(left, top, titleWidth, 44);
            _statusText.SetBounds(_toolbarTitle.Right + 12, top, 180, 44);

            Button[] actions = new Button[]
            {
                _startButton, _stopButton, _refreshButton, _openBrowserButton,
                _configButton, _logButton, _exitButton
            };
            int totalWidth = 0;
            foreach (Button action in actions) totalWidth += action.Width + gap;
            if (totalWidth > 0) totalWidth -= gap;

            int trayReserve = _trayButton == null ? 0 : _trayButton.Width + 18;
            int oneRowRight = Math.Max(280, _toolPanel.ClientSize.Width - trayReserve - 12);
            int actionsStart = _statusText.Right + 14;
            int x;
            int y;
            int neededHeight;
            if (actionsStart + totalWidth <= oneRowRight)
            {
                x = actionsStart;
                y = 9;
                foreach (Button action in actions)
                {
                    action.Location = new Point(x, y);
                    action.Height = buttonHeight;
                    x += action.Width + gap;
                }
                neededHeight = 60;
            }
            else
            {
                x = left;
                y = 60;
                int right = Math.Max(120, _toolPanel.ClientSize.Width - 14);
                foreach (Button action in actions)
                {
                    if (x > left && x + action.Width > right)
                    {
                        x = left;
                        y += buttonHeight + gap;
                    }
                    action.Location = new Point(x, y);
                    action.Height = buttonHeight;
                    x += action.Width + gap;
                }
                neededHeight = y + buttonHeight + 10;
            }

            if (_toolPanel.Height != neededHeight) _toolPanel.Height = neededHeight;

            if (_trayButton != null)
            {
                _trayButton.Location = new Point(Math.Max(8, _toolPanel.ClientSize.Width - _trayButton.Width - 10), 9);
            }

            _toolbarTargetTop = _toolbarTargetVisible ? 0 : -_toolPanel.Height;
            bool wasFullyHidden = previousHeight > 0 && _toolPanel.Top <= -previousHeight + 1;
            if (!_toolbarTargetVisible && wasFullyHidden) _toolPanel.Top = -_toolPanel.Height;
        }
        finally
        {
            _layingOutToolbar = false;
        }
    }

    private void MinimizeToTray()
    {
        if (_trayIcon != null) _trayIcon.Visible = true;
        if (!_coreReady && !_exitRequested)
        {
            _trayHidePending = true;
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            return;
        }
        _trayHidePending = false;
        ShowInTaskbar = false;
        Hide();
    }

    private void RestoreFromTray()
    {
        if (_exitRequested || IsDisposed) return;
        _trayHidePending = false;
        ShowInTaskbar = true;
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        if (!_coreReady && !_webViewInitializing)
        {
            BeginInvoke((MethodInvoker)InitWebView);
        }
    }

    private void CompletePendingTrayHide()
    {
        if (!_trayHidePending || _exitRequested || IsDisposed) return;
        _trayHidePending = false;
        ShowInTaskbar = false;
        Hide();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void RevealInterface()
    {
        if (_webView == null || _webView.IsDisposed) return;
        _webView.Visible = true;
        if (_logPanel.Visible) _logPanel.BringToFront();
        if (_toolbarTargetVisible || _toolPanel.Top > -_toolPanel.Height) _toolPanel.BringToFront();
    }

    private void SetLoadingStage(string stage, float progress)
    {
        if (_loadingOverlay != null) _loadingOverlay.SetStage(stage, progress);
    }

    private void UpdateToolbarReveal()
    {
        if (_toolbarSticky || _forceToolbarVisible)
        {
            _toolbarHideTicks = 0;
            RequestToolbar(true, false);
            return;
        }

        if (!_cfg.ToolbarEdgeReveal)
        {
            _toolbarHideTicks = 0;
            RequestToolbar(false, false);
            return;
        }

        try
        {
            Point p = PointToClient(Cursor.Position);
            bool insideWidth = p.X >= 0 && p.X < ClientSize.Width;
            bool edgeHit = insideWidth && p.Y >= 0 && p.Y <= 10;
            bool revealCorridor = insideWidth && _toolbarTargetVisible &&
                p.Y >= 0 && p.Y <= _toolPanel.Height + 18;
            if (edgeHit || revealCorridor)
            {
                _toolbarHideTicks = 0;
                RequestToolbar(true, false);
            }
            else if (_toolbarTargetVisible)
            {
                _toolbarHideTicks++;
                if (_toolbarHideTicks >= 18) RequestToolbar(false, false);
            }
        }
        catch
        {
        }
    }

    private void RequestToolbar(bool visible, bool immediate)
    {
        _toolbarTargetVisible = visible;
        _toolbarTargetTop = visible ? 0 : -_toolPanel.Height;
        if (visible) _toolPanel.BringToFront();
        if (immediate) _toolPanel.Top = _toolbarTargetTop;
    }

    private void AnimateToolbar()
    {
        if (_toolPanel.Top == _toolbarTargetTop) return;
        int distance = _toolbarTargetTop - _toolPanel.Top;
        int step = Math.Max(2, (int)Math.Ceiling(Math.Abs(distance) * 0.28));
        if (Math.Abs(distance) <= step)
        {
            _toolPanel.Top = _toolbarTargetTop;
            return;
        }
        _toolPanel.Top += Math.Sign(distance) * step;
        if (_toolbarTargetVisible) _toolPanel.BringToFront();
    }

    private void ToggleToolbarSticky()
    {
        _toolbarSticky = !_toolbarSticky;
        _toolbarHideTicks = 0;
        RequestToolbar(_toolbarSticky || _forceToolbarVisible, false);
        if (_toolbarSticky) AppendLog("Toolbar pinned (hotkey toggles)");
    }
    private void ApplyLaunchMode(string mode)
    {
        Screen scr = Screen.FromControl(this);
        _fullscreen = (mode == "bordered" || mode == "borderless" || mode == "exclusive");
        _forceToolbarVisible = _fullscreen && _cfg.FullscreenShowToolbar;
        if (mode == "bordered")
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            TopMost = false;
            WindowState = FormWindowState.Maximized;
        }
        else if (mode == "borderless")
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = false;
            if (_cfg.FullscreenShowTaskbar)
            {
                WindowState = FormWindowState.Normal;
                Bounds = scr.WorkingArea;
            }
            else
            {
                WindowState = FormWindowState.Maximized;
            }
        }
        else if (mode == "exclusive")
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = !_cfg.FullscreenShowTaskbar;
            WindowState = FormWindowState.Normal;
            Bounds = _cfg.FullscreenShowTaskbar ? scr.WorkingArea : scr.Bounds;
        }
        else
        {
            ApplyWindowMode(scr);
        }
        _toolbarHideTicks = 0;
        RequestToolbar(_forceToolbarVisible || _toolbarSticky, false);
        AppendLog("Launch mode: " + mode);
    }

    private void ApplyWindowMode(Screen screen)
    {
        FormBorderStyle = FormBorderStyle.Sizable;
        TopMost = false;
        WindowState = FormWindowState.Normal;
        StartPosition = FormStartPosition.Manual;

        Size requested = new Size(Math.Max(_cfg.ResolutionWidth, 400), Math.Max(_cfg.ResolutionHeight, 300));
        Rectangle workArea = screen.WorkingArea;
        Size requestedOuter = SizeFromClientSize(requested);
        int frameWidth = Math.Max(0, requestedOuter.Width - requested.Width);
        int frameHeight = Math.Max(0, requestedOuter.Height - requested.Height);
        const int margin = 24;
        int availableClientWidth = Math.Max(400, workArea.Width - frameWidth - margin * 2);
        int availableClientHeight = Math.Max(300, workArea.Height - frameHeight - margin * 2);

        double scale = Math.Min(1.0, Math.Min(
            availableClientWidth / (double)requested.Width,
            availableClientHeight / (double)requested.Height));
        Size fitted = new Size(
            Math.Max(400, (int)Math.Floor(requested.Width * scale)),
            Math.Max(300, (int)Math.Floor(requested.Height * scale)));

        ClientSize = fitted;
        Location = new Point(
            workArea.Left + Math.Max(0, (workArea.Width - Width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - Height) / 2));

        AppendLog("Window client size: requested " + requested.Width + "x" + requested.Height +
            ", fitted " + fitted.Width + "x" + fitted.Height);
    }

    private void ToggleFullscreen()
    {
        if (_fullscreen)
        {
            _fullscreen = false;
            ApplyLaunchMode("window");
        }
        else
        {
            string mode = _cfg.LaunchMode;
            if (mode == "window") mode = "borderless";
            _fullscreen = true;
            ApplyLaunchMode(mode);
        }
    }

    private async void InitWebView()
    {
        if (_coreReady || _webViewInitializing || _exitRequested || _shuttingDown || IsDisposed || Disposing) return;
        _webViewInitializing = true;
        SetLoadingStage("Initializing browser engine", 38F);
        try
        {
            EventHandler<CoreWebView2InitializationCompletedEventArgs> initializationHandler = null;
            initializationHandler = delegate(object s, CoreWebView2InitializationCompletedEventArgs e)
            {
                _webView.CoreWebView2InitializationCompleted -= initializationHandler;
                _webViewInitializing = false;
                if (!e.IsSuccess)
                {
                    _coreReady = false;
                    if (_exitRequested || _shuttingDown || IsDisposed || Disposing)
                    {
                        return;
                    }
                    if (IsOperationAborted(e.InitializationException))
                    {
                        AppendLog("WebView2 initialization paused while the window entered the system tray");
                        SetStatus("Interface initialization paused", Color.FromArgb(180, 130, 20));
                        SetButtons();
                        return;
                    }
                    AppendLog("WebView2 init failed: " + (e.InitializationException == null ? "unknown" : e.InitializationException.Message));
                    SetStatus("Interface engine init failed", Color.FromArgb(190, 60, 60));
                    if (_loadingOverlay != null) _loadingOverlay.ShowError("Browser engine initialization failed");
                    ShowRuntimeError();
                    SetButtons();
                    return;
                }
                _coreReady = true;
                SetLoadingStage("Browser engine ready", 58F);
                PrepareWebViewBehindLoadingOverlay();
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.KeyDown += delegate(object s2, KeyEventArgs e2)
                {
                    OnWebKeyDown(e2);
                };
                _webView.CoreWebView2.NewWindowRequested += delegate(object s3, CoreWebView2NewWindowRequestedEventArgs e3)
                {
                    e3.Handled = true;
                    if (_cfg.ExternalLinksInBrowser)
                    {
                        try { Process.Start(e3.Uri); }
                        catch { }
                    }
                    else
                    {
                        _webView.CoreWebView2.Navigate(e3.Uri);
                    }
                };
                _webView.CoreWebView2.ContextMenuRequested += delegate(object s4, CoreWebView2ContextMenuRequestedEventArgs e4)
                {
                    try
                    {
                        if (e4.MenuItems.Count > 0)
                        {
                            e4.MenuItems.Add(_webView.CoreWebView2.Environment.CreateContextMenuItem(
                                "", null, CoreWebView2ContextMenuItemKind.Separator));
                        }
                        CoreWebView2ContextMenuItem configMenuItem = _webView.CoreWebView2.Environment.CreateContextMenuItem(
                            "打开 DeepSeek Harness CONFIG", null, CoreWebView2ContextMenuItemKind.Command);
                        configMenuItem.CustomItemSelected += delegate
                        {
                            try { BeginInvoke((MethodInvoker)OpenConfigApp); }
                            catch { }
                        };
                        e4.MenuItems.Add(configMenuItem);
                    }
                    catch (Exception ex)
                    {
                        AppendLog("Add CONFIG context menu failed: " + ex.Message);
                    }
                };
                _webView.NavigationCompleted += delegate(object s5, CoreWebView2NavigationCompletedEventArgs e5)
                {
                    if (e5.IsSuccess)
                    {
                        SetStatus("Service running - page loaded", Color.FromArgb(34, 139, 74));
                        AppendLog("Page loaded: " + _activeUrl);
                        if (_loadingOverlay != null) _loadingOverlay.Complete("Interface ready");
                    }
                    else
                    {
                        SetStatus("Page load failed", Color.FromArgb(190, 60, 60));
                        AppendLog("Page load failed: " + e5.WebErrorStatus);
                        if (_loadingOverlay != null) _loadingOverlay.ShowError("Web interface did not respond");
                    }
                };
                InjectHooks();
                MaybeNavigate();
                if (_trayHidePending) CompletePendingTrayHide();
                SetButtons();
            };
            _webView.CoreWebView2InitializationCompleted += initializationHandler;
            CoreWebView2Environment env = null;
            string extraArgs = BuildBrowserArgs();
            try
            {
                CoreWebView2EnvironmentOptions opts = new CoreWebView2EnvironmentOptions();
                opts.AdditionalBrowserArguments = extraArgs;
                env = await CoreWebView2Environment.CreateAsync(null, AppPaths.WebView2Dir, opts);
                SetLoadingStage("Preparing embedded browser", 48F);
            }
            catch (Exception ex)
            {
                AppendLog("WebView2 env with options failed: " + ex.Message + " - using defaults");
                env = null;
            }
            if (env == null)
            {
                await _webView.EnsureCoreWebView2Async();
            }
            else
            {
                await _webView.EnsureCoreWebView2Async(env);
            }
        }
        catch (Exception ex)
        {
            _webViewInitializing = false;
            if (_exitRequested || _shuttingDown || IsDisposed || Disposing) return;
            if (IsOperationAborted(ex))
            {
                AppendLog("WebView2 startup paused while the window entered the system tray");
                SetStatus("Interface initialization paused", Color.FromArgb(180, 130, 20));
                return;
            }
            AppendLog("WebView2 start failed: " + ex.Message);
            if (_loadingOverlay != null) _loadingOverlay.ShowError("Embedded browser could not start");
            if (!_coreReady) ShowRuntimeError();
        }
    }

    private static bool IsOperationAborted(Exception exception)
    {
        return exception != null && exception.HResult == unchecked((int)0x80004004);
    }

    private string BuildBrowserArgs()
    {
        StringBuilder sb = new StringBuilder();
        if (_cfg.EnableExtensions && _cfg.Extensions.Count > 0)
        {
            sb.Append("--enable-features=msExtensions ");
            sb.Append("--load-extension=");
            for (int i = 0; i < _cfg.Extensions.Count; i++)
            {
                sb.Append("\"");
                sb.Append(_cfg.Extensions[i]);
                sb.Append("\"");
                if (i < _cfg.Extensions.Count - 1) sb.Append(",");
            }
        }
        return sb.ToString();
    }

    private void InjectHooks()
    {
        if (_webView.CoreWebView2 == null) return;
        if (!string.IsNullOrEmpty(_cfg.InjectJs))
        {
            try
            {
                string js = File.ReadAllText(_cfg.InjectJs, Encoding.UTF8);
                _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(js);
                AppendLog("Injected JS: " + _cfg.InjectJs);
            }
            catch (Exception ex)
            {
                AppendLog("Inject JS failed: " + ex.Message);
            }
        }
        if (!string.IsNullOrEmpty(_cfg.InjectCss))
        {
            try
            {
                string css = File.ReadAllText(_cfg.InjectCss, Encoding.UTF8);
                _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(BuildCssScript(css));
                AppendLog("Injected CSS: " + _cfg.InjectCss);
            }
            catch (Exception ex)
            {
                AppendLog("Inject CSS failed: " + ex.Message);
            }
        }
    }

    private static string BuildCssScript(string css)
    {
        string escaped = css.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        return "var s=document.createElement('style');s.type='text/css';s.appendChild(document.createTextNode(\"" + escaped + "\"));document.head.appendChild(s);";
    }

    private void OnWebKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == _toolbarKey && e.Modifiers == _toolbarMods)
        {
            ToggleToolbarSticky();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == _fullscreenKey && e.Modifiers == _fullscreenMods)
        {
            ToggleFullscreen();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F12)
        {
            if (_cfg.DevTools)
            {
                try { _webView.CoreWebView2.OpenDevToolsWindow(); }
                catch { }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        else if (e.KeyCode == Keys.Escape)
        {
            if (_fullscreen)
            {
                ToggleFullscreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
    }

    private static bool TryParseHotkey(string text, out Keys key, out Keys modifiers)
    {
        key = Keys.None;
        modifiers = Keys.None;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string[] parts = text.Trim().Split(new char[] { '\n' });
        string last = parts[parts.Length - 1].Trim();
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string part = parts[i].Trim().ToLowerInvariant();
            if (part == "ctrl" || part == "control") modifiers |= Keys.Control;
            else if (part == "alt") modifiers |= Keys.Alt;
            else if (part == "shift") modifiers |= Keys.Shift;
            else return false;
        }
        try
        {
            key = (Keys)Enum.Parse(typeof(Keys), last, true);
        }
        catch
        {
            return false;
        }
        if (key == Keys.None) return false;
        return true;
    }

    private void ShowRuntimeError()
    {
        if (_runtimeErrorShown || _exitRequested || _shuttingDown || IsDisposed || Disposing) return;
        _runtimeErrorShown = true;
        MessageBox.Show(this,
            "The embedded web UI requires the Microsoft Edge WebView2 runtime." + Environment.NewLine +
            "If it is missing, install it manually and restart this app." + Environment.NewLine +
            "No browser page will be opened automatically.",
            "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
    private void StartServer()
    {
        if (_proc != null && !_proc.HasExited)
        {
            AppendLog("Server already running");
            return;
        }

        SetLoadingStage("Starting local service", 20F);
        _activePort = _cfg.Port;
        _activeUrl = _cfg.Url;
        if (IsPortOpen(_activePort))
        {
            int occupiedPort = _activePort;
            _activePort = FindAvailableLoopbackPort();
            if (_activePort <= 0)
            {
                Fail("Port " + occupiedPort + " is already in use and no free local port could be reserved.");
                return;
            }
            _activeUrl = BuildUrlForPort(_cfg.Url, _activePort);
            SetStatus("Port " + occupiedPort + " occupied - using " + _activePort, Color.FromArgb(180, 130, 20));
            AppendLog("Port " + occupiedPort + " is occupied by another service; starting an isolated desktop service on " + _activePort);
        }

        string repo = FindRepo();
        string node = FindNode(repo);
        if (repo == null)
        {
            Fail("deepseek-harness project folder not found. Set RepoPath in the Config app.");
            return;
        }
        if (node == null)
        {
            Fail("Node.js not found. Install Node.js 22.19+ or set NodePath in the Config app.");
            return;
        }
        SetLoadingStage("Preparing JavaScript runtime", 31F);
        bool sourceEntry;
        string bin = FindServerEntry(repo, out sourceEntry);
        if (bin == null)
        {
            Fail("CLI entry missing in " + repo + ". Install a Windows Runtime or run 'pnpm install && pnpm run build'.");
            return;
        }

        AppendLog("Starting server (" + node + ") ...");
        AppendLog("Working dir: " + repo);
        SetStatus("Starting...", Color.FromArgb(180, 130, 20));
        SetLoadingStage("Launching local Web UI", 46F);

        ProcessStartInfo psi = new ProcessStartInfo();
        psi.FileName = node;
        StringBuilder arguments = new StringBuilder();
        if (sourceEntry)
        {
            arguments.Append(Quote("--import")).Append(" ").Append(Quote("tsx/esm")).Append(" ");
        }
        arguments.Append(Quote(bin)).Append(" ").Append(Quote("web"));
        string desktopPatch = EnsureDesktopWebPatch();
        if (string.IsNullOrEmpty(desktopPatch))
        {
            Fail("The desktop Web UI patch could not be created. The native Node.js directory picker will not be used as a fallback.");
            return;
        }
        arguments.Append(" ").Append(Quote("--patch")).Append(" ").Append(Quote(desktopPatch));
        arguments.Append(" ").Append(Quote("--port")).Append(" ").Append(_activePort.ToString());
        psi.Arguments = arguments.ToString();
        psi.WorkingDirectory = repo;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.StandardOutputEncoding = Encoding.UTF8;
        psi.StandardErrorEncoding = Encoding.UTF8;

        _proc = new Process();
        _proc.StartInfo = psi;
        _proc.EnableRaisingEvents = true;
        _proc.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { OnOutput(e.Data); };
        _proc.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { OnOutput(e.Data); };
        _proc.Exited += delegate
        {
            BeginInvoke((MethodInvoker)delegate
            {
                if (_shuttingDown) return;
                _portReady = false;
                SetStatus("Stopped", Color.FromArgb(150, 60, 60));
                AppendLog("Server process exited, code " + _proc.ExitCode);
                if (_loadingOverlay != null) _loadingOverlay.ShowError("Local service stopped during startup");
                SetButtons();
            });
        };
        try
        {
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            SetLoadingStage("Waiting for local service", 56F);
        }
        catch (Exception ex)
        {
            Fail("Start failed: " + ex.Message);
            return;
        }

        SetButtons();

        int watchedPort = _activePort;
        string watchedUrl = _activeUrl;
        Thread watcher = new Thread(new ThreadStart(delegate { WatchPort(watchedPort, watchedUrl); }));
        watcher.IsBackground = true;
        watcher.Start();
    }

    private void WatchPort(int port, string url)
    {
        for (int i = 0; i < 180; i++)
        {
            if (_shuttingDown) return;
            if (_proc == null || _proc.HasExited) return;
            if (IsPortOpen(port))
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    _portReady = true;
                    SetLoadingStage("Connecting to Web UI", 74F);
                    SetStatus("Service running", Color.FromArgb(34, 139, 74));
                    AppendLog("Service ready: " + url);
                    MaybeNavigate();
                    SetButtons();
                });
                return;
            }
            Thread.Sleep(500);
        }
        BeginInvoke((MethodInvoker)delegate
        {
            SetStatus("Startup timeout - check log", Color.FromArgb(190, 60, 60));
            AppendLog("Waited 90s for port " + port + ". Open the log panel for details.");
            if (_loadingOverlay != null) _loadingOverlay.ShowError("Local service startup timed out");
            SetButtons();
        });
    }

    private void MaybeNavigate()
    {
        if (_coreReady && _portReady && _webView.CoreWebView2 != null)
        {
            PrepareWebViewBehindLoadingOverlay();
            SetLoadingStage("Rendering interface", 88F);
            try
            {
                string current = _webView.CoreWebView2.Source;
                if (current != _activeUrl) _webView.CoreWebView2.Navigate(_activeUrl);
            }
            catch
            {
                try { _webView.CoreWebView2.Navigate(_activeUrl); }
                catch { }
            }
        }
    }

    private void PrepareWebViewBehindLoadingOverlay()
    {
        if (_webView == null || _webView.IsDisposed) return;
        _webView.Visible = true;
        if (_loadingOverlay != null && _loadingOverlay.Visible) _loadingOverlay.BringToFront();
        if (_logPanel.Visible) _logPanel.BringToFront();
        if (_toolbarTargetVisible || _toolPanel.Top > -_toolPanel.Height) _toolPanel.BringToFront();
    }

    private void RefreshPage()
    {
        if (_coreReady && _portReady && _webView.CoreWebView2 != null)
        {
            _webView.CoreWebView2.Reload();
        }
        else
        {
            AppendLog("Service not ready yet");
        }
    }

    private void OpenInBrowser()
    {
        if (!IsPortOpen(_activePort))
        {
            AppendLog("Service not ready yet");
            return;
        }
        try
        {
            ProcessStartInfo psi = new ProcessStartInfo(_activeUrl);
            psi.UseShellExecute = true;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            AppendLog("Open browser failed: " + ex.Message);
        }
    }

    private void OpenConfigApp()
    {
        string path = Path.Combine(AppPaths.ExeDir, "dsh-config.exe");
        try
        {
            if (File.Exists(path))
            {
                ProcessStartInfo psi = new ProcessStartInfo(path);
                psi.WorkingDirectory = AppPaths.ExeDir;
                Process.Start(psi);
            }
            else
            {
                AppendLog("dsh-config.exe not found next to dsh.exe");
            }
        }
        catch (Exception ex)
        {
            AppendLog("Open config failed: " + ex.Message);
        }
    }

    private void ToggleLog()
    {
        _logPanel.Visible = !_logPanel.Visible;
    }
    private void StopServer()
    {
        if (_proc != null && !_proc.HasExited)
        {
            AppendLog("Stopping server...");
            _shuttingDown = true;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "taskkill.exe";
                psi.Arguments = "/PID " + _proc.Id + " /T /F";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process killer = Process.Start(psi);
                if (killer != null) killer.WaitForExit(5000);
            }
            catch
            {
                try { _proc.Kill(); }
                catch { }
            }
            _proc = null;
            _portReady = false;
            SetStatus("Stopped", Color.FromArgb(150, 60, 60));
            AppendLog("Server stopped");
            SetButtons();
        }
        else
        {
            _proc = null;
            SetButtons();
        }
        _shuttingDown = false;
    }

    private void OnOutput(string line)
    {
        if (line == null) return;
        AppendLog(line);
    }

    private void AppendLog(string text)
    {
        if (_logBox == null) return;
        if (_logBox.InvokeRequired)
        {
            try { _logBox.BeginInvoke((MethodInvoker)delegate { AppendLog(text); }); }
            catch { }
            return;
        }
        string stamp = DateTime.Now.ToString("HH:mm:ss");
        string line = "[" + stamp + "] " + text;
        if (_logBox.Text.Length > 60000)
        {
            _logBox.Text = _logBox.Text.Substring(_logBox.Text.Length - 30000);
        }
        _logBox.AppendText(line + Environment.NewLine);
        try
        {
            AppPaths.Ensure();
            File.AppendAllText(Path.Combine(AppPaths.LogDir, "app.log"), line + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
        }
    }

    private void SetStatus(string text, Color color)
    {
        if (_statusText.InvokeRequired)
        {
            try { _statusText.BeginInvoke((MethodInvoker)delegate { SetStatus(text, color); }); }
            catch { }
            return;
        }
        _statusText.Text = text;
        _statusText.ForeColor = color;
    }

    private void Fail(string message)
    {
        SetStatus("Startup failed", Color.FromArgb(190, 60, 60));
        AppendLog(message);
        if (_loadingOverlay != null) _loadingOverlay.ShowError("Startup failed — open Config or the toolbar log");
        MessageBox.Show(this, message, "DeepSeek Harness", MessageBoxButtons.OK, MessageBoxIcon.Error);
        SetButtons();
    }

    private void SetButtons()
    {
        bool running = _proc != null && !_proc.HasExited;
        bool portOpen = IsPortOpen(_activePort);
        _startButton.Enabled = !running && !portOpen;
        _stopButton.Enabled = running;
        _refreshButton.Enabled = portOpen && _coreReady;
        _openBrowserButton.Enabled = portOpen;
    }

    private static bool IsPortOpen(int port)
    {
        TcpClient client = new TcpClient();
        try
        {
            IAsyncResult ar = client.BeginConnect("127.0.0.1", port, null, null);
            bool done = ar.AsyncWaitHandle.WaitOne(800);
            if (!done) return false;
            client.EndConnect(ar);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { client.Close(); }
            catch { }
        }
    }

    private static int FindAvailableLoopbackPort()
    {
        TcpListener listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        catch
        {
            return 0;
        }
        finally
        {
            if (listener != null)
            {
                try { listener.Stop(); }
                catch { }
            }
        }
    }

    private static string BuildUrlForPort(string configuredUrl, int port)
    {
        Uri configured;
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out configured))
        {
            UriBuilder builder = new UriBuilder(configured);
            builder.Port = port;
            return builder.Uri.AbsoluteUri;
        }
        return "http://127.0.0.1:" + port;
    }

    private string FindRepo()
    {
        if (!string.IsNullOrEmpty(_cfg.RepoPath))
        {
            if (HasServerEntry(_cfg.RepoPath)) return Path.GetFullPath(_cfg.RepoPath);
        }
        string exeDir = AppPaths.ExeDir;
        string bundledRuntime = Path.Combine(exeDir, "runtime");
        if (HasServerEntry(bundledRuntime)) return Path.GetFullPath(bundledRuntime);
        if (HasServerEntry(exeDir)) return Path.GetFullPath(exeDir);
        if (HasServerEntry(BakedRepo)) return BakedRepo;
        return null;
    }

    private static bool HasServerEntry(string root)
    {
        bool sourceEntry;
        return FindServerEntry(root, out sourceEntry) != null;
    }

    private static string FindServerEntry(string root, out bool sourceEntry)
    {
        sourceEntry = false;
        if (string.IsNullOrEmpty(root)) return null;
        string[] builtCandidates = new string[]
        {
            Path.Combine(root, "node_modules", "@deepseek-ai", "dsh", "lib", "bin.js"),
            Path.Combine(root, "lib", "bin.js"),
            Path.Combine(root, "apps", "cli", "lib", "bin.js")
        };
        foreach (string candidate in builtCandidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        string source = Path.Combine(root, "apps", "cli", "src", "bin.ts");
        if (File.Exists(source))
        {
            sourceEntry = true;
            return source;
        }
        return null;
    }

    private string EnsureDesktopWebPatch()
    {
        const string content =
            "- id: directory-picker\r\n" +
            "  disabled: true\r\n" +
            "- insert:\r\n" +
            "    - id: directory-picker-browse\r\n" +
            "      name: '@deepseek-ai/dsh-host-directory-picker-browse'\r\n" +
            "    - id: ui-directory-picker-browse\r\n" +
            "      name: '@deepseek-ai/dsh-client-ui-directory-picker-browse'\r\n";
        try
        {
            AppPaths.Ensure();
            string path = Path.Combine(AppPaths.DataDir, "desktop-web.patch.yml");
            if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != content)
            {
                File.WriteAllText(path, content, Encoding.UTF8);
            }
            return path;
        }
        catch (Exception ex)
        {
            AppendLog("Desktop directory picker patch failed: " + ex.Message);
            return null;
        }
    }

    private string FindNode(string repo)
    {
        if (!string.IsNullOrEmpty(_cfg.NodePath))
        {
            string configured = _cfg.NodePath;
            if (!Path.IsPathRooted(configured)) configured = Path.Combine(AppPaths.ExeDir, configured);
            if (File.Exists(configured)) return Path.GetFullPath(configured);
        }
        List<string> candidates = new List<string>();
        candidates.Add(Path.Combine(AppPaths.ExeDir, "runtime", "tools", "node", "node.exe"));
        candidates.Add(Path.Combine(AppPaths.ExeDir, "tools", "node", "node.exe"));
        if (!string.IsNullOrEmpty(repo)) candidates.Add(Path.Combine(repo, "tools", "node", "node.exe"));
        candidates.AddRange(new string[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs", "node.exe")
        });
        foreach (string c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in pathVar.Split(';'))
        {
            string p;
            try { p = Path.Combine(dir, "node.exe"); }
            catch { continue; }
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string Quote(string s)
    {
        return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
