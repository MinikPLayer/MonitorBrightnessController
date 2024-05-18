using FramePFX.Themes;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using winddcutil;

namespace WinDDC_UI
{
    public class MonitorData : INotifyPropertyChanged
    {
        private Monitor monitor;

        uint _brightness = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public uint Brightness
        {
            set
            {
                monitor.SetBrightness(value);
                _brightness = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Brightness)));
            }
            get => _brightness;
        }

        public float MaxValue { get; set; } = 100;

        public bool AllowExtended
        {
            set
            {
                MaxValue = value ? monitor.GetExtendedMax() : monitor.GetTypicalMax();
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxValue)));
            }
        }

        public override string? ToString() => monitor.ToString();

        public MonitorData(Monitor monitor, bool allowExtended)
        {
            this.monitor = monitor;
            _brightness = this.monitor.GetBrightness();
            AllowExtended = allowExtended;
        }
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private bool allowExtended;

        public bool AllowExtended
        {
            get { return allowExtended; }
            set
            {
                allowExtended = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllowExtended)));
                foreach (var m in Monitors)
                    m.AllowExtended = value;
            }
        }

        public ObservableCollection<MonitorData> Monitors { get; set; } = new ObservableCollection<MonitorData>();

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONIDENTIFIER
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public Guid guidItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public Int32 left;
            public Int32 top;
            public Int32 right;
            public Int32 bottom;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int Shell_NotifyIconGetRect([In] ref NOTIFYICONIDENTIFIER identifier, [Out] out RECT iconLocation);
        
        public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;
        public ObservableCollection<MonitorData> Monitors => ViewModel.Monitors;

        public MainWindow()
        {
            InitializeComponent();
            this.Hide();
            this.DataContext = new MainWindowViewModel();

            UpdateMonitors();
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.Desktop)
                    UpdateMonitors();

                if (e.Category == UserPreferenceCategory.General)
                    ((App)App.Current).UpdateTheme();
                
            };
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            if (App.icon == null)
                throw new Exception("App.icon is null");

            UpdateMonitors();

            var iconData = App.icon.IconData;
            var iconHandle = iconData.WindowHandle;
            var iconId = iconData.TaskbarIconId;

            var notifyIconIdentifier = new NOTIFYICONIDENTIFIER
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
                hWnd = iconHandle,
                uID = iconId,
            };

            var ret = Shell_NotifyIconGetRect(ref notifyIconIdentifier, out RECT iconLocation);
            if (ret == 0x8004005)
                throw new Exception("Icon doesn't exist");

            if (ret != 0)
                throw new Exception("Shell_NotifyIconGetRect failed");

            var dpiScaleFactor = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            this.Left = (iconLocation.left - this.Width) / dpiScaleFactor;
            this.Top = (iconLocation.top - this.Height) / dpiScaleFactor;
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            this.Close();
        }

        bool forceClose = false;
        protected override void OnClosing(CancelEventArgs e)
        {
            if(forceClose)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            this.Hide();
        }

        async void UpdateMonitors(List<Monitor>? monitors = null)
        {
            if (monitors == null)
            {
                monitors = await Monitor.Detect();
                if(monitors == null)
                {
                    if (Monitors.Count == 0)
                        throw new Exception("Cannot update monitors");

                    return;
                }
            }

            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateMonitors(monitors));
                return;
            }

            Monitors.Clear();
            foreach (Monitor monitor in monitors)
            {
                var data = new MonitorData(monitor, ViewModel.AllowExtended);
                Monitors.Add(data);
            }    

            this.Height = 70 + 30 * Monitors.Count;

            if (Monitors.Count == 0)
                return;

            var brightness = (int)Monitors[0].Brightness;
            foreach (var m in Monitors)
            {
                if (m.Brightness != brightness)
                {
                    brightness = -1;
                    break;
                }
            }

            disableCombinedBrightnessChange = true;
            if (brightness != -1)
            {
                CombinedBrightnessText.Text = brightness.ToString();
                CombinedBrightnessValue.Value = brightness;
            }
            else
            {
                CombinedBrightnessText.Text = "-";
                CombinedBrightnessValue.Value = Monitors[0].Brightness;
            }
            disableCombinedBrightnessChange = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e) => UpdateMonitors();

        bool disableCombinedBrightnessChange = false;
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(e.OldValue == e.NewValue || disableCombinedBrightnessChange)
                return;

            foreach(var m in Monitors)
                m.Brightness = (uint)e.NewValue;

            CombinedBrightnessText.Text = ((uint)e.NewValue).ToString();
        }
    }
}
