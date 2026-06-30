﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MidiBleWpfSample.Services;

namespace MidiBleWpfSample.Models
{
    public class BleSlot : INotifyPropertyChanged
    {
        public BleManager BleManager { get; }
        public MidiManager MidiManager { get; }

        public ObservableCollection<string> BleDeviceList { get; } = new ObservableCollection<string>();

        // ★ 最後に処理したCC値を記憶する変数を追加 (初期値は停止=64)
        private int _lastRawCcValue = 64;
        public int LastRawCcValue => _lastRawCcValue;

        public event Action<string, double>? ParameterChanged;

        private string _slotName;
        public string SlotName
        {
            get => _slotName;
            set { _slotName = value; OnPropertyChanged(); }
        }

        private int _midiChannel = 1;
        public int MidiChannel
        {
            get => _midiChannel;
            set
            {
                if (_midiChannel != value)
                {
                    _midiChannel = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedDeviceName = string.Empty;
        public string SelectedDeviceName
        {
            get => _selectedDeviceName;
            set
            {
                if (_selectedDeviceName != value)
                {
                    _selectedDeviceName = value;
                    OnPropertyChanged();
                    SelectedDeviceChanged?.Invoke(this, value);
                }
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); }
        }

        private double _rangeValue = 100.0;
        public double RangeValue
        {
            get => _rangeValue;
            set
            {
                if (_rangeValue != value)
                {
                    _rangeValue = value;
                    OnPropertyChanged();
                    ParameterChanged?.Invoke("Range", _rangeValue);
                    // ★ スライダー操作時に再計算・再送信を実行
                    ProcessAndSendMidi(_lastRawCcValue);
                }
            }
        }

        private double _gainValue = 0.0;
        public double GainValue
        {
            get => _gainValue;
            set
            {
                if (_gainValue != value)
                {
                    _gainValue = value;
                    OnPropertyChanged();
                    ParameterChanged?.Invoke("Gain", _gainValue);
                    // ★ スライダー操作時に再計算・再送信を実行
                    ProcessAndSendMidi(_lastRawCcValue);
                }
            }
        }

        private double _speedValue = 100.0;
        public double SpeedValue
        {
            get => _speedValue;
            set
            {
                if (_speedValue != value)
                {
                    _speedValue = value;
                    OnPropertyChanged();
                    ParameterChanged?.Invoke("Speed", _speedValue);
                    ProcessAndSendMidi(_lastRawCcValue);
                }
            }
        }

        private double _amplitudeValue = 1.0;
        public double AmplitudeValue
        {
            get => _amplitudeValue;
            set
            {
                if (_amplitudeValue != value)
                {
                    _amplitudeValue = value;
                    OnPropertyChanged();
                    ParameterChanged?.Invoke("Amplitude", _amplitudeValue);
                    ProcessAndSendMidi(_lastRawCcValue);
                }
            }
        }


        public event Action<BleSlot, string>? SelectedDeviceChanged;

        public BleSlot(string slotName, bool isAutoConnectSlot = false, int defaultCC = 10)
        {
            SlotName = slotName;
            BleManager = new BleManager(isAutoConnectSlot);
            MidiManager = new MidiManager(BleManager)
            {
                TargetController = defaultCC
            };

            MidiChannel = 1;

            BleManager.DeviceFound += devName =>
            {
                if (!string.IsNullOrEmpty(devName) && !BleDeviceList.Contains(devName))
                {
                    App.Current.Dispatcher.Invoke(() => {
                        BleDeviceList.Add(devName);
                    });
                }
            };

            BleManager.BleConnected += connected =>
            {
                IsConnected = connected;
                if (connected && string.IsNullOrEmpty(SelectedDeviceName))
                {
                    SelectedDeviceName = BleManager.ConnectedDeviceName;
                }
            };
        }

        public async Task InitializeAsync()
        {
            await BleManager.StartScan();
        }

        public void ProcessAndSendMidi(int rawCcValue)
        {
            // ★ 最後に処理したCC値を記憶
            _lastRawCcValue = rawCcValue;

            if (rawCcValue == 64)
            {
                this.BleManager.SendMidiValue(64);
                return;
            }

            double center = 64.0;
            double deviation = rawCcValue - center;

            // 1. Initial deviation with Range and Gain
            double rangedDeviation = deviation * (this.RangeValue / 100.0);
            double gainFactor = 1.0 + (this.GainValue / 100.0);
            double calculatedDeviation = rangedDeviation * gainFactor;

            // 2. Apply SpeedValue as gain
            double speedFactor = this.SpeedValue / 100.0;
            double speedAdjustedDeviation = calculatedDeviation * speedFactor;

            // 3. Apply AmplitudeValue as a limiter
            double limit = 64.0 * this.AmplitudeValue;
            double limitedDeviation = Math.Clamp(speedAdjustedDeviation, -limit, limit);

            // 4. Calculate final value
            double finalValue = center + limitedDeviation;

            int finalCcValue = (int)Math.Clamp(Math.Round(finalValue), 0, 127);

            this.BleManager.SendMidiValue(finalCcValue);
        }

        public async Task Connect()
        {
            if (!string.IsNullOrEmpty(SelectedDeviceName))
            {
                await BleManager.ConnectToDeviceName(SelectedDeviceName);
            }
        }

        public void Disconnect()
        {
            BleManager.Disconnect();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}