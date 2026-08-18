using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal static class ConfigProgram
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        try { SetProcessDPIAware(); }
        catch { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool firstRun = false;
        foreach (string arg in args)
        {
            if (string.Equals(arg, "--first-run", StringComparison.OrdinalIgnoreCase)) firstRun = true;
        }
        Application.Run(new ConfigForm(firstRun));
    }
}

internal sealed class LanguageDialog : Form
{
    private readonly RadioButton _chinese;
    private readonly RadioButton _english;

    public string SelectedLanguage { get; private set; }

    public LanguageDialog(string currentLanguage)
    {
        Text = "Configuration language / 配置程序语言";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(700, 430);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = Color.White;

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(38, 32, 38, 32);
        layout.ColumnCount = 1;
        layout.RowCount = 6;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        Controls.Add(layout);

        Label heading = new Label();
        heading.Text = "选择 CONFIG 语言\r\nChoose CONFIG language";
        heading.Dock = DockStyle.Fill;
        heading.Font = new Font(Font.FontFamily, 13F, FontStyle.Bold);
        heading.ForeColor = Color.FromArgb(31, 39, 49);
        heading.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(heading, 0, 0);

        Label description = new Label();
        description.Text = "This choice is stored for future launches.\r\n此选择会保存，并用于后续启动。";
        description.Dock = DockStyle.Fill;
        description.ForeColor = Color.FromArgb(96, 108, 122);
        description.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(description, 0, 1);

        _chinese = MakeLanguageOption("简体中文");
        _english = MakeLanguageOption("English");
        _chinese.Checked = currentLanguage != "en-US";
        _english.Checked = currentLanguage == "en-US";
        layout.Controls.Add(_chinese, 0, 2);
        layout.Controls.Add(_english, 0, 3);

        FlowLayoutPanel actions = new FlowLayoutPanel();
        actions.Dock = DockStyle.Fill;
        actions.FlowDirection = FlowDirection.RightToLeft;
        actions.WrapContents = false;

        Button continueButton = new Button();
        continueButton.Text = "继续  Continue";
        continueButton.Size = new Size(190, 42);
        continueButton.FlatStyle = FlatStyle.Flat;
        continueButton.FlatAppearance.BorderColor = Color.FromArgb(30, 105, 210);
        continueButton.BackColor = Color.FromArgb(30, 105, 210);
        continueButton.ForeColor = Color.White;
        continueButton.Click += delegate
        {
            SelectedLanguage = _english.Checked ? "en-US" : "zh-CN";
            DialogResult = DialogResult.OK;
            Close();
        };
        actions.Controls.Add(continueButton);
        layout.Controls.Add(actions, 0, 5);
        AcceptButton = continueButton;
    }

    private RadioButton MakeLanguageOption(string text)
    {
        RadioButton option = new RadioButton();
        option.Text = text;
        option.Dock = DockStyle.Fill;
        option.Padding = new Padding(14, 0, 0, 0);
        option.FlatStyle = FlatStyle.Flat;
        option.Font = new Font(Font.FontFamily, 10F, FontStyle.Regular);
        option.BackColor = Color.FromArgb(244, 246, 249);
        option.ForeColor = Color.FromArgb(31, 39, 49);
        option.Cursor = Cursors.Hand;
        return option;
    }
}

internal sealed class PageScrollPanel : Panel
{
    public PageScrollPanel()
    {
        AutoScroll = true;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollByWheel(e.Delta);
    }

    public void ScrollByWheel(int delta)
    {
        int current = -AutoScrollPosition.Y;
        int lines = SystemInformation.MouseWheelScrollLines;
        int step = lines < 0 ? Math.Max(1, ClientSize.Height) : Math.Max(48, lines * 24);
        int ticks = Math.Max(1, Math.Abs(delta) / SystemInformation.MouseWheelScrollDelta);
        int maximum = Math.Max(0, VerticalScroll.Maximum - VerticalScroll.LargeChange + 1);
        int target = current - Math.Sign(delta) * step * ticks;
        target = Math.Max(0, Math.Min(maximum, target));
        AutoScrollPosition = new Point(0, target);
    }

    public static void ForwardWheel(Control source, int delta)
    {
        Control current = source;
        while (current != null)
        {
            PageScrollPanel page = current as PageScrollPanel;
            if (page != null)
            {
                page.ScrollByWheel(delta);
                return;
            }
            current = current.Parent;
        }
    }
}

internal sealed class ConfigMouseWheelFilter : IMessageFilter
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    private const int MouseWheelMessage = 0x020A;
    private readonly Control _owner;

    public ConfigMouseWheelFilter(Control owner)
    {
        _owner = owner;
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (message.Msg != MouseWheelMessage || _owner.IsDisposed || !_owner.Visible) return false;

        Point cursor = Control.MousePosition;
        NativePoint nativePoint = new NativePoint();
        nativePoint.X = cursor.X;
        nativePoint.Y = cursor.Y;
        IntPtr window = WindowFromPoint(nativePoint);
        Control hovered = null;
        while (window != IntPtr.Zero && hovered == null)
        {
            hovered = Control.FromHandle(window);
            window = GetParent(window);
        }
        if (hovered == null || (hovered != _owner && !_owner.Contains(hovered))) return false;

        Control current = hovered;
        while (current != null)
        {
            PageScrollPanel page = current as PageScrollPanel;
            if (page != null && page.Visible)
            {
                int delta = unchecked((short)(message.WParam.ToInt64() >> 16));
                page.ScrollByWheel(delta);
                return true;
            }
            current = current.Parent;
        }
        return false;
    }
}

internal sealed class PageScrollComboBox : ComboBox
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        PageScrollPanel.ForwardWheel(this, e.Delta);
    }
}

internal sealed class PageScrollNumericUpDown : NumericUpDown
{
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        PageScrollPanel.ForwardWheel(this, e.Delta);
    }
}

internal sealed class ConfigForm : Form
{
    private sealed class ResolutionOption
    {
        public readonly int Width;
        public readonly int Height;
        public readonly bool IsCustom;

        public ResolutionOption(int width, int height)
        {
            Width = width;
            Height = height;
        }

