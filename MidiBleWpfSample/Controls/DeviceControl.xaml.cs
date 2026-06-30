using System.Windows;
using System.Windows.Controls;
using MidiBleWpfSample.Models;

namespace MidiBleWpfSample.Controls
{
    public partial class DeviceControl : UserControl
    {
        // コンストラクタ
        public DeviceControl()
        {
            InitializeComponent();
        }

        private void OnClickConnect(object sender, RoutedEventArgs e)
        {
            // DataContext は DeviceController のはずなので
            if (DataContext is DeviceController dc)
            {
                dc.Connect();
            }
        }

        private void OnClickDisconnect(object sender, RoutedEventArgs e)
        {
            if (DataContext is DeviceController dc)
            {
                dc.Disconnect();
            }
        }
    }
}
