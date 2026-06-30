using System;
using NAudio.Midi;

namespace MidiBleWpfSample.Global
{
    public class GlobalMidiManager
    {
        private MidiIn? _midiIn;
        private int _targetController = 10;

        // ─────────── 修正: チャンネル情報も通知 ───────────
        // (controller, ccValue, channel1to16)
        public event Action<int, int, int>? CcValueReceived;

        public void StartListening(string deviceName)
        {
            StopListening(); // 同じデバイス多重オープン防止

            int deviceCount = MidiIn.NumberOfDevices;
            for (int deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                if (MidiIn.DeviceInfo(deviceId).ProductName == deviceName)
                {
                    _midiIn = new MidiIn(deviceId);
                    _midiIn.MessageReceived += OnMessageReceived;
                    _midiIn.Start();
                    return;
                }
            }
        }

        public void StopListening()
        {
            if (_midiIn != null)
            {
                _midiIn.Stop();
                _midiIn.Dispose();
                _midiIn = null;
            }
        }

        private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is ControlChangeEvent ccEvent)
            {
                int controller = (int)ccEvent.Controller;
                int value = ccEvent.ControllerValue;
                int channel1to16 = ccEvent.Channel;
                
                CcValueReceived?.Invoke(controller, value, channel1to16);
            }
        }

        public int TargetController
        {
            get => _targetController;
            set => _targetController = value;
        }
    }
}
