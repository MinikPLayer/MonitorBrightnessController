using FramePFX.Themes;
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

        public static bool IsLightTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int i && i > 0;
        }

        public void UpdateTheme()
        {
            var currentTheme = IsLightTheme();
            var dictionary = Resources.MergedDictionaries[0];
            try
            {
                dictionary.MergedDictionaries.Clear();
            }
            // Exception is sometimes thrown, but it still works
            // (Probably because Window is already open and uses these styles, but they will be replaced later in this function call)
            catch (ArgumentOutOfRangeException) { } 

            var source = currentTheme ? "/Themes/ColourDictionaries/LightTheme.xaml" : "/Themes/ColourDictionaries/DeepDark.xaml";
            dictionary.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(source, UriKind.RelativeOrAbsolute) });

            dictionary.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri("/Themes/ControlColours.xaml", UriKind.RelativeOrAbsolute) });
            dictionary.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri("/Themes/Controls.xaml", UriKind.RelativeOrAbsolute) });

            icon = (TaskbarIcon)FindResource("NotifyIcon");
            source = currentTheme ? "icon_light.ico" : "icon_dark.ico";
            icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri($"pack://application:,,,/{source}"));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            UpdateTheme();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            icon.Dispose();

            base.OnExit(e);
        }

    }
}
