using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using SentinelKiosk.Runtime.Models;
using SentinelKiosk.Runtime.Services;
using Serilog;

namespace SentinelKiosk.Runtime;

public partial class MainWindow : Window
{
    private readonly KioskConfiguration _config;
    private readonly NavigationGuard _navigationGuard;
    private readonly SessionManager _sessionManager;
    private readonly CrashMonitor _crashMonitor;
    private readonly PolicyReceiver _policyReceiver;
    private readonly ContentReceiver _contentReceiver;
    private bool _isInitialized;

    public MainWindow(KioskConfiguration config)
    {
        _config = config;
        _navigationGuard = new NavigationGuard(config);
        _sessionManager = new SessionManager(config, ResetToHome);
        _crashMonitor = new CrashMonitor(config, RestartApplication);
        _policyReceiver = new PolicyReceiver(config, OnPolicyUpdated);
        _contentReceiver = new ContentReceiver(OnContentActivated);

        InitializeComponent();
        InitializeWebView();

        // Block common escape hotkeys
        PreviewKeyDown += OnPreviewKeyDown;

        // Track user activity
        PreviewMouseMove += (s, e) => _sessionManager.ResetInactivityTimer();
        PreviewKeyDown += (s, e) => _sessionManager.ResetInactivityTimer();
    }

    private async void InitializeWebView()
    {
        try
        {
            Log.Information("Initializing WebView2...");

            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SentinelKiosk", "WebView2Data"));

            await WebView.EnsureCoreWebView2Async(env);

            // Configure WebView2 settings
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = _config.AllowContextMenus;
            WebView.CoreWebView2.Settings.AreDevToolsEnabled = _config.AllowDevTools;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

            // Navigation events
            WebView.CoreWebView2.NavigationStarting += OnNavigationStarting;
            WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            WebView.CoreWebView2.ProcessFailed += OnProcessFailed;

            // New window requests (popups)
            WebView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            // Permission requests
            WebView.CoreWebView2.PermissionRequested += OnPermissionRequested;

            // Download handling
            WebView.CoreWebView2.DownloadStarting += OnDownloadStarting;

            _isInitialized = true;

            // Navigate to home URL
            NavigateToHome();

            // Start session management
            _sessionManager.Start();

            // Start policy receiver
            _policyReceiver.Start();

            // Start content receiver
            _contentReceiver.Start();

            Log.Information("WebView2 initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize WebView2");
            _crashMonitor.HandleCrash("WebView2 initialization failed", ex);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var uri = e.Uri;
        Log.Debug("Navigation starting: {Uri}", uri);

        if (!_navigationGuard.IsAllowed(uri))
        {
            Log.Warning("Navigation blocked: {Uri}", uri);
            e.Cancel = true;

            // Show blocked message or redirect to home
            NavigateToHome();
        }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            Log.Debug("Navigation completed: {Uri}", WebView.Source);
        }
        else
        {
            Log.Warning("Navigation failed: {Error}", e.WebErrorStatus);
        }
    }

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Log.Error("WebView2 process failed: {Kind}", e.ProcessFailedKind);
        _crashMonitor.HandleCrash($"Process failed: {e.ProcessFailedKind}", null);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Block popups unless explicitly allowed
        if (!_config.AllowPopups)
        {
            e.Handled = true;
            Log.Debug("Popup blocked: {Uri}", e.Uri);
        }
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        // Deny all permission requests by default in kiosk mode
        e.State = CoreWebView2PermissionState.Deny;
        Log.Debug("Permission denied: {Kind}", e.PermissionKind);
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        // Block downloads in kiosk mode
        e.Cancel = true;
        Log.Debug("Download blocked: {Uri}", e.DownloadOperation.Uri);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Block common escape sequences unless in maintenance mode
        if (!_config.MaintenanceModeEnabled)
        {
            var blocked = e.Key switch
            {
                Key.F4 when (Keyboard.Modifiers & ModifierKeys.Alt) != 0 => true,  // Alt+F4
                Key.Tab when (Keyboard.Modifiers & ModifierKeys.Alt) != 0 => true, // Alt+Tab
                Key.Escape when (Keyboard.Modifiers & ModifierKeys.Control) != 0 => true, // Ctrl+Esc
                Key.LWin => true,  // Windows key
                Key.RWin => true,  // Right Windows key
                _ => false
            };

            if (blocked)
            {
                e.Handled = true;
                Log.Debug("Key blocked: {Key}", e.Key);
            }
        }
    }

    public void NavigateToHome()
    {
        if (_isInitialized && !string.IsNullOrEmpty(_config.HomeUrl))
        {
            WebView.Source = new Uri(_config.HomeUrl);
            Log.Information("Navigated to home: {HomeUrl}", _config.HomeUrl);
        }
    }

    private void ResetToHome()
    {
        // Clear cache and session data if configured
        if (_config.ClearSessionOnReset)
        {
            WebView.CoreWebView2?.Profile.ClearBrowsingDataAsync();
        }

        NavigateToHome();
    }

    private void RestartApplication()
    {
        Log.Information("Restarting application...");
        System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName!);
        Application.Current.Shutdown();
    }

    private void OnPolicyUpdated(KioskConfiguration newConfig)
    {
        Log.Information("Policy updated, applying changes...");

        // Update configuration
        _config.HomeUrl = newConfig.HomeUrl;
        _config.SessionTimeoutSeconds = newConfig.SessionTimeoutSeconds;
        _config.InactivityTimeoutSeconds = newConfig.InactivityTimeoutSeconds;

        // Apply navigation guard changes
        _navigationGuard.UpdateConfiguration(newConfig);

        // Apply session manager changes
        _sessionManager.UpdateConfiguration(newConfig);

        // Navigate to new home if changed
        if (WebView.Source?.ToString() != newConfig.HomeUrl)
        {
            NavigateToHome();
        }
        }

        private void OnContentActivated(ContentActivatedMessage message)
        {
        Dispatcher.Invoke(() =>
        {
            Log.Information("Content activated: {ContentId}, navigating to {MainFile}", message.ContentId, message.MainFile);

            // Determine the URL to navigate to
            string url;
            if (File.Exists(message.MainFile))
            {
                // Local file — use file:// URI
                url = new Uri(message.MainFile).AbsoluteUri;
            }
            else if (message.MainFile.StartsWith("http"))
            {
                // Remote URL
                url = message.MainFile;
            }
            else if (Directory.Exists(message.ContentPath))
            {
                // Directory — look for index.html
                var indexPath = Path.Combine(message.ContentPath, "index.html");
                url = File.Exists(indexPath)
                    ? new Uri(indexPath).AbsoluteUri
                    : new Uri(message.ContentPath).AbsoluteUri;
            }
            else
            {
                Log.Warning("Content path not found: {Path}", message.MainFile);
                return;
            }

            // Update config home URL so session reset goes to new content
            _config.HomeUrl = url;

            // Navigate WebView2
            if (_isInitialized)
            {
                WebView.Source = new Uri(url);
                Log.Information("Navigated to deployed content: {Url}", url);
            }
        });
        }

        protected override void OnClosed(EventArgs e)
        {
        _sessionManager.Stop();
        _policyReceiver.Stop();
        _contentReceiver.Stop();
        base.OnClosed(e);
        }
        }
