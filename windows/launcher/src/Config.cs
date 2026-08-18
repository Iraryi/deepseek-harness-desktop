using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

internal static class AppPaths
{
    public static string ExeDir
    {
        get { return AppDomain.CurrentDomain.BaseDirectory; }
    }

    public static bool IsPortable
    {
        get { return File.Exists(Path.Combine(ExeDir, "portable.mode")); }
    }

    public static string DataDir
    {
        get
        {
            if (IsPortable)
            {
                return Path.Combine(ExeDir, "data");
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekHarness");
        }
    }

    public static string ConfigFile
    {
        get { return Path.Combine(DataDir, "config.json"); }
    }

    public static string LogDir
    {
        get { return Path.Combine(DataDir, "logs"); }
    }

    public static string WebView2Dir
    {
        get { return Path.Combine(DataDir, "WebView2"); }
    }

    public static void Ensure()
    {
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(WebView2Dir);
    }
}

internal sealed class AppConfig
{
    public int ResolutionWidth { get; set; }
    public int ResolutionHeight { get; set; }
    public string Language { get; set; }
    public bool FirstRunCompleted { get; set; }
    public string LaunchMode { get; set; }
    public string Url { get; set; }
    public int Port { get; set; }
    public string NodePath { get; set; }
    public string RepoPath { get; set; }
    public bool ToolbarAutoHide { get; set; }
    public bool ToolbarEdgeReveal { get; set; }
    public string ToolbarHotkey { get; set; }
    public string FullscreenHotkey { get; set; }
    public string LoadingStyle { get; set; }
    public string CloseAction { get; set; }
    public bool ShowTrayButton { get; set; }
    public bool FullscreenShowToolbar { get; set; }
    public bool FullscreenShowTaskbar { get; set; }
    public bool EnableExtensions { get; set; }
    public List<string> Extensions { get; set; }
    public string InjectCss { get; set; }
    public string InjectJs { get; set; }
    public bool DevTools { get; set; }
    public bool ExternalLinksInBrowser { get; set; }

    public AppConfig()
    {
        ResolutionWidth = 1280;
        ResolutionHeight = 800;
        Language = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        FirstRunCompleted = false;
        LaunchMode = "window";
        Url = "http://127.0.0.1:3080";
        Port = 3080;
        NodePath = "";
        RepoPath = "";
        ToolbarAutoHide = true;
        ToolbarEdgeReveal = false;
        ToolbarHotkey = "F8";
        FullscreenHotkey = "F11";
        LoadingStyle = "whales";
        CloseAction = "tray";
        ShowTrayButton = true;
        FullscreenShowToolbar = false;
        FullscreenShowTaskbar = false;
        EnableExtensions = false;
        Extensions = new List<string>();
        InjectCss = "";
        InjectJs = "";
        DevTools = true;
        ExternalLinksInBrowser = true;
    }

    public static AppConfig Load()
    {
        AppPaths.Ensure();
        AppConfig cfg = null;
        bool existingConfig = false;
        bool firstRunFieldPresent = false;
        bool languageFieldPresent = false;
        try
        {
            if (File.Exists(AppPaths.ConfigFile))
            {
                existingConfig = true;
                string json = File.ReadAllText(AppPaths.ConfigFile, Encoding.UTF8);
                firstRunFieldPresent = json.IndexOf("\"FirstRunCompleted\"", StringComparison.OrdinalIgnoreCase) >= 0;
                languageFieldPresent = json.IndexOf("\"Language\"", StringComparison.OrdinalIgnoreCase) >= 0;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                cfg = ser.Deserialize<AppConfig>(json);
            }
        }
        catch
        {
            cfg = null;
        }
        if (cfg == null)
        {
            cfg = new AppConfig();
        }
        if (cfg.Extensions == null) cfg.Extensions = new List<string>();
        if (existingConfig && !languageFieldPresent) cfg.Language = "en-US";
        if (cfg.Language != "zh-CN" && cfg.Language != "en-US")
        {
            cfg.Language = CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh-CN" : "en-US";
        }
        if (existingConfig && !firstRunFieldPresent) cfg.FirstRunCompleted = true;
        if (string.IsNullOrEmpty(cfg.LaunchMode)) cfg.LaunchMode = "window";
        if (string.IsNullOrEmpty(cfg.Url)) cfg.Url = "http://127.0.0.1:3080";
        if (cfg.Port <= 0 || cfg.Port > 65535) cfg.Port = 3080;
        if (cfg.ResolutionWidth < 400) cfg.ResolutionWidth = 400;
        if (cfg.ResolutionHeight < 300) cfg.ResolutionHeight = 300;
        if (string.IsNullOrEmpty(cfg.ToolbarHotkey)) cfg.ToolbarHotkey = "F8";
        if (string.IsNullOrEmpty(cfg.FullscreenHotkey)) cfg.FullscreenHotkey = "F11";
        if (cfg.LoadingStyle != "whales" && cfg.LoadingStyle != "progress" && cfg.LoadingStyle != "off") cfg.LoadingStyle = "whales";
        if (cfg.CloseAction != "tray" && cfg.CloseAction != "exit") cfg.CloseAction = "tray";
        return cfg;
    }

    public void Save()
    {
        AppPaths.Ensure();
        JavaScriptSerializer ser = new JavaScriptSerializer();
        string json = ser.Serialize(this);
        File.WriteAllText(AppPaths.ConfigFile, json, Encoding.UTF8);
    }
}
