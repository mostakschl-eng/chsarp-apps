using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using SCHLStudio.App.Services;
using SCHLStudio.App.Configuration;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace SCHLStudio.App.Shared.Services
{
    public sealed class ThemeService : IThemeService
    {
        private readonly IConfiguration _configuration;
        private AppTheme _currentTheme = AppTheme.Dark;

        public ThemeService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AppTheme CurrentTheme => _currentTheme;

        public void Initialize()
        {
            try
            {
                var theme = LoadThemePreference();
                ApplyTheme(theme);
            }
            catch
            {
            }
        }

        public void SetUserTheme(AppTheme theme)
        {
            try
            {
                ApplyTheme(theme);

                WpfMessageBox.Show(
                    "Theme updated.",
                    "Theme",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch
            {
            }
        }

        private AppTheme LoadThemePreference()
        {
            // Always use Light theme for the app
            return AppTheme.Light;
        }

        private void ApplyTheme(AppTheme theme)
        {
            _currentTheme = theme;

            if (WpfApplication.Current is null)
            {
                return;
            }

            var dictionaries = WpfApplication.Current.Resources.MergedDictionaries;

            var existingThemeDictionary = dictionaries
                .FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Shared/Resources/Themes/Theme", StringComparison.OrdinalIgnoreCase));

            var newSource = theme == AppTheme.Light
                ? new Uri("Shared/Resources/Themes/Theme.Light.xaml", UriKind.Relative)
                : new Uri("Shared/Resources/Themes/Theme.xaml", UriKind.Relative);

            if (existingThemeDictionary is not null)
            {
                existingThemeDictionary.Source = newSource;
            }
            else
            {
                dictionaries.Add(new ResourceDictionary { Source = newSource });
            }
        }

        private static bool TryParseTheme(string? value, out AppTheme theme)
        {
            theme = AppTheme.Dark;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase))
            {
                theme = AppTheme.Light;
                return true;
            }

            if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                theme = AppTheme.Dark;
                return true;
            }

            return false;
        }

        private sealed record ThemePreference(string Theme);
    }
}
