using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MidiBleWpfSample.Models;
using MidiBleWpfSample.Properties;
using System.ComponentModel;
using MidiBleWpfSample.Global;

namespace MidiBleWpfSample
{
    public partial class SettingsWindow : Window
    {
        // 公開プロパティとして Slots（BLEスロット）を定義（XAML バインディング対象）
        public ObservableCollection<BleSlot> Slots { get; }
        private GlobalMidiManager _globalMidi;

        // プレイヤーコントローラー設定
        public string SelectedPlayerController { get; set; } = "MPC";
        public string PlayerPort { get; set; } = "13579";

        // ★ 追加: Player Controller 変更を通知するイベント
        public event Action<string, string>? PlayerControllerChanged;

        public SettingsWindow(ObservableCollection<BleSlot> slots, GlobalMidiManager gm)
        {
            Slots = slots;
            _globalMidi = gm;

            InitializeComponent();

            // ウィンドウ位置とサイズの復元
            this.Left = Settings.Default.SettingsWindowLeft;
            this.Top = Settings.Default.SettingsWindowTop;
            this.Width = Settings.Default.SettingsWindowWidth;
            this.Height = Settings.Default.SettingsWindowHeight;

            this.DataContext = this;

            Loaded += SettingsWindow_Loaded;
        }

        // デザインタイム用
        public SettingsWindow()
        {
            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                throw new InvalidOperationException("Use the constructor with parameters");
            }
            InitializeComponent();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // CC Number 保存
            if (cmbCCSelect.SelectedItem is ComboBoxItem citem)
            {
                Settings.Default.LastCC = citem.Content.ToString(); // "CC#1" or "CC#10"
            }

            // MIDI Input Device 保存
            if (cmbMidiDevices.SelectedItem is string devName)
            {
                Settings.Default.LastMidiDevice = devName;
            }

            // プレイヤーコントローラー設定保存
            Settings.Default.LastPlayerController = SelectedPlayerController;
            Settings.Default.LastPlayerPort = txtPlayerPort.Text;

            // ウィンドウ位置とサイズの保存
            Settings.Default.SettingsWindowLeft = this.Left;
            Settings.Default.SettingsWindowTop = this.Top;
            Settings.Default.SettingsWindowWidth = this.Width;
            Settings.Default.SettingsWindowHeight = this.Height;
            Settings.Default.Save();
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // (1) CC Number 復元
            string lastCC = Settings.Default.LastCC;
            foreach (var item in cmbCCSelect.Items)
            {
                if (item is ComboBoxItem cboxItem && cboxItem.Content.ToString() == lastCC)
                {
                    cboxItem.IsSelected = true;
                    break;
                }
            }

            // (2) MIDI Input Device 復元
            if (Slots != null && Slots.Count > 0)
            {
                var devs = Slots[0].MidiManager.GetMidiDevices();
                cmbMidiDevices.ItemsSource = devs;
                string lastMidi = Settings.Default.LastMidiDevice;
                if (!string.IsNullOrEmpty(lastMidi))
                {
                    int idx = -1;
                    for (int i = 0; i < devs.Length; i++)
                    {
                        if (devs[i].Equals(lastMidi, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = i;
                            break;
                        }
                    }
                    if (idx >= 0)
                        cmbMidiDevices.SelectedIndex = idx;
                }
            }

            // (3) プレイヤーコントローラー復元
            string lastPlayer = Settings.Default.LastPlayerController;
            if (!string.IsNullOrEmpty(lastPlayer))
            {
                switch (lastPlayer)
                {
                    case "MPC":
                        rbMPC.IsChecked = true;
                        SelectedPlayerController = "MPC";
                        break;
                    case "None":
                        rbNone.IsChecked = true;
                        SelectedPlayerController = "None";
                        break;
                    default:
                        rbMPC.IsChecked = true;
                        SelectedPlayerController = "MPC";
                        break;
                }
            }
            // (4) ポート番号復元
            string lastPort = Settings.Default.LastPlayerPort;
            if (!string.IsNullOrEmpty(lastPort))
            {
                txtPlayerPort.Text = lastPort;
                PlayerPort = lastPort;
            }
        }

        private void cmbMidiDevices_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_globalMidi == null)
                return;
            if (cmbMidiDevices.SelectedItem is string devName)
            {
                _globalMidi.StartListening(devName);
            }
        }

        private void cmbCCSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_globalMidi == null)
                return;
            if (cmbCCSelect.SelectedItem is ComboBoxItem item)
            {
                int cc = (item.Content.ToString() == "CC#1") ? 1 : 10;
                _globalMidi.TargetController = cc;
            }
        }

        // ラジオボタンのチェック変更ハンドラー
        private void rbPlayerController_Checked(object sender, RoutedEventArgs e)
        {
            if (rbMPC.IsChecked == true)
            {
                SelectedPlayerController = "MPC";
                txtPlayerPort.IsEnabled = true;
            }
            else if (rbNone.IsChecked == true)
            {
                SelectedPlayerController = "None";
                txtPlayerPort.IsEnabled = false;
            }
            // ★ 変更: 選択変更時に即時通知
            PlayerControllerChanged?.Invoke(SelectedPlayerController, txtPlayerPort.Text);
        }

        // ★ 追加: ポート番号変更時も即時通知する
        private void txtPlayerPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlayerControllerChanged?.Invoke(SelectedPlayerController, txtPlayerPort.Text);
        }

        // 単一トグルボタンによる Connect/Disconnect (BLE Slot 用)
        private async void OnClickToggleConnect(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is BleSlot slot)
            {
                if (!slot.IsConnected)
                {
                    // 自動接続対象の場合、Slot1なら SelectedDeviceName が空なら自動で "CycSA" を設定
                    if (slot.BleManager != null && slot.SlotName == "Slot1" && string.IsNullOrEmpty(slot.SelectedDeviceName))
                    {
                        slot.SelectedDeviceName = "CycSA";
                    }
                    await slot.Connect();
                }
                else
                {
                    slot.Disconnect();
                }
            }
        }
    }
}
