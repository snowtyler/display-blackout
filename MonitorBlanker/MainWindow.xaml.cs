using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace MonitorBlanker;

public sealed partial class MainWindow : Window
{
    public ObservableCollection<MonitorItem> Monitors { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        LoadMonitors();
    }

    private void LoadMonitors()
    {
        var displays = DisplayArea.FindAll();
        var primaryId = DisplayArea.Primary?.DisplayId.Value;

        // Find the bounding box of all monitors
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        // Use indexed loop - foreach throws InvalidCastException due to CsWinRT bug:
        // https://github.com/microsoft/WindowsAppSDK/issues/3484
        for (int i = 0; i < displays.Count; i++)
        {
            var bounds = displays[i].OuterBounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxX = Math.Max(maxX, bounds.X + bounds.Width);
            maxY = Math.Max(maxY, bounds.Y + bounds.Height);
        }

        int totalWidth = maxX - minX;
        int totalHeight = maxY - minY;

        // Scale to fit in the canvas (max 400x200)
        const double maxCanvasWidth = 400;
        const double maxCanvasHeight = 200;
        double scale = Math.Min(maxCanvasWidth / totalWidth, maxCanvasHeight / totalHeight);

        for (int i = 0; i < displays.Count; i++)
        {
            var display = displays[i];
            var bounds = display.OuterBounds;
            bool isPrimary = display.DisplayId.Value == primaryId;

            Monitors.Add(new MonitorItem
            {
                DisplayNumber = i + 1,
                IsPrimary = isPrimary,
                IsSelected = !isPrimary,
                // Scaled and offset positions for the visual layout
                ScaledX = (bounds.X - minX) * scale,
                ScaledY = (bounds.Y - minY) * scale,
                ScaledWidth = bounds.Width * scale,
                ScaledHeight = bounds.Height * scale,
                // Store actual bounds for blanking
                Bounds = bounds
            });
        }

        // Set canvas size based on scaled total
        MonitorCanvas.Width = totalWidth * scale;
        MonitorCanvas.Height = totalHeight * scale;

        // Create visual elements for each monitor
        foreach (var monitor in Monitors)
        {
            var border = CreateMonitorVisual(monitor);
            Canvas.SetLeft(border, monitor.ScaledX);
            Canvas.SetTop(border, monitor.ScaledY);
            MonitorCanvas.Children.Add(border);
        }
    }

    private static Border CreateMonitorVisual(MonitorItem monitor)
    {
        var border = new Border
        {
            Width = monitor.ScaledWidth - 4, // Gap between monitors
            Height = monitor.ScaledHeight - 4,
            CornerRadius = new CornerRadius(4),
            Tag = monitor
        };

        var textBlock = new TextBlock
        {
            Text = monitor.DisplayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        border.Child = textBlock;
        UpdateMonitorVisualState(border, monitor);

        border.PointerPressed += (s, e) =>
        {
            monitor.IsSelected = !monitor.IsSelected;
            UpdateMonitorVisualState(border, monitor);
        };

        monitor.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MonitorItem.IsSelected))
            {
                UpdateMonitorVisualState(border, monitor);
            }
        };

        return border;
    }

    private static void UpdateMonitorVisualState(Border border, MonitorItem monitor)
    {
        var textBlock = (TextBlock)border.Child;

        if (monitor.IsSelected)
        {
            // Selected for blanking - show as dark/will be blanked
            border.Background = new SolidColorBrush(Microsoft.UI.Colors.Black);
            border.BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 120, 212));
            border.BorderThickness = new Thickness(2);
            textBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        }
        else
        {
            // Not selected - show as normal/active
            border.Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"];
            border.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            border.BorderThickness = new Thickness(1);
            textBlock.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }
    }
}

public sealed partial class MonitorItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public int DisplayNumber { get; set; }
    public bool IsPrimary { get; set; }

    public double ScaledX { get; set; }
    public double ScaledY { get; set; }
    public double ScaledWidth { get; set; }
    public double ScaledHeight { get; set; }
    public RectInt32 Bounds { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
