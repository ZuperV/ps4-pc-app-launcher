using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PS4Launcher;

public partial class MainWindow : Window
{
    private readonly List<AppItem> _allApps = new();
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _clockTimer = new();
    private string _currentCategory = "All";
    private List<AppItem> _visibleApps = new();
    private string FavoritesFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PS4PcLauncher", "favorites.json");

    public MainWindow()
    {
        InitializeComponent();
        LoadFavorites();
        LoadInstalledApps();
        _clockTimer.Interval = TimeSpan.FromSeconds(30);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        ApplyFilters();
    }

    private void LoadInstalledApps()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folders = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        foreach (var folder in folders.Where(Directory.Exists))
        {
            try
            {
                foreach (var shortcutPath in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        var shortcut = ReadShortcut(shortcutPath);
                        if (shortcut is null || string.IsNullOrWhiteSpace(shortcut.Target) || !File.Exists(shortcut.Target))
                            continue;

                        var extension = Path.GetExtension(shortcut.Target);
                        if (!extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                            && !extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                            && !extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!seen.Add(shortcut.Target))
                            continue;

                        var name = string.IsNullOrWhiteSpace(shortcut.Name)
                            ? Path.GetFileNameWithoutExtension(shortcut.Target)
                            : shortcut.Name;

                        var app = new AppItem
                        {
                            Name = name,
                            Path = shortcut.Target,
                            Arguments = shortcut.Arguments,
                            WorkingDirectory = shortcut.WorkingDirectory,
                            Category = Categorize(name, shortcut.Target),
                            Icon = LoadIcon(shortcut.Target),
                            IsFavorite = _favorites.Contains(shortcut.Target)
                        };
                        _allApps.Add(app);
                    }
                    catch
                    {
                        // One broken shortcut should not prevent other apps from loading.
                    }
                }
            }
            catch
            {
                // Some Start Menu folders may be inaccessible.
            }
        }

        _allApps.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
    }

    private static ShortcutInfo? ReadShortcut(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
                return null;

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
                return null;

            dynamic shellObject = shell;
            shortcut = shellObject.CreateShortcut(shortcutPath);
            dynamic shortcutObject = shortcut;
            return new ShortcutInfo(
                Convert.ToString(shortcutObject.TargetPath) ?? string.Empty,
                Convert.ToString(shortcutObject.Arguments) ?? string.Empty,
                Convert.ToString(shortcutObject.WorkingDirectory) ?? string.Empty,
                Path.GetFileNameWithoutExtension(shortcutPath));
        }
        catch
        {
            return null;
        }
        finally
        {
            if (shortcut is not null && System.Runtime.InteropServices.Marshal.IsComObject(shortcut))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            if (shell is not null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    private static string Categorize(string name, string path)
    {
        var value = $"{name} {path}".ToLowerInvariant();
        if (new[] { "game", "steam", "xbox", "epic", "battle.net", "riot", "minecraft", "gog", "playnite" }.Any(value.Contains))
            return "Games";
        if (new[] { "media", "music", "video", "spotify", "vlc", "youtube", "netflix", "plex", "movies" }.Any(value.Contains))
            return "Media";
        return "Apps";
    }

    private static ImageSource? LoadIcon(string filePath)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            if (icon is null)
                return null;
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = 128;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private void LoadFavorites()
    {
        try
        {
            if (!File.Exists(FavoritesFile))
                return;
            var paths = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FavoritesFile));
            if (paths is not null)
                _favorites.UnionWith(paths);
        }
        catch
        {
            // Corrupt preferences are ignored; the launcher remains usable.
        }
    }

    private void SaveFavorites()
    {
        try
        {
            var directory = Path.GetDirectoryName(FavoritesFile);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(FavoritesFile, JsonSerializer.Serialize(_favorites.OrderBy(x => x)));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save favorites: {ex.Message}", "Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void UpdateClock() => ClockText.Text = DateTime.Now.ToString("ddd, MMM d   h:mm tt");

    private void ApplyFilters()
    {
        var query = SearchBox.Text.Trim();
        IEnumerable<AppItem> filtered = _allApps;
        if (_currentCategory == "Favorites")
            filtered = filtered.Where(app => app.IsFavorite);
        else if (_currentCategory != "All")
            filtered = filtered.Where(app => app.Category == _currentCategory);

        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(app => app.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        _visibleApps = filtered.ToList();
        AppList.ItemsSource = _visibleApps;
        CurrentCategoryLabel.Text = _currentCategory == "All" ? "WHAT'S NEW" : _currentCategory.ToUpperInvariant();
        AppCountText.Text = $"{_visibleApps.Count} items";
        if (_visibleApps.Count > 0)
        {
            AppList.SelectedIndex = 0;
        }
        else
        {
            AppList.SelectedIndex = -1;
            ShowNoSelection();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string category })
        {
            _currentCategory = category;
            ApplyFilters();
            AppList.Focus();
        }
    }

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppList.SelectedItem is AppItem app)
            ShowAppDetails(app);
        else
            ShowNoSelection();
    }

    private void ShowAppDetails(AppItem app)
    {
        SelectedAppName.Text = app.Name;
        SelectedAppCategory.Text = app.Category.ToUpperInvariant();
        SelectedAppPath.Text = app.Path;
        SelectedAppImage.Source = app.Icon;
        FavoriteButton.Content = app.IsFavorite ? "★  REMOVE FROM FAVORITES" : "☆  ADD TO FAVORITES";
        AppList.ScrollIntoView(app);
    }

    private void ShowNoSelection()
    {
        SelectedAppName.Text = "No apps found";
        SelectedAppCategory.Text = "Try another category or search";
        SelectedAppPath.Text = string.Empty;
        SelectedAppImage.Source = null;
        FavoriteButton.Content = "ADD TO FAVORITES";
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => LaunchSelectedApp();
    private void LaunchSelected_Click(object sender, RoutedEventArgs e) => LaunchSelectedApp();

    private void FavoriteSelected_Click(object sender, RoutedEventArgs e)
    {
        if (AppList.SelectedItem is not AppItem app)
            return;

        app.IsFavorite = !app.IsFavorite;
        if (app.IsFavorite)
            _favorites.Add(app.Path);
        else
            _favorites.Remove(app.Path);
        SaveFavorites();

        if (_currentCategory == "Favorites" && !app.IsFavorite)
        {
            ApplyFilters();
        }
        else
        {
            ShowAppDetails(app);
        }
    }

    private void LaunchSelectedApp()
    {
        if (AppList.SelectedItem is not AppItem app)
            return;
        try
        {
            var startInfo = new ProcessStartInfo { FileName = app.Path, UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(app.Arguments))
                startInfo.Arguments = app.Arguments;
            if (!string.IsNullOrWhiteSpace(app.WorkingDirectory) && Directory.Exists(app.WorkingDirectory))
                startInfo.WorkingDirectory = app.WorkingDirectory;
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open {app.Name}: {ex.Message}", "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (AppList.Items.Count > 0)
        {
            AppList.SelectedIndex = 0;
            AppList.Focus();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            return;
        }

        if (e.Key == Key.F && AppList.SelectedItem is AppItem)
        {
            FavoriteSelected_Click(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }

        if (SearchBox.IsKeyboardFocusWithin && e.Key != Key.Enter)
            return;

        if (_visibleApps.Count == 0)
            return;

        var index = AppList.SelectedIndex < 0 ? 0 : AppList.SelectedIndex;
        var columns = Math.Max(1, (int)Math.Floor((AppList.ActualWidth - 40) / 180));
        switch (e.Key)
        {
            case Key.Left: index--; break;
            case Key.Right: index++; break;
            case Key.Up: index -= columns; break;
            case Key.Down: index += columns; break;
            case Key.Enter: LaunchSelectedApp(); e.Handled = true; return;
            default: return;
        }

        AppList.SelectedIndex = Math.Clamp(index, 0, _visibleApps.Count - 1);
        AppList.ScrollIntoView(AppList.SelectedItem);
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        base.OnClosed(e);
    }

    private sealed record ShortcutInfo(string Target, string Arguments, string WorkingDirectory, string Name);
}

public sealed class AppItem : INotifyPropertyChanged
{
    private bool _isFavorite;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Category { get; set; } = "Apps";
    public ImageSource? Icon { get; set; }
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            if (_isFavorite == value)
                return;
            _isFavorite = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
