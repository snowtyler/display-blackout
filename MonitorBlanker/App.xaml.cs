using Microsoft.UI;
using WinUIEx;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MonitorBlanker.Services;

namespace MonitorBlanker;

public sealed partial class App : Application, IDisposable
{
    private TrayIcon? _trayIcon;
    private MainWindow? _settingsWindow;
    private Window? _hiddenWindow;
    private BlankingService? _blankingService;
    private HotkeyService? _hotkeyService;
    private GameModeService? _gameModeService;
    private bool _disposed;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool openSettings = Environment.GetCommandLineArgs()
            .Skip(1) // Skip executable path
            .Any(arg => arg.Equals("/OpenSettings", StringComparison.OrdinalIgnoreCase));

        _blankingService = new BlankingService();

        // Create a hidden window for hotkey messages
        _hiddenWindow = new Window { Title = "MonitorBlankerHidden" };

        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => ToggleBlanking();
        _hotkeyService.Register(_hiddenWindow);

        _gameModeService = new GameModeService();
        _gameModeService.GameModeChanged += OnGameModeChanged;
        // TODO: Game mode detection disabled - triggers unreliably. Revisit in v2.
        // _gameModeService.StartMonitoring();

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        _trayIcon = new TrayIcon(1, iconPath, "Monitor Blanker - Click to open settings, double-click to toggle (Win+Shift+B)");
        _trayIcon.Selected += (_, _) => ShowSettings();
        _trayIcon.LeftDoubleClick += (_, _) => ToggleBlanking();
        _trayIcon.IsVisible = true;

        if (openSettings)
        {
            ShowSettings();
        }
    }

    private void OnGameModeChanged(object? sender, GameModeChangedEventArgs e)
    {
        if (e.IsInGameMode)
        {
            _blankingService?.Blank();
        }
        else
        {
            _blankingService?.Unblank();
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new MainWindow(_blankingService!);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Activate();
    }

    private void ToggleBlanking()
    {
        _blankingService?.Toggle();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gameModeService?.Dispose();
        _hotkeyService?.Dispose();
        _blankingService?.Dispose();
        _hiddenWindow?.Close();
        _trayIcon?.Dispose();
        GC.SuppressFinalize(this);
    }
}
