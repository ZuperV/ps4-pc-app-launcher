using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PS4Launcher;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<AppItem> _apps = new();

    public MainWindow()
    {
        InitializeComponent();
        AppPanel.ItemsSource = _apps;
        LoadInstalledApps();
    }

    private void LoadInstalledApps()
    {
        var results = new List<AppItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        var folders = new[]
        {
            Path.Combine(userStartMenu, "Programs"),
            Path.Combine(commonStartMenu, "Programs"),
            desktop
        };

        foreach (var folder in folders.Where(Directory.Exists))
        {
            try
            {
                foreach (var shortcut in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                {
                    try
                    {
                        var target = GetShortcutTarget(shortcut);
                        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
                            continue;

                        var extension = Path.GetExtension(target).ToLowerInvariant();
                        if (extension != ".exe" && extension != ".bat" && extension != ".cmd")
                            continue;

                        var name = Path.GetFileNameWithoutExtension(target);
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (!seen.Add(target))
                            continue;

                        results.Add(new AppItem
                        {
                            Name = name,
                            Path = target,
                            Icon = LoadIcon(target)
                        });
                    }
                    catch
                    {
                        // Ignore malformed shortcut entries.
                    }
                }
            }
            catch
            {
                // Ignore inaccessible folders.
            }
        }

        foreach (var app in results.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
        {
            _apps.Add(app);
        }
    }

    private static string GetShortcutTarget(string shortcutPath)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return string.Empty;

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            return shortcut.TargetPath ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static ImageSource? LoadIcon(string filePath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon == null)
                return null;

            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.DecodePixelWidth = 72;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            AppPanel.ItemsSource = _apps;
            return;
        }

        AppPanel.ItemsSource = _apps.Where(a =>
            a.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void LaunchApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string appPath)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open app: {ex.Message}", "Launcher Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

public class AppItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ImageSource? Icon { get; set; }
}
