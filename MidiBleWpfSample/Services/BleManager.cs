using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using MidiBleWpfSample.Services.Protocols;

namespace MidiBleWpfSample.Services
{
    public class BleManager
    {
        private IBleProtocol? _currentProtocol;
        private BluetoothLEAdvertisementWatcher? _watcher;
        private bool _isScanning;
        private bool _isConnected;
        private GattCharacteristic? _targetCharacteristic;
        private BluetoothLEDevice? _connectedDevice;
        private string? _connectedDeviceName;
        private readonly Dictionary<string, ulong> _advertisedDevices = new();
        private byte[]? _latestPacket = null;
        private bool _sendingLoopRunning;

        private bool _autoConnectDisabled = false;
        private bool _autoConnectAttempted = false;

        private bool _isAutoConnectSlot = false;
        private byte _deviceIdentifier = 0x01;

        public event Action<string>? StatusMessage;
        public event Action<bool>? BleConnected;
        public event Action<string>? DeviceFound;
        public event Action<string>? DeviceConnected;

        public bool IsConnected => _isConnected;
        public string? ConnectedDeviceName => _connectedDeviceName;
        public byte DeviceIdentifier => _deviceIdentifier;

        public BleManager(bool isAutoConnectSlot = false)
        {
            _isAutoConnectSlot = isAutoConnectSlot;
        }

        public async Task<bool> StartScan()
        {
            if (_isScanning) return true;

            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null || !adapter.IsCentralRoleSupported || !adapter.IsLowEnergySupported)
            {
                StatusMessage?.Invoke("Bluetooth adapter is not available or does not support BLE.");
                Debug.WriteLine("[BleManager] Bluetooth adapter not available.");
                return false;
            }

            var radio = await adapter.GetRadioAsync();
            if (radio == null || radio.State != RadioState.On)
            {
                StatusMessage?.Invoke("Bluetooth is turned off.");
                Debug.WriteLine("[BleManager] Bluetooth radio is off.");
                return false;
            }

            _watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            _watcher.Received += Watcher_Received;
            _watcher.Stopped += (s, e) =>
            {
                _isScanning = false;
                Debug.WriteLine("[BleManager] Watcher stopped.");
                StatusMessage?.Invoke("Watcher Stopped.");
            };

            _isScanning = true;
            _advertisedDevices.Clear();
            _autoConnectAttempted = false;
            _watcher.Start();
            StatusMessage?.Invoke("BLE scanning start...");
            return true;
        }

        public void StopScan()
        {
            if (_isScanning && _watcher != null)
            {
                _watcher.Stop();
                _isScanning = false;
            }
        }

        private void Watcher_Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            var deviceName = args.Advertisement.LocalName;
            if (string.IsNullOrEmpty(deviceName))
                return;

            if (_isConnected && deviceName == _connectedDeviceName)
                return;

            DeviceFound?.Invoke(deviceName);
            if (!_advertisedDevices.ContainsKey(deviceName))
            {
                _advertisedDevices[deviceName] = args.BluetoothAddress;
            }