        private ResolutionOption()
        {
            IsCustom = true;
        }

        public static ResolutionOption Custom()
        {
            return new ResolutionOption();
        }

        public override string ToString()
        {
            return IsCustom ? "Custom" : Width + " x " + Height;
        }
    }

    private static readonly Color ShellColor = Color.FromArgb(244, 246, 249);
    private static readonly Color PageColor = Color.White;
    private static readonly Color TextColor = Color.FromArgb(31, 39, 49);
    private static readonly Color MutedColor = Color.FromArgb(96, 108, 122);
    private static readonly Color AccentColor = Color.FromArgb(30, 105, 210);
    private static readonly Color AccentSoftColor = Color.FromArgb(229, 239, 252);
    private static readonly Color LineColor = Color.FromArgb(220, 225, 232);

    private readonly Dictionary<string, Panel> _pages = new Dictionary<string, Panel>();
    private readonly Dictionary<string, Button> _navigationButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, List<ResolutionOption>> _resolutionPresets = BuildResolutionPresets();
    private readonly ToolTip _toolTip = new ToolTip();

    private AppConfig _cfg;
    private readonly bool _firstRun;
    private bool _languagePromptShown;
    private readonly ConfigMouseWheelFilter _mouseWheelFilter;
    private Panel _pageHost;
    private Label _lblModeDescription;

    private NumericUpDown _numWidth;
    private NumericUpDown _numHeight;
    private ComboBox _comboAspectRatio;
    private ComboBox _comboResolutionPreset;
    private ComboBox _comboMode;
    private bool _updatingResolutionControls;
    private CheckBox _chkAutoHide;
    private CheckBox _chkEdgeReveal;
    private ComboBox _comboLoadingStyle;
    private ComboBox _comboCloseAction;
    private CheckBox _chkShowTrayButton;
    private CheckBox _chkFullscreenToolbar;
    private CheckBox _chkFullscreenTaskbar;
    private TextBox _txtToolbarHotkey;
    private TextBox _txtFullscreenHotkey;

    private TextBox _txtUrl;
    private NumericUpDown _numPort;
    private TextBox _txtNodePath;
    private TextBox _txtRepoPath;

    private CheckBox _chkExtensions;
    private ListBox _listExtensions;
    private TextBox _txtInjectCss;
    private TextBox _txtInjectJs;
    private CheckBox _chkDevTools;
    private CheckBox _chkExternalBrowser;

    private Label _lblDataDir;

    public ConfigForm()
        : this(false)
    {
    }

