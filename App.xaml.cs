using MaterialDesignThemes.Wpf;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace MusicPlayerWPF
{
    /// <summary>
    /// App.xaml 的互動邏輯
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            SetTheme();
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

            if (targetTheme == "Dark")
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
