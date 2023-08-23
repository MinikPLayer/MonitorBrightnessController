using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace WinDDC_UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static TaskbarIcon? icon;

        private static bool IsLightTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }

        public void UpdateTheme()
        {
            var dictionary = Resources.MergedDictionaries[0];
            dictionary.MergedDictionaries.Clear();

            var source = IsLightTheme() ? "/Themes/ColourDictionaries/LightTheme.xaml" : "/Themes/ColourDictionaries/SoftDark.xaml";
            Resources.MergedDictionaries[0].MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(source, UriKind.RelativeOrAbsolute) });
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            icon = (TaskbarIcon)FindResource("NotifyIcon");
            var source = IsLightTheme() ? "icon_light.ico" : "icon_dark.ico";
            icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri($"pack://application:,,,/{source}"));

            UpdateTheme();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            icon.Dispose();

            base.OnExit(e);
        }

    }
}