    public ConfigForm(bool firstRun)
    {
        _firstRun = firstRun;
        _cfg = AppConfig.Load();
        BuildUi();
        LoadValues();
        _mouseWheelFilter = new ConfigMouseWheelFilter(this);
        Application.AddMessageFilter(_mouseWheelFilter);
        FormClosed += delegate { Application.RemoveMessageFilter(_mouseWheelFilter); };
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }
        Shown += delegate
        {
            ShowPage("display");
            _navigationButtons["display"].Select();
            if ((_firstRun || !_cfg.FirstRunCompleted) && !_languagePromptShown)
            {
                BeginInvoke((MethodInvoker)ShowLanguagePrompt);
            }
        };
    }

    private void BuildUi()
    {
        SuspendLayout();

        Text = "DeepSeek Harness - Config";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(920, 680);
        MinimumSize = new Size(820, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        BackColor = PageColor;
        ForeColor = TextColor;
        DoubleBuffered = true;

        TableLayoutPanel shell = new TableLayoutPanel();
        shell.Dock = DockStyle.Fill;
        shell.Margin = Padding.Empty;
        shell.Padding = Padding.Empty;
        shell.ColumnCount = 2;
        shell.RowCount = 2;
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
        Controls.Add(shell);

        Panel sidebar = BuildSidebar();
        shell.Controls.Add(sidebar, 0, 0);
        shell.SetRowSpan(sidebar, 2);

        _pageHost = new Panel();
        _pageHost.Dock = DockStyle.Fill;
        _pageHost.Margin = Padding.Empty;
        _pageHost.BackColor = PageColor;
        shell.Controls.Add(_pageHost, 1, 0);

        Panel footer = BuildFooter();
        shell.Controls.Add(footer, 1, 1);

        _pages.Add("display", BuildDisplayPage());
        _pages.Add("server", BuildServerPage());
        _pages.Add("extensions", BuildExtensionsPage());

        foreach (Panel page in _pages.Values)
        {
            page.Visible = false;
            _pageHost.Controls.Add(page);
        }

        ShowPage("display");
        TranslateControlTree(this);
        ResumeLayout(true);
    }

    private void ShowLanguagePrompt()
    {
        _languagePromptShown = true;
        using (LanguageDialog dialog = new LanguageDialog(_cfg.Language))
        {
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            bool changed = _cfg.Language != dialog.SelectedLanguage;
            _cfg.Language = dialog.SelectedLanguage;
            _cfg.Save();
            if (changed) RebuildUiForLanguage();
        }
    }

    private void RebuildUiForLanguage()
    {
        SuspendLayout();
        Control[] oldControls = new Control[Controls.Count];
        Controls.CopyTo(oldControls, 0);
        Controls.Clear();
        foreach (Control control in oldControls) control.Dispose();
        _pages.Clear();
        _navigationButtons.Clear();
        BuildUi();
        LoadValues();
        ShowPage("display");
        ResumeLayout(true);
    }

    private string T(string english)
    {
        if (_cfg.Language != "zh-CN") return english;
        switch (english)
        {
            case "DeepSeek Harness - Config": return "DeepSeek Harness - 配置";
            case "CONFIGURATION": return "配置中心";
            case "Display": return "显示";
            case "Server": return "服务";
            case "Extensions & Dev": return "扩展与开发";
            case "Cancel": return "取消";
            case "Save": return "保存";
            case "Save & Launch": return "保存并启动";
            case "Browse": return "浏览";
            case "Add": return "添加";
            case "Remove": return "移除";
            case "Window size, launch mode, and keyboard controls.": return "窗口尺寸、启动模式与快捷键设置。";
            case "Window": return "窗口";
            case "Aspect ratio": return "宽高比";
            case "Resolution preset": return "分辨率预设";
            case "Custom width and height": return "自定义宽度和高度";
            case "Default launch mode": return "默认启动模式";
            case "Startup and window controls": return "启动与窗口控制";
            case "Loading screen": return "加载画面";
            case "When the title-bar X is clicked": return "点击标题栏 X 时";
            case "Show a dedicated Minimize to tray button when X exits": return "当 X 设置为退出时，显示专用的最小化到托盘按钮";
            case "Fullscreen behavior": return "全屏行为";
            case "Keep the application toolbar visible in fullscreen": return "全屏时始终显示应用工具栏";
            case "Keep the Windows taskbar visible in borderless or exclusive fullscreen": return "无边框或独占全屏时保留 Windows 任务栏";
            case "Toolbar and shortcuts": return "工具栏与快捷键";
            case "Auto-hide toolbar": return "自动隐藏工具栏";
            case "Reveal toolbar at the top screen edge": return "鼠标触及屏幕顶边时显示工具栏";
            case "Toolbar hotkey": return "工具栏快捷键";
            case "Fullscreen hotkey": return "全屏快捷键";
            case "F12 opens DevTools. Esc exits fullscreen.": return "F12 打开开发者工具，Esc 退出全屏。";
            case "Local Web UI endpoint and runtime locations.": return "本地 Web UI 地址与运行环境位置。";
            case "Connection": return "连接";
            case "Web UI URL": return "Web UI 地址";
            case "Port": return "端口";
            case "Runtime": return "运行环境";
            case "node.exe path": return "node.exe 路径";
            case "Harness folder": return "Harness 目录";
            case "Leave either path blank to use automatic detection.": return "路径留空时使用自动检测。";
            case "Browser extensions and Web UI customization files.": return "浏览器扩展与 Web UI 二次开发文件。";
            case "Browser extensions": return "浏览器扩展";
            case "Enable unpacked Edge or Chrome extensions": return "启用未打包的 Edge 或 Chrome 扩展";
            case "Web UI injection": return "Web UI 注入";
            case "CSS file": return "CSS 文件";
            case "JavaScript file": return "JavaScript 文件";
            case "Developer options": return "开发者选项";
            case "Enable F12 DevTools window": return "允许使用 F12 打开开发者工具";
            case "Open external links in the system browser": return "使用系统浏览器打开外部链接";
            case "Whales": return "双鲸环游";
            case "Progress console": return "进度控制台";
            case "Disabled": return "关闭";
            case "Minimize to tray": return "最小化到托盘";
            case "Exit application": return "退出应用";
            case "Configuration stored in portable data": return "配置保存在便携数据目录";
            case "Configuration stored in user data": return "配置保存在用户数据目录";
            case "Maximized with the standard window border.": return "使用标准窗口边框最大化显示。";
            case "Borderless fullscreen that leaves normal window priority.": return "无边框全屏，并保持普通窗口优先级。";
            case "Borderless, always on top, and covers the taskbar.": return "无边框、始终置顶并覆盖任务栏。";
            case "A normal resizable window. Oversized resolutions are fitted to the current desktop.": return "普通可缩放窗口；过大的分辨率会自动适配当前桌面。";
            default: return english;
        }
    }

    private void TranslateControlTree(Control control)
    {
        if (control is Form || control is Label || control is Button || control is CheckBox)
        {
            control.Text = T(control.Text);
        }
        foreach (Control child in control.Controls) TranslateControlTree(child);
    }

    private Panel BuildSidebar()
    {
        Panel sidebar = new Panel();
        sidebar.Dock = DockStyle.Fill;
        sidebar.Margin = Padding.Empty;
        sidebar.Padding = new Padding(16, 20, 16, 16);
        sidebar.BackColor = ShellColor;

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.Margin = Padding.Empty;
        layout.ColumnCount = 1;
        layout.RowCount = 7;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sidebar.Controls.Add(layout);

        Label brand = new Label();
        brand.Text = "DeepSeek Harness";
        brand.Dock = DockStyle.Fill;
        brand.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        brand.ForeColor = TextColor;
        brand.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(brand, 0, 0);

        Label caption = new Label();
        caption.Text = "CONFIGURATION";
        caption.Dock = DockStyle.Fill;
        caption.Font = new Font(Font.FontFamily, 8F, FontStyle.Bold);
        caption.ForeColor = MutedColor;
        caption.TextAlign = ContentAlignment.TopLeft;
        layout.Controls.Add(caption, 0, 1);

        Panel spacer = new Panel();
        spacer.Dock = DockStyle.Fill;
        spacer.Margin = Padding.Empty;
        spacer.BackColor = ShellColor;
        layout.Controls.Add(spacer, 0, 2);

        Button displayButton = MakeNavigationButton("Display", "display");
        Button serverButton = MakeNavigationButton("Server", "server");
        Button extensionsButton = MakeNavigationButton("Extensions & Dev", "extensions");
        layout.Controls.Add(displayButton, 0, 3);
        layout.Controls.Add(serverButton, 0, 4);
        layout.Controls.Add(extensionsButton, 0, 5);

        return sidebar;
    }

    private Panel BuildFooter()
    {
        Panel footer = new Panel();
        footer.Dock = DockStyle.Fill;
        footer.Margin = Padding.Empty;
        footer.Padding = new Padding(22, 12, 20, 12);
        footer.BackColor = PageColor;
        footer.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(LineColor))
            {
                e.Graphics.DrawLine(pen, 0, 0, footer.ClientSize.Width, 0);
            }
        };

        TableLayoutPanel layout = new TableLayoutPanel();
        layout.Dock = DockStyle.Fill;
        layout.Margin = Padding.Empty;
        layout.ColumnCount = 2;
        layout.RowCount = 1;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 342F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        footer.Controls.Add(layout);

        _lblDataDir = new Label();
        _lblDataDir.Dock = DockStyle.Fill;
        _lblDataDir.AutoEllipsis = true;
        _lblDataDir.ForeColor = MutedColor;
        _lblDataDir.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_lblDataDir, 0, 0);

        FlowLayoutPanel actions = new FlowLayoutPanel();
        actions.Dock = DockStyle.Fill;
        actions.Margin = Padding.Empty;
        actions.Padding = Padding.Empty;
        actions.FlowDirection = FlowDirection.RightToLeft;
        actions.WrapContents = false;
        layout.Controls.Add(actions, 1, 0);

        Button btnCancel = MakeActionButton("Cancel", false, 86);
        btnCancel.Click += delegate { Close(); };
        actions.Controls.Add(btnCancel);

        Button btnSaveLaunch = MakeActionButton("Save & Launch", true, 122);
        btnSaveLaunch.Click += delegate { SaveAndClose(true); };
        actions.Controls.Add(btnSaveLaunch);

        Button btnSave = MakeActionButton("Save", false, 86);
        btnSave.Click += delegate { SaveAndClose(false); };
        actions.Controls.Add(btnSave);

        AcceptButton = btnSave;
        CancelButton = btnCancel;
        return footer;
    }

    private Panel BuildDisplayPage()
    {
        TableLayoutPanel content;
        Panel page = CreatePage("Display", "Window size, launch mode, and keyboard controls.", out content);

        AddSectionTitle(content, "Window");
        AddFieldLabel(content, "Aspect ratio");
        _comboAspectRatio = MakeChoiceCombo(220);
        _comboAspectRatio.Items.Add("16:9");
        _comboAspectRatio.Items.Add("16:10");
        _comboAspectRatio.Items.Add("21:9");
        _comboAspectRatio.Items.Add("32:9");
        _comboAspectRatio.Items.Add("4:3");
        _comboAspectRatio.Items.Add("5:4");
        _comboAspectRatio.Items.Add("3:2");
        _comboAspectRatio.Items.Add("Custom");
        _comboAspectRatio.SelectedIndexChanged += delegate { AspectRatioChanged(); };
        AddRow(content, _comboAspectRatio);

        AddFieldLabel(content, "Resolution preset");
        _comboResolutionPreset = MakeChoiceCombo(320);
        _comboResolutionPreset.SelectedIndexChanged += delegate { ResolutionPresetChanged(); };
        AddRow(content, _comboResolutionPreset);

        AddSubFieldLabel(content, "Custom width and height");

        FlowLayoutPanel resolution = new FlowLayoutPanel();
        resolution.Dock = DockStyle.Top;
        resolution.AutoSize = true;
        resolution.Margin = new Padding(12, 0, 0, 14);
        resolution.FlowDirection = FlowDirection.LeftToRight;
        resolution.WrapContents = false;

        _numWidth = MakeNumeric(400, 7680);
        _numHeight = MakeNumeric(300, 4320);
        _numWidth.Dock = DockStyle.None;
        _numHeight.Dock = DockStyle.None;
        _numWidth.Width = 118;
        _numHeight.Width = 118;
        _numWidth.Margin = Padding.Empty;
        _numHeight.Margin = Padding.Empty;
        resolution.Controls.Add(_numWidth);

        Label multiply = new Label();
        multiply.Text = "x";
        multiply.AutoSize = true;
        multiply.Margin = new Padding(7, 4, 7, 0);
        resolution.Controls.Add(multiply);
        resolution.Controls.Add(_numHeight);
        AddRow(content, resolution);
        _numWidth.ValueChanged += delegate { CustomResolutionChanged(); };
        _numHeight.ValueChanged += delegate { CustomResolutionChanged(); };

        AddFieldLabel(content, "Default launch mode");
        _comboMode = new PageScrollComboBox();
        _comboMode.DropDownStyle = ComboBoxStyle.DropDownList;
        _comboMode.Dock = DockStyle.Left;
        _comboMode.Width = 360;
        _comboMode.Margin = new Padding(0, 0, 0, 8);
        _comboMode.Items.Add("window");
        _comboMode.Items.Add("bordered");
        _comboMode.Items.Add("borderless");
        _comboMode.Items.Add("exclusive");
        _comboMode.SelectedIndexChanged += delegate { UpdateModeDescription(); };
        AddRow(content, _comboMode);

        _lblModeDescription = MakeMutedBlock(30);
        _lblModeDescription.Margin = new Padding(0, 0, 0, 6);
        AddRow(content, _lblModeDescription);

        AddSeparator(content);
        AddSectionTitle(content, "Startup and window controls");

        AddFieldLabel(content, "Loading screen");
        _comboLoadingStyle = MakeChoiceCombo(360);
        _comboLoadingStyle.Items.Add(T("Whales"));
        _comboLoadingStyle.Items.Add(T("Progress console"));
        _comboLoadingStyle.Items.Add(T("Disabled"));
        _toolTip.SetToolTip(_comboLoadingStyle, "Whale animation, console-style progress, or no native loading screen.");
        AddRow(content, _comboLoadingStyle);

        AddFieldLabel(content, "When the title-bar X is clicked");
        _comboCloseAction = MakeChoiceCombo(360);
        _comboCloseAction.Items.Add(T("Minimize to tray"));
        _comboCloseAction.Items.Add(T("Exit application"));
        _comboCloseAction.SelectedIndexChanged += delegate { UpdateCloseActionUi(); };
        _toolTip.SetToolTip(_comboCloseAction, "Tray keeps the local service running. Exit closes the app and stops it.");
        AddRow(content, _comboCloseAction);

        _chkShowTrayButton = MakeCheckBox("Show a dedicated Minimize to tray button when X exits");
        AddRow(content, _chkShowTrayButton);

        AddSeparator(content);
        AddSectionTitle(content, "Fullscreen behavior");

        _chkFullscreenToolbar = MakeCheckBox("Keep the application toolbar visible in fullscreen");
        AddRow(content, _chkFullscreenToolbar);

        _chkFullscreenTaskbar = MakeCheckBox("Keep the Windows taskbar visible in borderless or exclusive fullscreen");
        AddRow(content, _chkFullscreenTaskbar);

        AddSeparator(content);
        AddSectionTitle(content, "Toolbar and shortcuts");

        _chkAutoHide = MakeCheckBox("Auto-hide toolbar");
        _toolTip.SetToolTip(_chkAutoHide, "Keep the toolbar hidden until the toolbar hotkey is pressed.");
        AddRow(content, _chkAutoHide);

        _chkEdgeReveal = MakeCheckBox("Reveal toolbar at the top screen edge");
        _toolTip.SetToolTip(_chkEdgeReveal, "Optional. The toolbar slides over the page without resizing the Web UI.");
        _chkAutoHide.CheckedChanged += delegate { _chkEdgeReveal.Enabled = _chkAutoHide.Checked; };
        AddRow(content, _chkEdgeReveal);

        AddFieldLabel(content, "Toolbar hotkey");
        _txtToolbarHotkey = MakeShortTextBox();
        _toolTip.SetToolTip(_txtToolbarHotkey, "Examples: F8 or Ctrl+Alt+F5");
        AddRow(content, _txtToolbarHotkey);

        AddFieldLabel(content, "Fullscreen hotkey");
        _txtFullscreenHotkey = MakeShortTextBox();
        _toolTip.SetToolTip(_txtFullscreenHotkey, "Example: F11");
        AddRow(content, _txtFullscreenHotkey);

        Label fixedKeys = MakeMutedBlock(28);
        fixedKeys.Text = "F12 opens DevTools. Esc exits fullscreen.";
        fixedKeys.Margin = new Padding(0, 4, 0, 0);
        AddRow(content, fixedKeys);
        return page;
    }

    private Panel BuildServerPage()
    {
        TableLayoutPanel content;
        Panel page = CreatePage("Server", "Local Web UI endpoint and runtime locations.", out content);

        AddSectionTitle(content, "Connection");
        AddFieldLabel(content, "Web UI URL");
        _txtUrl = MakeFullTextBox();
        AddRow(content, _txtUrl);

        AddFieldLabel(content, "Port");
        _numPort = MakeNumeric(1024, 65535);
        _numPort.Width = 160;
        _numPort.Dock = DockStyle.Left;
        AddRow(content, _numPort);

        AddSeparator(content);
        AddSectionTitle(content, "Runtime");
        AddFieldLabel(content, "node.exe path");
        _txtNodePath = MakeFullTextBox();
        Button btnNode = MakeBrowseButton();
        btnNode.Click += delegate
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "node.exe|node.exe";
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtNodePath.Text = dlg.FileName;
            }
        };
        AddRow(content, MakeBrowseRow(_txtNodePath, btnNode));

        AddFieldLabel(content, "Harness folder");
        _txtRepoPath = MakeFullTextBox();
        Button btnRepo = MakeBrowseButton();
        btnRepo.Click += delegate
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtRepoPath.Text = dlg.SelectedPath;
            }
        };
        AddRow(content, MakeBrowseRow(_txtRepoPath, btnRepo));

        Label runtimeHint = MakeMutedBlock(38);
        runtimeHint.Text = "Leave either path blank to use automatic detection.";
        runtimeHint.Margin = new Padding(0, 4, 0, 0);
        AddRow(content, runtimeHint);
        return page;
    }

    private Panel BuildExtensionsPage()
    {
        TableLayoutPanel content;
        Panel page = CreatePage("Extensions & Dev", "Browser extensions and Web UI customization files.", out content);

        AddSectionTitle(content, "Browser extensions");
        _chkExtensions = MakeCheckBox("Enable unpacked Edge or Chrome extensions");
        _toolTip.SetToolTip(_chkExtensions, "Uses WebView2 msExtensions support.");
        AddRow(content, _chkExtensions);

        TableLayoutPanel extensionRow = new TableLayoutPanel();
        extensionRow.Dock = DockStyle.Top;
        extensionRow.Height = 174;
        extensionRow.MinimumSize = new Size(0, 174);
        extensionRow.Margin = new Padding(0, 0, 0, 14);
        extensionRow.ColumnCount = 2;
        extensionRow.RowCount = 1;
        extensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        extensionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108F));
        extensionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _listExtensions = new ListBox();
        _listExtensions.Dock = DockStyle.Fill;
        _listExtensions.IntegralHeight = false;
        _listExtensions.Margin = new Padding(0, 0, 12, 0);
        extensionRow.Controls.Add(_listExtensions, 0, 0);

        FlowLayoutPanel extensionActions = new FlowLayoutPanel();
        extensionActions.Dock = DockStyle.Fill;
        extensionActions.Margin = Padding.Empty;
        extensionActions.FlowDirection = FlowDirection.TopDown;
        extensionActions.WrapContents = false;
        extensionRow.Controls.Add(extensionActions, 1, 0);

        Button btnAdd = MakeActionButton("Add", false, 96);
        btnAdd.Margin = new Padding(0, 0, 0, 8);
        btnAdd.Click += delegate
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select an unpacked extension folder containing manifest.json";
                if (dlg.ShowDialog(this) == DialogResult.OK) _listExtensions.Items.Add(dlg.SelectedPath);
            }
        };
        extensionActions.Controls.Add(btnAdd);

        Button btnRemove = MakeActionButton("Remove", false, 96);
        btnRemove.Margin = Padding.Empty;
        btnRemove.Click += delegate
        {
            if (_listExtensions.SelectedIndex >= 0) _listExtensions.Items.RemoveAt(_listExtensions.SelectedIndex);
        };
        extensionActions.Controls.Add(btnRemove);
        AddRow(content, extensionRow);

        AddSeparator(content);
        AddSectionTitle(content, "Web UI injection");
        AddFieldLabel(content, "CSS file");
        _txtInjectCss = MakeFullTextBox();
        Button btnCss = MakeBrowseButton();
        btnCss.Click += delegate
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "CSS|*.css|All files|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtInjectCss.Text = dlg.FileName;
            }
        };
        AddRow(content, MakeBrowseRow(_txtInjectCss, btnCss));

        AddFieldLabel(content, "JavaScript file");
        _txtInjectJs = MakeFullTextBox();
        Button btnJs = MakeBrowseButton();
        btnJs.Click += delegate
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Filter = "JavaScript|*.js|All files|*.*";
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtInjectJs.Text = dlg.FileName;
            }
        };
        AddRow(content, MakeBrowseRow(_txtInjectJs, btnJs));

        AddSeparator(content);
        AddSectionTitle(content, "Developer options");
        _chkDevTools = MakeCheckBox("Enable F12 DevTools window");
        AddRow(content, _chkDevTools);
        _chkExternalBrowser = MakeCheckBox("Open external links in the system browser");
        AddRow(content, _chkExternalBrowser);
        return page;
    }

    private static Dictionary<string, List<ResolutionOption>> BuildResolutionPresets()
    {
        Dictionary<string, List<ResolutionOption>> presets = new Dictionary<string, List<ResolutionOption>>();
        presets.Add("16:9", new List<ResolutionOption>
        {
            new ResolutionOption(1280, 720),
            new ResolutionOption(1366, 768),
            new ResolutionOption(1600, 900),
            new ResolutionOption(1920, 1080),
            new ResolutionOption(2560, 1440),
            new ResolutionOption(3840, 2160)
        });
        presets.Add("16:10", new List<ResolutionOption>
        {
            new ResolutionOption(1280, 800),
            new ResolutionOption(1440, 900),
            new ResolutionOption(1680, 1050),
            new ResolutionOption(1920, 1200),
            new ResolutionOption(2560, 1600),
            new ResolutionOption(3840, 2400)
        });
        presets.Add("21:9", new List<ResolutionOption>
        {
            new ResolutionOption(2560, 1080),
            new ResolutionOption(3440, 1440),
            new ResolutionOption(3840, 1600),
            new ResolutionOption(5120, 2160)
        });
        presets.Add("32:9", new List<ResolutionOption>
        {
            new ResolutionOption(3840, 1080),
            new ResolutionOption(5120, 1440),
            new ResolutionOption(7680, 2160)
        });
        presets.Add("4:3", new List<ResolutionOption>
        {
            new ResolutionOption(1024, 768),
            new ResolutionOption(1280, 960),
            new ResolutionOption(1600, 1200),
            new ResolutionOption(2048, 1536)
        });
        presets.Add("5:4", new List<ResolutionOption>
        {
            new ResolutionOption(1280, 1024),
            new ResolutionOption(2560, 2048)
        });
        presets.Add("3:2", new List<ResolutionOption>
        {
            new ResolutionOption(1440, 960),
            new ResolutionOption(1920, 1280),
            new ResolutionOption(2160, 1440),
            new ResolutionOption(3000, 2000)
        });
        return presets;
    }

    private Panel CreatePage(string title, string subtitle, out TableLayoutPanel content)
    {
        Panel page = new PageScrollPanel();
        page.Dock = DockStyle.Fill;
        page.Margin = Padding.Empty;
        page.BackColor = PageColor;

        content = new TableLayoutPanel();
        content.Dock = DockStyle.Top;
        content.AutoSize = true;
        content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        content.Margin = Padding.Empty;
        content.Padding = new Padding(30, 20, 30, 6);
        content.ColumnCount = 1;
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        content.BackColor = PageColor;
        page.Controls.Add(content);

        Label heading = new Label();
        heading.Text = title;
        heading.UseMnemonic = false;
        heading.AutoSize = true;
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 4);
        heading.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
        heading.ForeColor = TextColor;
        AddRow(content, heading);

        Label description = MakeMutedBlock(28);
        description.Text = subtitle;
        description.Margin = new Padding(0, 0, 0, 2);
        AddRow(content, description);
        return page;
    }

    private Button MakeNavigationButton(string text, string key)
    {
        Button button = new Button();
        button.Text = text;
        button.UseMnemonic = false;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(0, 2, 0, 2);
        button.Padding = new Padding(14, 0, 8, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = ShellColor;
        button.ForeColor = TextColor;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Cursor = Cursors.Hand;
        button.Click += delegate { ShowPage(key); };
        _navigationButtons.Add(key, button);
        return button;
    }

    private Button MakeActionButton(string text, bool primary, int width)
    {
        Button button = new Button();
        button.Text = text;
        button.UseMnemonic = false;
        button.Size = new Size(width, 36);
        button.Margin = new Padding(6, 0, 0, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = primary ? AccentColor : LineColor;
        button.BackColor = primary ? AccentColor : PageColor;
        button.ForeColor = primary ? Color.White : TextColor;
        button.Cursor = Cursors.Hand;
        return button;
    }

    private Button MakeBrowseButton()
    {
        Button button = MakeActionButton("Browse", false, 96);
        button.Dock = DockStyle.Fill;
        button.Margin = Padding.Empty;
        return button;
    }

    private NumericUpDown MakeNumeric(int min, int max)
    {
        NumericUpDown numeric = new PageScrollNumericUpDown();
        numeric.Dock = DockStyle.Fill;
        numeric.Minimum = min;
        numeric.Maximum = max;
        numeric.Margin = Padding.Empty;
        numeric.ThousandsSeparator = true;
        return numeric;
    }

    private ComboBox MakeChoiceCombo(int width)
    {
        ComboBox comboBox = new PageScrollComboBox();
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.Dock = DockStyle.Left;
        comboBox.Width = width;
        comboBox.Margin = new Padding(0, 0, 0, 12);
        return comboBox;
    }

    private TextBox MakeFullTextBox()
    {
        TextBox textBox = new TextBox();
        textBox.Dock = DockStyle.Fill;
        textBox.Margin = new Padding(0, 0, 0, 12);
        return textBox;
    }

    private TextBox MakeShortTextBox()
    {
        TextBox textBox = new TextBox();
        textBox.Dock = DockStyle.Left;
        textBox.Width = 190;
        textBox.Margin = new Padding(0, 0, 0, 12);
        return textBox;
    }

    private CheckBox MakeCheckBox(string text)
    {
        CheckBox checkBox = new CheckBox();
        checkBox.Text = text;
        checkBox.Dock = DockStyle.Top;
        checkBox.AutoSize = true;
        checkBox.Margin = new Padding(0, 0, 0, 12);
        checkBox.ForeColor = TextColor;
        return checkBox;
    }

    private TableLayoutPanel MakeBrowseRow(TextBox textBox, Button button)
    {
        textBox.Margin = new Padding(0, 0, 12, 0);

        TableLayoutPanel row = new TableLayoutPanel();
        row.Dock = DockStyle.Top;
        row.AutoSize = true;
        row.Margin = new Padding(0, 0, 0, 14);
        row.ColumnCount = 2;
        row.RowCount = 1;
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        row.Controls.Add(textBox, 0, 0);
        row.Controls.Add(button, 1, 0);
        return row;
    }

    private void AddSectionTitle(TableLayoutPanel content, string text)
    {
        Label label = new Label();
        label.Text = text;
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 6, 0, 10);
        label.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        label.ForeColor = TextColor;
        AddRow(content, label);
    }

    private void AddFieldLabel(TableLayoutPanel content, string text)
    {
        Label label = new Label();
        label.Text = text;
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 0, 0, 5);
        label.ForeColor = TextColor;
        AddRow(content, label);
    }

    private void AddSubFieldLabel(TableLayoutPanel content, string text)
    {
        Label label = new Label();
        label.Text = text;
        label.AutoSize = true;
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(12, 2, 0, 5);
        label.ForeColor = MutedColor;
        AddRow(content, label);
    }

    private Label MakeMutedBlock(int height)
    {
        Label label = new Label();
        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.Height = height;
        label.MinimumSize = new Size(0, height);
        label.ForeColor = MutedColor;
        label.TextAlign = ContentAlignment.TopLeft;
        return label;
    }

    private void AddSeparator(TableLayoutPanel content)
    {
        Panel line = new Panel();
        line.Dock = DockStyle.Top;
        line.Height = 1;
        line.MinimumSize = new Size(0, 1);
        line.Margin = new Padding(0, 8, 0, 8);
        line.BackColor = LineColor;
        AddRow(content, line);
    }

    private void AddRow(TableLayoutPanel content, Control control)
    {
        int row = content.RowCount;
        content.RowCount++;
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.Controls.Add(control, 0, row);
    }

    private void ShowPage(string key)
    {
        foreach (KeyValuePair<string, Panel> item in _pages)
        {
            item.Value.Visible = item.Key == key;
        }

        foreach (KeyValuePair<string, Button> item in _navigationButtons)
        {
            bool selected = item.Key == key;
            item.Value.BackColor = selected ? AccentSoftColor : ShellColor;
            item.Value.ForeColor = selected ? AccentColor : TextColor;
            item.Value.Font = new Font(Font.FontFamily, Font.Size, selected ? FontStyle.Bold : FontStyle.Regular);
        }

        Panel selectedPage;
        if (_pages.TryGetValue(key, out selectedPage))
        {
            selectedPage.AutoScrollPosition = Point.Empty;
            selectedPage.BringToFront();
        }
    }

    private void AspectRatioChanged()
    {
        if (_updatingResolutionControls || _comboAspectRatio.SelectedItem == null) return;

        string ratio = _comboAspectRatio.SelectedItem.ToString();
        int width = (int)_numWidth.Value;
        int height = (int)_numHeight.Value;

        _updatingResolutionControls = true;
        ResolutionOption selected = PopulateResolutionPresetItems(ratio, width, height);
        if (ratio != "Custom" && selected.IsCustom && FindAspectRatio(width, height) != ratio)
        {
            selected = (ResolutionOption)_comboResolutionPreset.Items[0];
            _comboResolutionPreset.SelectedItem = selected;
            ApplyResolution(selected);
        }
        _updatingResolutionControls = false;
    }

    private void ResolutionPresetChanged()
    {
        if (_updatingResolutionControls) return;
        ResolutionOption option = _comboResolutionPreset.SelectedItem as ResolutionOption;
        if (option == null || option.IsCustom) return;

        _updatingResolutionControls = true;
        ApplyResolution(option);
        _updatingResolutionControls = false;
    }

    private void CustomResolutionChanged()
    {
        if (_updatingResolutionControls) return;
        SyncResolutionSelectorsFromNumbers();
    }

    private void SyncResolutionSelectorsFromNumbers()
    {
        int width = (int)_numWidth.Value;
        int height = (int)_numHeight.Value;
        string ratio = FindAspectRatio(width, height);

        _updatingResolutionControls = true;
        _comboAspectRatio.SelectedItem = ratio;
        PopulateResolutionPresetItems(ratio, width, height);
        _updatingResolutionControls = false;
    }

    private ResolutionOption PopulateResolutionPresetItems(string ratio, int width, int height)
    {
        _comboResolutionPreset.BeginUpdate();
        _comboResolutionPreset.Items.Clear();

        ResolutionOption selected = null;
        List<ResolutionOption> options;
        if (_resolutionPresets.TryGetValue(ratio, out options))
        {
            foreach (ResolutionOption option in options)
            {
                _comboResolutionPreset.Items.Add(option);
                if (option.Width == width && option.Height == height) selected = option;
            }
        }

        ResolutionOption custom = ResolutionOption.Custom();
        _comboResolutionPreset.Items.Add(custom);
        if (selected == null) selected = custom;
        _comboResolutionPreset.SelectedItem = selected;
        _comboResolutionPreset.EndUpdate();
        return selected;
    }

    private string FindAspectRatio(int width, int height)
    {
        string[] ratios = new string[] { "16:9", "16:10", "21:9", "32:9", "4:3", "5:4", "3:2" };
        foreach (string ratio in ratios)
        {
            List<ResolutionOption> options = _resolutionPresets[ratio];
            foreach (ResolutionOption option in options)
            {
                if (option.Width == width && option.Height == height) return ratio;
            }
        }

        if (width * 9 == height * 16) return "16:9";
        if (width * 10 == height * 16) return "16:10";
        if (width * 9 == height * 21) return "21:9";
        if (width * 9 == height * 32) return "32:9";
        if (width * 3 == height * 4) return "4:3";
        if (width * 4 == height * 5) return "5:4";
        if (width * 2 == height * 3) return "3:2";
        return "Custom";
    }

    private void ApplyResolution(ResolutionOption option)
    {
        _numWidth.Value = Math.Max(_numWidth.Minimum, Math.Min(_numWidth.Maximum, option.Width));
        _numHeight.Value = Math.Max(_numHeight.Minimum, Math.Min(_numHeight.Maximum, option.Height));
    }

    private void UpdateModeDescription()
    {
        if (_lblModeDescription == null || _comboMode.SelectedItem == null) return;

        switch (_comboMode.SelectedItem.ToString())
        {
            case "bordered":
                _lblModeDescription.Text = T("Maximized with the standard window border.");
                break;
            case "borderless":
                _lblModeDescription.Text = T("Borderless fullscreen that leaves normal window priority.");
                break;
            case "exclusive":
                _lblModeDescription.Text = T("Borderless, always on top, and covers the taskbar.");
                break;
            default:
                _lblModeDescription.Text = T("A normal resizable window. Oversized resolutions are fitted to the current desktop.");
                break;
        }
    }

    private void UpdateCloseActionUi()
    {
        if (_chkShowTrayButton == null || _comboCloseAction == null) return;
        bool exits = _comboCloseAction.SelectedIndex == 1;
        _chkShowTrayButton.Visible = exits;
    }

    private void LoadValues()
    {
        _updatingResolutionControls = true;
        _numWidth.Value = Math.Max(_numWidth.Minimum, Math.Min(_numWidth.Maximum, _cfg.ResolutionWidth));
        _numHeight.Value = Math.Max(_numHeight.Minimum, Math.Min(_numHeight.Maximum, _cfg.ResolutionHeight));
        _updatingResolutionControls = false;
        SyncResolutionSelectorsFromNumbers();
        int idx = _comboMode.Items.IndexOf(_cfg.LaunchMode);
        _comboMode.SelectedIndex = idx >= 0 ? idx : 0;
        _chkAutoHide.Checked = _cfg.ToolbarAutoHide;
        _chkEdgeReveal.Checked = _cfg.ToolbarEdgeReveal;
        _chkEdgeReveal.Enabled = _chkAutoHide.Checked;
        _comboLoadingStyle.SelectedIndex = _cfg.LoadingStyle == "progress" ? 1 : (_cfg.LoadingStyle == "off" ? 2 : 0);
        _comboCloseAction.SelectedIndex = _cfg.CloseAction == "exit" ? 1 : 0;
        _chkShowTrayButton.Checked = _cfg.ShowTrayButton;
        _chkFullscreenToolbar.Checked = _cfg.FullscreenShowToolbar;
        _chkFullscreenTaskbar.Checked = _cfg.FullscreenShowTaskbar;
        UpdateCloseActionUi();
        _txtToolbarHotkey.Text = _cfg.ToolbarHotkey;
        _txtFullscreenHotkey.Text = _cfg.FullscreenHotkey;
        _txtUrl.Text = _cfg.Url;
        _numPort.Value = Math.Max(_numPort.Minimum, Math.Min(_numPort.Maximum, _cfg.Port));
        _txtNodePath.Text = _cfg.NodePath;
        _txtRepoPath.Text = _cfg.RepoPath;
        _chkExtensions.Checked = _cfg.EnableExtensions;
        _listExtensions.Items.Clear();
        foreach (string ext in _cfg.Extensions)
        {
            _listExtensions.Items.Add(ext);
        }
        _txtInjectCss.Text = _cfg.InjectCss;
        _txtInjectJs.Text = _cfg.InjectJs;
        _chkDevTools.Checked = _cfg.DevTools;
        _chkExternalBrowser.Checked = _cfg.ExternalLinksInBrowser;
        _lblDataDir.Text = AppPaths.IsPortable ? T("Configuration stored in portable data") : T("Configuration stored in user data");
        _toolTip.SetToolTip(_lblDataDir, AppPaths.ConfigFile);
        UpdateModeDescription();
    }

    private void CollectValues()
    {
        _cfg.ResolutionWidth = (int)_numWidth.Value;
        _cfg.ResolutionHeight = (int)_numHeight.Value;
        _cfg.LaunchMode = _comboMode.SelectedItem == null ? "window" : _comboMode.SelectedItem.ToString();
        _cfg.ToolbarAutoHide = _chkAutoHide.Checked;
        _cfg.ToolbarEdgeReveal = _chkEdgeReveal.Checked;
        _cfg.LoadingStyle = _comboLoadingStyle.SelectedIndex == 1 ? "progress" : (_comboLoadingStyle.SelectedIndex == 2 ? "off" : "whales");
        _cfg.CloseAction = _comboCloseAction.SelectedIndex == 1 ? "exit" : "tray";
        _cfg.ShowTrayButton = _chkShowTrayButton.Checked;
        _cfg.FullscreenShowToolbar = _chkFullscreenToolbar.Checked;
        _cfg.FullscreenShowTaskbar = _chkFullscreenTaskbar.Checked;
        _cfg.ToolbarHotkey = _txtToolbarHotkey.Text.Trim();
        _cfg.FullscreenHotkey = _txtFullscreenHotkey.Text.Trim();
        _cfg.Url = _txtUrl.Text.Trim();
        _cfg.Port = (int)_numPort.Value;
        _cfg.NodePath = _txtNodePath.Text.Trim();
        _cfg.RepoPath = _txtRepoPath.Text.Trim();
        _cfg.EnableExtensions = _chkExtensions.Checked;
        _cfg.Extensions = new List<string>();
        foreach (object item in _listExtensions.Items)
        {
            _cfg.Extensions.Add(item.ToString());
        }
        _cfg.InjectCss = _txtInjectCss.Text.Trim();
        _cfg.InjectJs = _txtInjectJs.Text.Trim();
        _cfg.DevTools = _chkDevTools.Checked;
        _cfg.ExternalLinksInBrowser = _chkExternalBrowser.Checked;
    }

    private void SaveAndClose(bool launch)
    {
        try
        {
            CollectValues();
            _cfg.FirstRunCompleted = true;
            _cfg.Save();
            if (launch)
            {
                string main = Path.Combine(AppPaths.ExeDir, "dsh.exe");
                if (File.Exists(main))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(main);
                    psi.WorkingDirectory = AppPaths.ExeDir;
                    Process.Start(psi);
                }
            }
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, (_cfg.Language == "zh-CN" ? "保存失败：" : "Save failed: ") + ex.Message,
                _cfg.Language == "zh-CN" ? "DeepSeek Harness 配置" : "DeepSeek Harness Config",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
