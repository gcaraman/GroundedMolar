using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GroundedMolar.Core;
using Microsoft.Win32;

namespace GroundedMolar.App;

public partial class MainWindow : Window
{
    private const double MapSize = SaveMapImageRenderer.LogicalMapSize;
    private const double MarkerSize = 32;
    private static readonly Lazy<SaveAnalysisService> AnalysisService = new(() => new(
        new GroundedCsavDecoder(new OozKrakenDecoder(Path.Combine(AppContext.BaseDirectory, "ooz.exe"))),
        new ProfiledMolarAnalyzer([new GroundedSaveFormatProfileV1()], new GroundedMolarStateResolverV1())), LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly DispatcherTimer _reloadTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<(Image Element, PixelPoint Point, MolarApproachState ApproachState)> _markerElements = [];
    private FileSystemWatcher? _saveWatcher;
    private string _saveFolder = "";
    private bool _monitorFolder;
    private bool _isLoading;
    private double _zoomScale;
    private bool _isPanning;
    private Point _panStart;
    private double _panHorizontalOffset;
    private double _panVerticalOffset;
    private double _mapLeft;
    private double _mapTop;
    private double _unapproachedOpacity = MolarMarkerOpacity.DefaultUnapproached;
    private bool _settingsApplied;
    private CancellationTokenSource? _screenshotCancellation;
    private CancellationTokenSource? _analysisCancellation;

    public MainWindow()
    {
        InitializeComponent();
        ApplySettings(AppSettingsStore.Load());
        Loaded += OnLoaded;
        SizeChanged += WindowSizeChanged;
        Closed += OnClosed;
        _reloadTimer.Tick += async (_, _) => { _reloadTimer.Stop(); await LoadSaveAsync(false); };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyResponsiveLayout();
        if (!string.IsNullOrWhiteSpace(_saveFolder) && Directory.Exists(_saveFolder))
        {
            ConfigureSaveWatcher(_saveFolder);
            await LoadSaveAsync(false);
            return;
        }
        var path = SavePathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path))
        {
            ClearPreview("The previously selected save no longer exists. Pick a save or saves folder.");
            return;
        }
        _saveFolder = Path.GetDirectoryName(path) ?? "";
        ConfigureSaveWatcher(_saveFolder);
        await LoadSaveAsync(false);
    }

    private void WindowSizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        if (WorkspaceGrid is null || PreferencesPanel is null || MapFrame is null) return;
        var portrait = ActualHeight > ActualWidth;
        WorkspaceGrid.ColumnDefinitions[1].Width = portrait ? new GridLength(0) : new GridLength(280);
        Grid.SetColumn(MapFrame, 0);
        Grid.SetRow(MapFrame, portrait ? 1 : 0);
        Grid.SetRowSpan(MapFrame, portrait ? 1 : 2);
        Grid.SetColumn(PreferencesPanel, portrait ? 0 : 1);
        Grid.SetRow(PreferencesPanel, 0);
        Grid.SetRowSpan(PreferencesPanel, portrait ? 1 : 2);
        PreferencesPanel.Margin = portrait ? new Thickness(0, 0, 0, 16) : new Thickness(18, 0, 0, 0);
        PreferencesPanel.VerticalScrollBarVisibility = portrait ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    private async void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Grounded world save|World.csav|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        SavePathBox.Text = dialog.FileName;
        _saveFolder = Path.GetDirectoryName(dialog.FileName) ?? "";
        _monitorFolder = false;
        SetMonitorCheckBox(false);
        ConfigureSaveWatcher(_saveFolder);
        UpdateCurrentSavePresentation(dialog.FileName);
        PersistSettings();
        await LoadSaveAsync(true);
    }

    private async void BrowseFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose the Grounded saves folder", Multiselect = false };
        if (Directory.Exists(_saveFolder)) dialog.InitialDirectory = _saveFolder;
        if (dialog.ShowDialog(this) != true) return;
        _saveFolder = dialog.FolderName;
        _monitorFolder = true;
        SetMonitorCheckBox(true);
        ConfigureSaveWatcher(_saveFolder);
        PersistSettings();
        await LoadSaveAsync(true);
    }

    private async void LoadClick(object sender, RoutedEventArgs e) => await LoadSaveAsync(true);

    private async Task LoadSaveAsync(bool userInitiated)
    {
        if (_isLoading) { _analysisCancellation?.Cancel(); ScheduleReload(); return; }
        var path = _monitorFolder
            ? FindCurrentSave(_saveFolder)
            : File.Exists(SavePathBox.Text.Trim()) ? SavePathBox.Text.Trim() : null;
        if (path is null)
        {
            ClearPreview("No World.csav was found.");
            return;
        }
        SavePathBox.Text = path;
        UpdateCurrentSavePresentation(path);
        _isLoading = true;
        _analysisCancellation = new CancellationTokenSource();
        var analysisCancellation = _analysisCancellation;
        RefreshButton.IsEnabled = false;
        SetConfidenceState("Status.Warning");
        try
        {
            var analysis = await AnalyzeWithRetryAsync(path, analysisCancellation.Token);
            if (analysis.Confidence == SaveConfidence.Validated)
            {
                MapPreview.Source = SaveMapImageRenderer.LoadMap(analysis);
                RebuildMarkers(analysis);
                CurrentSaveSummary.Text = analysis.Uncollected.Count == 1 ? "Validated • 1 milk molar remaining" : $"Validated • {analysis.Uncollected.Count} milk molars remaining";
                PreviewOverlay.Visibility = Visibility.Collapsed;
                SetConfidenceState("Status.Success");
                if (_zoomScale <= 0) FitMap(); else ApplyZoom(_zoomScale);
            }
            else
            {
                CurrentSaveSummary.Text = "This save version isn't supported";
                SetConfidenceState("Status.Warning");
                ClearPreview("This save format is not validated, so no image was created.");
            }
            PersistSettings();
        }
        catch (Exception exception) when (!userInitiated && exception is IOException or UnauthorizedAccessException)
        {
            ScheduleReload();
        }
        catch (OperationCanceledException) when (analysisCancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            CurrentSaveSummary.Text = "This save couldn't be opened";
            SetConfidenceState("Status.Error");
            ClearPreview("The save could not be read, so no image was created.");
        }
        finally
        {
            if (ReferenceEquals(_analysisCancellation, analysisCancellation)) _analysisCancellation = null;
            analysisCancellation.Dispose();
            _isLoading = false;
            RefreshButton.IsEnabled = HasSaveSource();
        }
    }

    private void ClearPreview(string message)
    {
        MapPreview.Source = null;
        foreach (var marker in _markerElements) MapSurface.Children.Remove(marker.Element);
        _markerElements.Clear();
        PreviewPlaceholder.Text = message;
        PreviewOverlay.Visibility = Visibility.Visible;
    }

    private void RebuildMarkers(MolarAnalysis analysis)
    {
        foreach (var marker in _markerElements) MapSurface.Children.Remove(marker.Element);
        _markerElements.Clear();
        var icon = SaveMapImageRenderer.LoadMarkerIcon();
        var projector = new CoordinateProjector();
        foreach (var marker in analysis.Uncollected)
        {
            var exportedPoint = projector.WorldToExportedTexture(marker.WorldX, marker.WorldY);
            var point = new PixelPoint(
                MapZoom.NormalizeCoordinate(exportedPoint.X, SaveMapImageRenderer.ExportedMapSize, MapSize),
                MapZoom.NormalizeCoordinate(exportedPoint.Y, SaveMapImageRenderer.ExportedMapSize, MapSize));
            if (point.X is < 0 or > MapSize || point.Y is < 0 or > MapSize) continue;
            var element = new Image
            {
                Source = icon,
                Width = MarkerSize,
                Height = MarkerSize,
                Opacity = MolarMarkerOpacity.Resolve(marker.ApproachState, _unapproachedOpacity),
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(element, BitmapScalingMode.NearestNeighbor);
            Panel.SetZIndex(element, 1);
            MapSurface.Children.Add(element);
            _markerElements.Add((element, point, marker.ApproachState));
        }
    }

    private double FitScale => MapZoom.FitScale(MapScroller.ViewportWidth, MapScroller.ViewportHeight, MapSize, MapSize);

    private void ApplyZoom(double requestedScale, Point? focalPoint = null)
    {
        var fit = FitScale;
        if (fit <= 0) return;
        var oldScale = _zoomScale > 0 ? _zoomScale : fit;
        var focal = focalPoint ?? new Point(MapScroller.ViewportWidth / 2, MapScroller.ViewportHeight / 2);
        var imageX = (MapScroller.HorizontalOffset + focal.X - _mapLeft) / oldScale;
        var imageY = (MapScroller.VerticalOffset + focal.Y - _mapTop) / oldScale;
        _zoomScale = MapZoom.Clamp(requestedScale, fit);
        var extent = MapSize * _zoomScale;
        MapSurface.Width = MapPreview.Width = extent;
        MapSurface.Height = MapPreview.Height = extent;
        ViewportSurface.Width = Math.Max(extent, MapScroller.ViewportWidth);
        ViewportSurface.Height = Math.Max(extent, MapScroller.ViewportHeight);
        _mapLeft = MapZoom.CenterOffset(MapScroller.ViewportWidth, extent);
        _mapTop = MapZoom.CenterOffset(MapScroller.ViewportHeight, extent);
        Canvas.SetLeft(MapSurface, _mapLeft);
        Canvas.SetTop(MapSurface, _mapTop);
        foreach (var marker in _markerElements)
        {
            Canvas.SetLeft(marker.Element, MapZoom.CenteredTopLeft(marker.Point.X, _zoomScale, MarkerSize));
            Canvas.SetTop(marker.Element, MapZoom.CenteredTopLeft(marker.Point.Y, _zoomScale, MarkerSize));
        }
        MapSurface.UpdateLayout();
        MapScroller.ScrollToHorizontalOffset(_mapLeft + imageX * _zoomScale - focal.X);
        MapScroller.ScrollToVerticalOffset(_mapTop + imageY * _zoomScale - focal.Y);
        ZoomText.Text = Math.Abs(_zoomScale - fit) < .000001 ? "Fit" : $"{_zoomScale:0.##}×";
    }

    private void FitMap() => ApplyZoom(FitScale);
    private void FitClick(object sender, RoutedEventArgs e) => FitMap();
    private void ZoomInClick(object sender, RoutedEventArgs e) => ApplyZoom((_zoomScale > 0 ? _zoomScale : FitScale) * 1.25);
    private void ZoomOutClick(object sender, RoutedEventArgs e) => ApplyZoom((_zoomScale > 0 ? _zoomScale : FitScale) / 1.25);
    private void MapMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (MapPreview.Source is null) return;
        ApplyZoom((_zoomScale > 0 ? _zoomScale : FitScale) * (e.Delta > 0 ? 1.25 : 0.8), e.GetPosition(MapScroller));
        e.Handled = true;
    }
    private void MapMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (MapPreview.Source is null || (MapScroller.ScrollableWidth <= 0 && MapScroller.ScrollableHeight <= 0)) return;
        _isPanning = true;
        _panStart = e.GetPosition(MapScroller);
        _panHorizontalOffset = MapScroller.HorizontalOffset;
        _panVerticalOffset = MapScroller.VerticalOffset;
        MapScroller.Cursor = Cursors.SizeAll;
        MapScroller.CaptureMouse();
        e.Handled = true;
    }
    private void MapMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning || e.LeftButton != MouseButtonState.Pressed) return;
        var current = e.GetPosition(MapScroller);
        MapScroller.ScrollToHorizontalOffset(_panHorizontalOffset - (current.X - _panStart.X));
        MapScroller.ScrollToVerticalOffset(_panVerticalOffset - (current.Y - _panStart.Y));
        e.Handled = true;
    }
    private void MapMouseUp(object sender, MouseButtonEventArgs e) => EndPan();
    private void MapMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isPanning && e.LeftButton != MouseButtonState.Pressed) EndPan();
    }
    private void EndPan()
    {
        if (!_isPanning) return;
        _isPanning = false;
        MapScroller.ReleaseMouseCapture();
        MapScroller.Cursor = Cursors.Arrow;
    }
    private void MapViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MapPreview.Source is null) return;
        var oldFit = MapZoom.FitScale(e.PreviousSize.Width, e.PreviousSize.Height, MapSize, MapSize);
        if (_zoomScale <= 0 || Math.Abs(_zoomScale - oldFit) < .000001) FitMap();
        else ApplyZoom(_zoomScale);
    }

    private static async Task<MolarAnalysis> AnalyzeWithRetryAsync(string path, CancellationToken cancellationToken)
    {
        var delays = new[] { 250, 500, 1000 };
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await Task.Run(() =>
                {
                    return AnalysisService.Value.Analyze(path, cancellationToken);
                }, cancellationToken);
            }
            catch (Exception exception) when (attempt < delays.Length && exception is IOException or UnauthorizedAccessException) { await Task.Delay(delays[attempt], cancellationToken); }
        }
    }

    private void ConfigureSaveWatcher(string folder)
    {
        _saveWatcher?.Dispose();
        _saveWatcher = null;
        if (!_monitorFolder || !Directory.Exists(folder)) { UpdateMonitorStatus(); return; }
        _saveWatcher = new FileSystemWatcher(folder, "World.csav")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _saveWatcher.Changed += SaveChanged;
        _saveWatcher.Created += SaveChanged;
        _saveWatcher.Renamed += SaveChanged;
        _saveWatcher.Deleted += SaveChanged;
        _saveWatcher.Error += (_, _) => Dispatcher.BeginInvoke(ScheduleReload);
        UpdateMonitorStatus();
    }

    private async void MonitorFolderChanged(object sender, RoutedEventArgs e)
    {
        if (!_settingsApplied) return;
        _monitorFolder = MonitorFolderCheckBox.IsChecked == true;
        ConfigureSaveWatcher(_saveFolder);
        PersistSettings();
        if (_monitorFolder) await LoadSaveAsync(true);
    }

    private void UpdateMonitorStatus()
    {
        if (MonitorStatusText is null) return;
        MonitorStatusText.Text = _monitorFolder
            ? _saveWatcher is null ? "Folder unavailable" : "Watching for changes"
            : "Paused";
    }

    private void SetMonitorCheckBox(bool isChecked)
    {
        var wasApplied = _settingsApplied;
        _settingsApplied = false;
        MonitorFolderCheckBox.IsChecked = isChecked;
        _settingsApplied = wasApplied;
    }

    private void UpdateCurrentSavePresentation(string path)
    {
        var file = new FileInfo(path);
        var saveGroup = file.Directory?.Name;
        var area = MatchFolderValue(saveGroup, "Area");
        var gameTime = MatchFolderValue(saveGroup, "GameTime");
        var saveType = FriendlySaveType(saveGroup);
        CurrentSaveName.Text = file.Exists ? (area ?? saveType ?? "Current save").ToUpperInvariant() : "SAVE NOT FOUND";
        CurrentSaveLocation.Text = file.Exists
            ? string.Join("  •  ", new[] { saveType, gameTime is null ? null : $"Played {gameTime}" }.Where(value => value is not null))
            : "Choose another save to continue.";
        CurrentSaveModified.Text = file.Exists
            ? $"Last played {file.LastWriteTime:MMM d, yyyy · h:mm tt}"
            : "—";
        LoadSaveScreenshot(file.DirectoryName);
        if (file.Exists) CurrentSaveSummary.Text = "Loading molars…";
        RefreshButton.IsEnabled = HasSaveSource();
    }

    private async void LoadSaveScreenshot(string? folder)
    {
        _screenshotCancellation?.Cancel();
        _screenshotCancellation?.Dispose();
        _screenshotCancellation = new CancellationTokenSource();
        var cancellationToken = _screenshotCancellation.Token;
        SaveScreenshotPreview.Source = null;
        SaveScreenshotPlaceholder.Visibility = Visibility.Visible;
        if (folder is null) return;
        var screenshot = new[] { "SaveGameScreenshot.jpg", "SaveGameScreenshot.png" }
            .Select(name => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
        if (screenshot is null) return;
        try
        {
            var bitmap = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var encoded = GroundedScreenshotValidator.ReadValidated(screenshot);
                cancellationToken.ThrowIfCancellationRequested();
                using var stream = new MemoryStream(encoded, writable: false);
                var decoded = new BitmapImage();
                decoded.BeginInit();
                decoded.CacheOption = BitmapCacheOption.OnLoad;
                decoded.DecodePixelWidth = GroundedScreenshotValidator.RequiredWidth;
                decoded.DecodePixelHeight = GroundedScreenshotValidator.RequiredHeight;
                decoded.StreamSource = stream;
                decoded.EndInit();
                if (decoded.PixelWidth != GroundedScreenshotValidator.RequiredWidth || decoded.PixelHeight != GroundedScreenshotValidator.RequiredHeight)
                    throw new InvalidDataException("Decoded screenshot dimensions changed after header validation.");
                decoded.Freeze();
                return decoded;
            }, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            SaveScreenshotPreview.Source = bitmap;
            SaveScreenshotPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException or OperationCanceledException) { }
    }

    private static string? MatchFolderValue(string? folder, string label)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        var match = Regex.Match(folder, $@"\({label}-(?<value>[^)]*)\)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static string? FriendlySaveType(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;
        if (Regex.IsMatch(folder, @"\(LOGOUT-SAVE\)", RegexOptions.IgnoreCase)) return "Logout save";
        if (Regex.IsMatch(folder, @"\(AUTOSAVE-\d+\)", RegexOptions.IgnoreCase)) return "Autosave";
        if (Regex.IsMatch(folder, @"\(PREMIX\)", RegexOptions.IgnoreCase)) return "Before Remix.R";
        if (Regex.IsMatch(folder, @"\(REMIX\)", RegexOptions.IgnoreCase)) return "Remix.R";
        if (Regex.IsMatch(folder, @"\(ENDGAME\)", RegexOptions.IgnoreCase)) return "Endgame save";
        return "Manual save";
    }

    private static string? FindCurrentSave(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive };
        return Directory.EnumerateFiles(folder, "World.csav", options)
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase).Select(file => file.FullName).FirstOrDefault();
    }

    private void SaveChanged(object sender, FileSystemEventArgs e) => Dispatcher.BeginInvoke(ScheduleReload);
    private void ScheduleReload() { _reloadTimer.Stop(); _reloadTimer.Start(); }

    private void ApplySettings(AppSettings settings)
    {
        SavePathBox.Text = settings.SavePath;
        _monitorFolder = settings.MonitorFolder;
        _saveFolder = !string.IsNullOrWhiteSpace(settings.SaveFolder) ? settings.SaveFolder : Path.GetDirectoryName(settings.SavePath) ?? "";
        _unapproachedOpacity = MolarMarkerOpacity.Clamp(settings.UnapproachedOpacity);
        UnapproachedOpacitySlider.Value = _unapproachedOpacity;
        UnapproachedOpacityText.Text = $"{_unapproachedOpacity:P0}";
        MonitorFolderCheckBox.IsChecked = _monitorFolder;
        UpdateMonitorStatus();
        if (!string.IsNullOrWhiteSpace(settings.SavePath)) UpdateCurrentSavePresentation(settings.SavePath);
        _settingsApplied = true;
        RefreshButton.IsEnabled = HasSaveSource();
    }

    private bool HasSaveSource() => _monitorFolder
        ? Directory.Exists(_saveFolder)
        : File.Exists(SavePathBox.Text.Trim());

    private void SetConfidenceState(string brushKey)
    {
        var brush = (Brush)FindResource(brushKey);
        CurrentSaveSummary.Foreground = brush;
    }

    private void UnapproachedOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _unapproachedOpacity = MolarMarkerOpacity.Clamp(e.NewValue);
        if (UnapproachedOpacityText is not null) UnapproachedOpacityText.Text = $"{_unapproachedOpacity:P0}";
        foreach (var marker in _markerElements)
            marker.Element.Opacity = MolarMarkerOpacity.Resolve(marker.ApproachState, _unapproachedOpacity);
        if (_settingsApplied) PersistSettings();
    }

    private void PersistSettings()
    {
        try { AppSettingsStore.Save(new(SavePathBox.Text.Trim(), _saveFolder, _monitorFolder, _unapproachedOpacity)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }

    private void OnClosed(object? sender, EventArgs e) { _reloadTimer.Stop(); _saveWatcher?.Dispose(); _analysisCancellation?.Cancel(); _analysisCancellation?.Dispose(); _screenshotCancellation?.Cancel(); _screenshotCancellation?.Dispose(); }
}
