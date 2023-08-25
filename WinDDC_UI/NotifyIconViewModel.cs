using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WinDDC_UI
{
    public class NotifyIconViewModel
    {
        public ICommand? ShowWindowCommand => new RelayCommand(() => {
            var window = App.Current.MainWindow;
            window.Show();
            window.Activate();
        });
        public ICommand? ExitApplicationCommand => new RelayCommand(() => App.Current.Shutdown());
        public ICommand? HideWindowCommand => new RelayCommand(() => App.Current.MainWindow?.Close());

    }
}
