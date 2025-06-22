using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace MusicPlayerWPF
{
    public interface IThemeChangeListener
    {
        event EventHandler ThemeChanged;
    }

    public class Constants
    {
        public static readonly List<string> SupportFormat = new List<string>()
        {
            ".mp3", ".ogg", ".flac", ".m4a", ".wav", ".opus", ".webm"
        };
    }

    public class ThemeHelper
    {
        [DllImport("UXTheme.dll", SetLastError = true, EntryPoint = "#138")]
        public static extern bool IsDarkModeEnabled();

    }

    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        public static ObservableCollection<PlaylistItem> GlobalPlaylist { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            GlobalPlaylist = new ObservableCollection<PlaylistItem>();

            if (e.Args != null && e.Args.Length > 0)
            {
                foreach (string filePath in e.Args)
                {
                    if (!File.Exists(filePath))
                    {
                        MessageBox.Show($"檔案 {filePath} 不存在", "檔案不存在", MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }
                    else if (!Constants.SupportFormat.Contains(Path.GetExtension(filePath)))
                    {
                        MessageBox.Show($"檔案 {filePath} 的格式 {Path.GetExtension(filePath).ToLower().Substring(1)} 未被支援", "檔案格式錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                        continue;
                    }
                    GlobalPlaylist.Add(
                        new PlaylistItem
                        {
                            FileName = Path.GetFileNameWithoutExtension(filePath),
                            FullPath = filePath,
                        }
                    );
                }
            }

            SetTheme();
            var mainWin = new MainWindow();
            mainWin.Show();

            Dispatcher?.Invoke(() =>
            {
                if (GlobalPlaylist.Count > 0)
                    mainWin.PlayFileFromPath(GlobalPlaylist[0].FullPath);
            });
        }

        public static void SetTheme()
        {
            SetAppTheme(
                MusicPlayerWPF.Properties.Settings.Default.BaseTheme,
                MusicPlayerWPF.Properties.Settings.Default.PrimaryColor,
                MusicPlayerWPF.Properties.Settings.Default.SecondaryColor);
        }

        private static void SetAppTheme(string targetTheme, string primaryColorName, string secondaryColorName)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (targetTheme == "Auto")
            {
                bool isDarkMode = ThemeHelper.IsDarkModeEnabled();
                theme.SetBaseTheme(isDarkMode ? BaseTheme.Dark : BaseTheme.Light);
            }
            else if (targetTheme == "Dark")
            {
                theme.SetBaseTheme(BaseTheme.Dark);
            }
            else
            {
                theme.SetBaseTheme(BaseTheme.Light);
            }

            Color primaryColor = GetMaterialDesignColor(primaryColorName);
            if (primaryColor != Colors.Transparent)
            {
                theme.SetPrimaryColor(primaryColor);
            }

            Color secondaryColor = GetMaterialDesignColor(secondaryColorName);
            if (secondaryColor != Colors.Transparent)
            {
                theme.SetSecondaryColor(secondaryColor);
            }

            paletteHelper.SetTheme(theme);
        }

        public static Color GetMaterialDesignColor(string colorName)
        {
            var colorProperty = typeof(Colors).GetProperties()
                .Where(prop => prop.PropertyType == typeof(Color))
                .FirstOrDefault(prop => prop.Name == colorName);

            if (colorProperty != null)
            {
                return (Color)colorProperty.GetValue(null, null);
            }
            return Colors.Transparent;
        }
    }
}
