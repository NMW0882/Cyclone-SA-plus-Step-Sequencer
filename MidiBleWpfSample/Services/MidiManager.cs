using System;
using NAudio.Midi;

namespace MidiBleWpfSample.Services
{
    public class MidiManager
    {
        private MidiIn? _midiIn;
        private string? _currentMidiDeviceName;
        private BleManager _bleManager;
        public event Action<string>? StatusMessage;

        public int TargetController { get; set; } = 10;

        public MidiManager(BleManager bleManager)
        {
            _bleManager = bleManager;
        }

        public string[] GetMidiDevices()
        {
            int count = MidiIn.NumberOfDevices;
            string[] devices = new string[count];
            for (int i = 0; i < count; i++)
            {
                devices[i] = MidiIn.DeviceInfo(i).ProductName;
            }
            return devices;
        }

        public string? CurrentMidiDeviceName => _currentMidiDeviceName;

        public void StartListening(string deviceName)
        {
            // 既に同じデバイスを使用している場合は何もしない
            if (_midiIn != null && _currentMidiDeviceName == deviceName) return;

            // 別のデバイスへの切り替え時は、既存のMIDI入力を停止・解放
            if (_midiIn != null)
            {
                _midiIn.Stop();
                _midiIn.Dispose();
                _midiIn = null;
            }

            int deviceCount = MidiIn.NumberOfDevices;
            for (int deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                if (MidiIn.DeviceInfo(deviceId).ProductName == deviceName)
                {
                    _midiIn = new MidiIn(deviceId);
                    _midiIn.MessageReceived += OnMessageReceived;
                    _midiIn.ErrorReceived += OnErrorReceived;
                    _midiIn.Start();
                    _currentMidiDeviceName = deviceName;
                    StatusMessage?.Invoke($"MIDI Start: {deviceName}");
                    return;
                }
            }
            StatusMessage?.Invoke($"MIDI device '{deviceName}' not found.");
        }

        private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is ControlChangeEvent ccEvent)
            {
                if (ccEvent.Controller == (MidiController)TargetController)
                {
                    int value = ccEvent.ControllerValue;
                    // 従来: var bleData = ConvertCC10ToBle(value); _bleManager.UpdateLatestData(bleData);
                    // 今後: BleManager が Protocol.BuildPacket() する
                    _bleManager.SendMidiValue(value);
                }
            }
        }

        private void OnErrorReceived(object? sender, MidiInMessageEventArgs e)
        {
            StatusMessage?.Invoke($"MIDI Error: {e.RawMessage}");
        }
    }
}