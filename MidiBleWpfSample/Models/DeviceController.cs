using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MidiBleWpfSample.Services;

namespace MidiBleWpfSample.Models
{
    public class DeviceController : INotifyPropertyChanged
    {
        private string _deviceName;
        private bool _isConnected;
        private double _rangeValue;
        private double _gainValue;
        private bool _invert;
        private double _delayValue;

        // 共有のBleManagerインスタンス
        private readonly BleManager _bleManager;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DeviceController(string name, BleManager sharedBleManager)
        {
            _deviceName = name;
            _bleManager = sharedBleManager;
        }

        public string DeviceName
        {
            get => _deviceName;
            set { _deviceName = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        public double RangeValue
        {
            get => _rangeValue;
            set { _rangeValue = value; OnPropertyChanged(); }
        }

        public double GainValue
        {
            get => _gainValue;
            set { _gainValue = value; OnPropertyChanged(); }
        }

        public bool Invert
        {
            get => _invert;
            set { _invert = value; OnPropertyChanged(); }
        }

        public double DelayValue
        {
            get => _delayValue;
            set { _delayValue = value; OnPropertyChanged(); }
        }

        /// <summary>接続ボタン</summary>
        public void Connect()
        {
            Debug.WriteLine($"[DeviceController] Connect called for {_deviceName}.");
            _ = _bleManager.StartScan(); // サンプルではスキャン開始で自動接続を待つ

            // あくまでデモ用: 実際には BleManager が TargetDeviceName を検出 → 接続成功したら
            // BleConnected イベントで通知を受けて IsConnected = true にするほうが自然
            IsConnected = true;
        }

        /// <summary>切断ボタン</summary>
        public void Disconnect()
        {
            Debug.WriteLine($"[DeviceController] Disconnect called for {_deviceName}.");
            _bleManager.Disconnect();
            IsConnected = false;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}