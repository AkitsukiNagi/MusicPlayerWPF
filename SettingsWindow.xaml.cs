using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MusicPlayerWPF
{
    /// <summary>
    /// SettingsWindow.xaml 的互動邏輯
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public List<string> AvailableColors { get; set; }
        public event EventHandler<CancelEventArgs> WindowClosedEvent;
        public List<string> Themes = new List<string>
        {
            "Auto", "Light", "Dark"
        };

        public SettingsWindow(MainWindow root)
        {
            InitializeComponent();

            Title = "【設定】" + root.AppName;

            DataContext = this;
            AvailableColors = GetMaterialDesignColorNames();

            ThemeSwitch.SelectedItem = Properties.Settings.Default.BaseTheme;
            ThemeSwitch.ItemsSource = Themes;

            PrimaryColorCombo.SelectedValue = Properties.Settings.Default.PrimaryColor;
            SecondaryColorCombo.SelectedValue = Properties.Settings.Default.SecondaryColor;

            Closing += SettingsWindow_Closing;
        }

        private void SettingsWindow_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.BaseTheme = ThemeSwitch.SelectedItem.ToString();
            Properties.Settings.Default.PrimaryColor = PrimaryColorCombo.SelectedItem.ToString();
            Properties.Settings.Default.SecondaryColor = SecondaryColorCombo.SelectedItem.ToString();
            Properties.Settings.Default.Save();
            App.SetTheme();

            WindowClosedEvent?.Invoke(this, e);
        }

        private List<string> GetMaterialDesignColorNames()
        {
            var colors = typeof(Colors).GetProperties()
                .Where(prop => prop.PropertyType == typeof(Color))
                .Select(prop => prop.Name)
                .ToList();
            return colors;
        }

        private void ThemeSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeSwitch.SelectedItem is string selected)
            {
                Properties.Settings.Default.BaseTheme = selected;
                Properties.Settings.Default.Save();
                App.SetTheme();
            }
        }

        private void PrimaryColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrimaryColorCombo.SelectedItem is string selected)
            {
                Properties.Settings.Default.PrimaryColor = selected;
                Properties.Settings.Default.Save();

                App.SetTheme();
            }
        }

        private void SecondaryColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondaryColorCombo.SelectedItem is string selected)
            {
                Properties.Settings.Default.SecondaryColor = selected;
                Properties.Settings.Default.Save();

                App.SetTheme();
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
