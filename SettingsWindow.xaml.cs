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

        public SettingsWindow()
        {
            InitializeComponent();

            DataContext = this;
            AvailableColors = GetMaterialDesignColorNames();

            ThemeSwitch.IsChecked = Properties.Settings.Default.BaseTheme == "Dark";
            ThemeSwitch.Checked += ThemeSwitch_Checked;
            ThemeSwitch.Unchecked += ThemeSwitch_Unchecked;

            PrimaryColorCombo.SelectedValue = Properties.Settings.Default.PrimaryColor;
            SecondaryColorCombo.SelectedValue = Properties.Settings.Default.SecondaryColor;

            Closing += SettingsWindow_Closing;
        }

        private void SettingsWindow_Closing(object sender, CancelEventArgs e)
        {
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

        private void ThemeSwitch_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.BaseTheme = "Dark";
            Properties.Settings.Default.Save();

            App.SetTheme();
        }

        private void ThemeSwitch_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.BaseTheme = "Light";
            Properties.Settings.Default.Save();

            App.SetTheme();
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

        public void Close_Window()
        {
            Close();
        }
    }
}