            if (_isAutoConnectSlot && !_autoConnectAttempted && !_autoConnectDisabled && !_isConnected && deviceName == "CycSA")
            {
                _autoConnectAttempted = true;
                _ = ConnectToDeviceName("CycSA");
            }
        }

        public async Task ConnectToDeviceName(string deviceName)
        {
            if (_isConnected && _connectedDeviceName == deviceName)
                return;
            if (_isConnected)
            {
                Disconnect();
            }
            if (!_advertisedDevices.ContainsKey(deviceName))
            {
                StatusMessage?.Invoke($"'{deviceName}' is not found in advertised list.");
                return;
            }
            ulong address = _advertisedDevices[deviceName];
            try
            {
                var device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);
                if (device == null)
                {
                    StatusMessage?.Invoke($"Failed to connect '{deviceName}': device is null");
                    return;
                }
                _connectedDevice = device;
                var servicesResult = await device.GetGattServicesAsync();
                if (servicesResult.Status != GattCommunicationStatus.Success)
                {
                    StatusMessage?.Invoke("Failed to get GATT services.");
                    return;
                }
                Guid serviceUuid = Guid.Parse("40EE1111-63EC-4B7F-8CE7-712EFD55B90E");
                Guid characteristicUuid = Guid.Parse("40EE2222-63EC-4B7F-8CE7-712EFD55B90E");
                GattDeviceService? targetService = null;
                foreach (var s in servicesResult.Services)
                {
                    if (s.Uuid == serviceUuid)
                    {
                        targetService = s;
                        break;
                    }
                }
                if (targetService == null)
                {
                    StatusMessage?.Invoke("Target service not found.");
                    return;
                }
                var charsResult = await targetService.GetCharacteristicsAsync();
                if (charsResult.Status != GattCommunicationStatus.Success)
                {
                    StatusMessage?.Invoke("Failed to get characteristics.");
                    return;
                }
                GattCharacteristic? foundCharacteristic = null;
                foreach (var c in charsResult.Characteristics)
                {
                    if (c.Uuid == characteristicUuid)
                    {
                        foundCharacteristic = c;
                        break;
                    }
                }
                if (foundCharacteristic == null)
                {
                    StatusMessage?.Invoke("Target characteristic not found.");
                    return;
                }
                _targetCharacteristic = foundCharacteristic;
                _connectedDeviceName = deviceName;
                _isConnected = true;
                BleConnected?.Invoke(true);
                StatusMessage?.Invoke($"BLE connected successfully to {deviceName}!");
                DeviceConnected?.Invoke(deviceName);
                StartSendLoop();
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke($"BLE connect error ({deviceName}): {ex.Message}");
            }

            if (deviceName == "CycSA")
            {
                _currentProtocol = new VorzeCycloneProtocol(0x01);
                _deviceIdentifier = 0x01;
            }
            else if (deviceName == "UFOSA")
            {
                _currentProtocol = new VorzeCycloneProtocol(0x02);
                _deviceIdentifier = 0x02;
            }
            else if (deviceName == "PistonSA")
            {
                _currentProtocol = new VorzePistonProtocol();
            }
            else
            {
                _currentProtocol = null;
            }
        }

        private void StartSendLoop()
        {
            if (_sendingLoopRunning) return;
            _sendingLoopRunning = true;
            Task.Run(async () =>
            {
                while (_sendingLoopRunning)
                {
                    byte[]? packetToSend = Interlocked.Exchange(ref _latestPacket, null);

                    if (packetToSend != null)
                    {
                        await SendData(packetToSend);
                    }
                    
                    await Task.Delay(50);
                }
            });
        }

        public async Task<bool> SendData(byte[] data)
        {
            if (!_isConnected || _targetCharacteristic == null)
            {
                return false;
            }
            try
            {
                var writer = new Windows.Storage.Streams.DataWriter();
                writer.WriteBytes(data);
                var result = await _targetCharacteristic.WriteValueAsync(writer.DetachBuffer());
                if (result == GattCommunicationStatus.Success)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine("Write failed: " + result);
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage?.Invoke($"SendData Error: {ex.Message}");
                return false;
            }
        }

        public void SendMidiValue(int ccValue)
        {
            if (!_isConnected || _currentProtocol == null)
                return;
            byte[] packet = _currentProtocol.BuildPacket(ccValue);
            Interlocked.Exchange(ref _latestPacket, packet);
        }

        public void Disconnect()
        {
            Debug.WriteLine("[BleManager] Initiating disconnect...");
            StopScan();
            _sendingLoopRunning = false;
            _autoConnectDisabled = true;

            Interlocked.Exchange(ref _latestPacket, null);

            if (_connectedDevice != null)
            {
                _connectedDevice.Dispose();
                _connectedDevice = null;
            }
            _connectedDeviceName = null;
            _targetCharacteristic = null;
            _currentProtocol = null;
            _isConnected = false;
            BleConnected?.Invoke(false);
            StatusMessage?.Invoke("Disconnected.");
            Debug.WriteLine("[BleManager] Disconnect completed.");
        }
    }
}
