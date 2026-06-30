﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MidiBleWpfSample.Global;
using MidiBleWpfSample.Models;
using MidiBleWpfSample.Services.PlayerControllers;
using MidiBleWpfSample.Services;
using NAudio.Midi;
using MidiBleWpfSample.Properties;
using MidiBleWpfSample.Controls;
using System.Windows.Interop;
using MidiBleWpfSample.Sequencer.ViewModels;
using System.Runtime.InteropServices;
using System.Collections.Specialized;

namespace MidiBleWpfSample.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private HwndSource? _source;
        private const int HOTKEY_ID_GAIN_UP = 9000;
        private const int HOTKEY_ID_GAIN_DOWN = 9001;
        private const int HOTKEY_ID_MUTE = 9002;

        private const uint MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var helper = new WindowInteropHelper(this);
            _source = HwndSource.FromHwnd(helper.Handle);
            _source.AddHook(HwndHook);
            RegisterHotKeys();
        }

        private void RegisterHotKeys()
        {
            var helper = new WindowInteropHelper(this);
            // Mute/Unmute
            RegisterHotKey(helper.Handle, HOTKEY_ID_MUTE, 0, (uint)KeyInterop.VirtualKeyFromKey(Key.Multiply));
            // Gain
            RegisterHotKey(helper.Handle, HOTKEY_ID_GAIN_DOWN, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.Z));
            RegisterHotKey(helper.Handle, HOTKEY_ID_GAIN_UP, MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.Oem5));
        }

        private void UnregisterHotKeys()
        {
            var helper = new WindowInteropHelper(this);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_MUTE);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_GAIN_DOWN);
            UnregisterHotKey(helper.Handle, HOTKEY_ID_GAIN_UP);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                const double gainStep = 5.0;
                switch (wParam.ToInt32())
                {
                    case HOTKEY_ID_MUTE:
                        Dispatcher.BeginInvoke(new Action(ToggleMute));
                        handled = true;
                        break;
                    case HOTKEY_ID_GAIN_DOWN:
                        if (Slots.Count > 0)
                        {
                            var slot = Slots[0];
                            double newValue = Math.Clamp(slot.GainValue - gainStep, -100, 100);
                            slot.GainValue = newValue;
                            handled = true;
                        }
                        break;
                    case HOTKEY_ID_GAIN_UP:
                        if (Slots.Count > 0)
                        {
                            var slot = Slots[0];
                            double newValue = Math.Clamp(slot.GainValue + gainStep, -100, 100);
                            slot.GainValue = newValue;
                            handled = true;
                        }
                        break;
                }
            }
            return IntPtr.Zero;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool _isGloballyPaused;
        public bool IsGloballyPaused
        {
            get => _isGloballyPaused;
            set
            {
                if (_isGloballyPaused != value)
                {
                    _isGloballyPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        public GlobalMidiManager GlobalMidi { get; } = new GlobalMidiManager();
        public ObservableCollection<BleSlot> Slots { get; } = new ObservableCollection<BleSlot>();
        public SequencerViewModel SequencerViewModel { get; }

        private MpcController? _mpcController;
        private readonly MidiPlayer _midiPlayer = new();
        private readonly DispatcherTimer mpcMonitorTimer = new();

        private long _previousMpcPosition = -1;
        private double _previousPlaybackRate = -1;
        private bool _isMpcPaused = false;
        private long _lastSeekPosition = -1;
        private int _lastMpcState = -1;
        private DateTime _lastMpcUpdateTime = DateTime.Now;

        private bool mpcMissingLogged = false;
        
        private bool isMidiPrioritized = false; // Default: Sequencer overrides MIDI
        private bool _midiFileLoaded = false;
        private bool _isHoldPedalDown = false;

        // --- Interrupt Feature Fields ---
        private readonly DispatcherTimer _interruptTimer = new DispatcherTimer();
        private bool _isInterrupting = false;
        private double _interruptDuration = 2.0;
        private double _interruptIntensity = 100.0;
        private InterruptDirection _interruptDirection = InterruptDirection.FollowPrevious;
        private InterruptMode _interruptMode = InterruptMode.Toggle; // Add this
        private int _lastSentMidiValue = 64; // Neutral value

        private enum InterruptDirection
        {
            Clockwise,
            CounterClockwise,
            FollowPrevious
        }

        public enum InterruptMode // Add this
        {
            Toggle,
            Hold
        }
        // --------------------------------

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;

            LoadSettings();

            SequencerViewModel = new SequencerViewModel();
            SequencerViewModel.SequencerMidiValueGenerated += OnSequencerMidiValueGenerated;
            SequencerViewModel.SequencerStopped += OnSequencerStopped;
            
            Slots.Add(new BleSlot("Slot1", isAutoConnectSlot: true, defaultCC: 1));

            foreach (var slot in Slots)
            {
                slot.ParameterChanged += (paramName, value) =>
                {
                    AppendLog($"{slot.SlotName} {paramName} changed to: {value:F0}");
                };
            }

            GlobalMidi.CcValueReceived += (controller, ccValue, channel) =>
            {
                if (IsGloballyPaused) return;

                if (controller == 64)
                {
                    bool isPedalCurrentlyDown = ccValue >= 64;
                    if (isPedalCurrentlyDown == _isHoldPedalDown) return;
                    _isHoldPedalDown = isPedalCurrentlyDown;

                    if (_interruptMode == InterruptMode.Hold)
                    {
                        if (isPedalCurrentlyDown)
                        {
                            if (!_isInterrupting) StartInterrupt();
                        }
                        else
                        {
                            if (_isInterrupting) StopInterrupt();
                        }
                    }
                    else // Toggle mode
                    {
                        if (isPedalCurrentlyDown) // Only toggle on press, not release
                        {
                            if (_isInterrupting)
                            {
                                StopInterrupt();
                            }
                            else
                            {
                                StartInterrupt();
                            }
                        }
                    }
                    return; // Don't process this CC message further
                }

                if (_isInterrupting) return;

                // Handle other CC messages as before
                if (controller == GlobalMidi.TargetController)
                {
                    _lastSentMidiValue = ccValue;
                    foreach (var s in Slots)
                    {
                        if (s.MidiChannel == channel)
                        {
                            s.ProcessAndSendMidi(ccValue);
                        }
                    }
                }
            };

            string? lastPlayer = Settings.Default.LastPlayerController;
            string? lastPort = Settings.Default.LastPlayerPort;
            UpdatePlayerController(lastPlayer, lastPort);

            _midiPlayer.MidiEventReady += (timedEvent) =>
            {
                if (IsGloballyPaused || _isInterrupting) return;
                if (timedEvent.MidiEvent is ControlChangeEvent ccEvent)
                {
                    if ((int)ccEvent.Controller == 10)
                    {
                        int ccValue = ccEvent.ControllerValue;
                        _lastSentMidiValue = ccValue;
                        int midiChannel = ccEvent.Channel;
                        foreach (var s in Slots)
                        {
                            if (s.MidiChannel == midiChannel)
                            {
                                s.ProcessAndSendMidi(ccValue);
                            }
                        }
                    }
                }
            };

            mpcMonitorTimer.Interval = TimeSpan.FromSeconds(1);
            mpcMonitorTimer.Tick += MpcMonitorTimer_Tick;
            mpcMonitorTimer.Start();

            _interruptTimer.Tick += InterruptTimer_Tick;

            this.Closing += MainWindow_Closing;

            this.Loaded += MainWindow_Loaded;
        }

        private void LoadSettings()
        {
            // Window position
             this.Left = Settings.Default.MainWindowLeft;
             this.Top = Settings.Default.MainWindowTop;
             this.Width = Settings.Default.MainWindowWidth;
             this.Height = Settings.Default.MainWindowHeight;

            // MIDI vs Sequencer Priority
            isMidiPrioritized = Settings.Default.IsMidiPrioritized;
            if (isMidiPrioritized)
            {
                rbMidiPriority.IsChecked = true;
            }
            else
            {
                rbSequencerPriority.IsChecked = true;
            }

            // MIDI Delay
            sldMidiDelay.Value = Settings.Default.MidiDelay;
            txtMidiDelay.Text = Settings.Default.MidiDelay.ToString() + "ms";

            // Always on Top
            chkAlwaysOnTop.IsChecked = Settings.Default.IsAlwaysOnTop;

            // Interrupt Settings
            _interruptDuration = Settings.Default.InterruptDuration;
            sldInterruptDuration.Value = _interruptDuration;
            txtInterruptDuration.Text = $"{_interruptDuration:F1}s";

            _interruptIntensity = Settings.Default.InterruptIntensity;
            sldInterruptIntensity.Value = _interruptIntensity;
            txtInterruptIntensity.Text = $"{_interruptIntensity:F0}%";

            _interruptDirection = (InterruptDirection)Settings.Default.InterruptDirection;
            switch (_interruptDirection)
            {
                case InterruptDirection.Clockwise:
                    rbInterruptDirCw.IsChecked = true;
                    break;
                case InterruptDirection.CounterClockwise:
                    rbInterruptDirCcw.IsChecked = true;
                    break;
                case InterruptDirection.FollowPrevious:
                    rbInterruptDirFollow.IsChecked = true;
                    break;
            }

            _interruptMode = (InterruptMode)Settings.Default.InterruptMode;
        }

        private void SaveSettings()
        {
            Settings.Default.MainWindowLeft = this.Left;
            Settings.Default.MainWindowTop = this.Top;
            Settings.Default.MainWindowWidth = this.Width;
            Settings.Default.MainWindowHeight = this.Height;

            // MIDI vs Sequencer Priority
            Settings.Default.IsMidiPrioritized = isMidiPrioritized;

            // MIDI Delay
            Settings.Default.MidiDelay = (int)sldMidiDelay.Value;

            // Always on Top
            Settings.Default.IsAlwaysOnTop = chkAlwaysOnTop.IsChecked ?? false;

            // Interrupt Settings
            Settings.Default.InterruptDuration = _interruptDuration;
            Settings.Default.InterruptIntensity = _interruptIntensity;
            Settings.Default.InterruptDirection = (int)_interruptDirection;
            Settings.Default.InterruptMode = (int)_interruptMode;

            // Last Preset Paths
            var paths = new StringCollection();
            var fileBasedPresets = SequencerViewModel.UserPresets.Where(p => !string.IsNullOrEmpty(p.FilePath) && File.Exists(p.FilePath));
            foreach (var preset in fileBasedPresets)
            {
                if(preset.FilePath != null) paths.Add(preset.FilePath);
            }
            Settings.Default.LastPresetFilePaths = paths;

            Settings.Default.Save();
        }

        private void ToggleMute()
        {
            IsGloballyPaused = !IsGloballyPaused;

            if (IsGloballyPaused)
            {
                AppendLog("--- System Muted ---");
                // Stop any ongoing interrupt
                if (_isInterrupting)
                {
                    StopInterrupt();
                }
                foreach (var slot in Slots)
                {
                    slot.ProcessAndSendMidi(64);
                }
            }
            else
            {
                AppendLog("--- System Unmuted ---");
                AppendLog($"Resuming with last sent value: {_lastSentMidiValue}");

                // Resend the last known value to ensure the device wakes up
                foreach (var slot in Slots)
                {
                    slot.ProcessAndSendMidi(_lastSentMidiValue);
                }
            }
        }

        private void PauseResumeButton_Click(object? sender, RoutedEventArgs e)
        {
            ToggleMute();
        }

        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            AppendLog("Window loaded. Initializing BLE slots...");
            foreach (var slot in Slots)
            {
                await slot.InitializeAsync();
            }
            AppendLog("BLE slot initialization finished.");

            // --- Start MIDI Listener on startup ---
            string? lastMidiDevice = Settings.Default.LastMidiDevice;
            if (!string.IsNullOrEmpty(lastMidiDevice))
            {
                GlobalMidi.StartListening(lastMidiDevice);
                AppendLog($"Attempting to start MIDI listener for: {lastMidiDevice}");
            }
            // ------------------------------------

            if (Settings.Default.LastPresetFilePaths != null)
            {
                foreach (var path in Settings.Default.LastPresetFilePaths)
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        if (SequencerViewModel.LoadPresetFromPath(path))
                        {
                            AppendLog($"Automatically loaded last used preset: {path}");
                        }
                    }
                }
            }

            if (!SequencerViewModel.UserPresets.Any())
            {
                SequencerViewModel.AddPreset(true);
                AppendLog("No preset found. Created a default preset.");
            }
        }

        private void MpcMonitorTimer_Tick(object? sender, EventArgs e)
        {
            if (IsGloballyPaused) return;

            if (DateTime.Now - _lastMpcUpdateTime > TimeSpan.FromSeconds(1))
            {
                if (!mpcMissingLogged)
                {
                    AppendLog("No MPC update detected for over 1 second. Stopping MIDI playback.");
                    _midiPlayer.Stop();
                    mpcMissingLogged = true;
                }
            }
            else
            {
                mpcMissingLogged = false;
            }
        }

        private bool ccUpdateSkippedLogged = false;
        private void UpdatePlayerController(string? controller, string? portStr)
        {
            if (_mpcController != null)
            {
                _mpcController.StopPolling();
                _mpcController = null;
                AppendLog("Existing Player Controller stopped.");
            }

            if (controller == "MPC" || controller == "DeoVR")
            {
                int port;
                if (!int.TryParse(portStr, out port))
                {
                    port = 13579;
                }
                _mpcController = new MpcController("127.0.0.1", port);
                _mpcController.StartPolling(50);
                AppendLog($"Player Controller changed to {controller} and started polling on port {port}.");

                _mpcController.PositionUpdated += pos =>
                {
                    if (IsGloballyPaused) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _lastMpcUpdateTime = DateTime.Now;
                        mpcMissingLogged = false;

                        if (!_midiFileLoaded)
                            return;

                        if (_isMpcPaused) return;
                        if (pos != _previousMpcPosition)
                        {
                            _previousMpcPosition = pos;
                        }
                        long midiPos = _midiPlayer.CurrentPosition;
                        long diff = pos - midiPos;
                        if (Math.Abs(diff) > 100 && pos != _lastSeekPosition)
                        {
                            if (!_midiPlayer.IsPlaying)
                            {
                                if (!ccUpdateSkippedLogged)
                                {
                                    AppendLog("MIDI is paused; forced CC update skipped.");
                                    ccUpdateSkippedLogged = true;
                                 }
                                return;
                            }
                            ccUpdateSkippedLogged = false;

                            _midiPlayer.Seek(pos);
                            AppendLog($"MIDI player seeked to {pos} ms (diff: {diff} ms)");
                            _lastSeekPosition = pos;

                            int newCc = _midiPlayer.GetCurrentCcValue();
                            AppendLog($"Forced new CC value after seek: {newCc}");
                            if (!_isInterrupting)
                            {
                                _lastSentMidiValue = newCc;
                                foreach (var slot in Slots)
                                {
                                    slot.ProcessAndSendMidi(newCc);
                                }
                            }

                            if (_lastMpcState == 2)
                            {
                                _midiPlayer.Resume();
                                AppendLog("MIDI resumed after seek.");
                            }
                        }
                    }));
                };

                _mpcController.StateChanged += state =>
                {
                    if (IsGloballyPaused) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AppendLog($"MPC state changed: {state}");
                        _lastMpcState = state; // Update the state immediately.

                        // Now, decide if we should act on this state change.
                        if (SequencerViewModel.IsPlaying && !isMidiPrioritized)
                        {
                            AppendLog("Sequencer overrides MIDI: Ignoring MPC state change for MIDI player control.");
                            if (_midiPlayer.IsPlaying)
                            {
                                _midiPlayer.Stop();
                                AppendLog("MIDI playback stopped as a safety measure under sequencer priority.");
                            }
                            return;
                        }

                        // Control the MIDI player based on the new state.
                        if (_midiFileLoaded)
                        {
                            switch (state)
                            {
                                case 0:
                                    _midiPlayer.Stop();
                                    AppendLog("MIDI playback stopped.");
                                    if (!_isInterrupting) foreach (var slot in Slots) { slot.ProcessAndSendMidi(64); }
                                    break;
                                case 1:
                                    _midiPlayer.Pause();
                                    AppendLog("MIDI playback paused.");
                                    if (!_isInterrupting) foreach (var slot in Slots) { slot.ProcessAndSendMidi(64); }
                                    break;
                                case 2:
                                    // Simplify the logic: always call Play().
                                    // If the player was paused, Play() should restart it correctly.
                                    // If it was stopped, it will start from the beginning (or last seek position).
                                    _midiPlayer.Play();
                                    AppendLog("MIDI playback started/resumed via Play().");
                                    break;
                            }
                        }
                        else
                        {
                            AppendLog("MPC state changed, but no MIDI file loaded. Ignoring.");
                        }
                    }));
                };

                _mpcController.PlaybackRateUpdated += rate =>
                {
                    if (IsGloballyPaused) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (rate != _previousPlaybackRate)
                        {
                            AppendLog($"Playback Rate: {rate}");
                            _previousPlaybackRate = rate;
                        }
                    }));
                };

                _mpcController.PlaybackPausedUpdated += paused =>
                {
                    if (IsGloballyPaused) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_isMpcPaused != paused)
                        {
                            _isMpcPaused = paused;
                            AppendLog($"MPC paused state changed: {paused}");
                        }
                    }));
                };

                _mpcController.LoopBoundaryDetected += () =>
                {
                    if (IsGloballyPaused) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (SequencerViewModel.IsPlaying && !isMidiPrioritized)
                        {
                            AppendLog("MPC loop boundary detected, but MIDI playback is suppressed by Sequencer.");
                            if (_midiPlayer.IsPlaying)
                            {
                                _midiPlayer.Stop();
                            }
                            return;
                        }

                        if (_lastMpcState == 2 && _midiFileLoaded)
                        {
                            AppendLog("MPC loop boundary detected → restarting MIDI playback.");
                            _midiPlayer.Stop();
                            _midiPlayer.Play();
                        }
                        else
                        {
                            AppendLog("MPC loop boundary detected, but either MPC is not playing or no MIDI file is loaded.");
                        }
                    }));
                };

                _mpcController.FileNameUpdated += OnFileNameUpdated;
            }
            else
            {
                AppendLog("Player Controller set to None. No polling started.");
            }
        }

        private void OnFileNameUpdated(string? videoFilePath)
        {
            if (IsGloballyPaused) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (string.IsNullOrEmpty(videoFilePath)) return;

                AppendLog($"MPC loaded: {videoFilePath}");
                string midiFilePath = Path.ChangeExtension(videoFilePath, ".mid");
                AppendLog($"Looking for MIDI file: {midiFilePath}");

                if (File.Exists(midiFilePath))
                {
                    _midiPlayer.Stop();
                    _midiPlayer.Load(midiFilePath);
                    AppendLog($"MIDI file loaded: {midiFilePath}");
                    _midiFileLoaded = true;

                    if (SequencerViewModel.IsPlaying && !isMidiPrioritized)
                    {
                        AppendLog("Sequencer is active and overrides MIDI. MIDI playback suppressed.");
                        return;
                    }

                    if (_lastMpcState == 2)
                    {
                        _midiPlayer.Play();
                        AppendLog("Auto-started MIDI playback (MPC is playing).");
                    }
                    else
                    {
                        AppendLog("MIDI is loaded but not started (MPC is not playing).");
                    }
                }
                else
                {
                    AppendLog($"No matching MIDI file found for {videoFilePath}");
                    _midiPlayer.Stop();
                    _midiFileLoaded = false;
                }
            }));
        }

        private void OnClickSettings(object? sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(Slots, GlobalMidi);
            win.PlayerControllerChanged += (controller, port) =>
            {
                UpdatePlayerController(controller, port);
            };
            win.ShowDialog();
        }

        private void MidiPriority_Checked(object? sender, RoutedEventArgs e)
        {
            // Guard against calls during UI initialization
            if (SequencerViewModel == null)
            {
                return;
            }

            if (sender is not RadioButton rb || rb.IsChecked != true) return;

            bool midiIsPrioritized = (rb.Name == "rbMidiPriority");

            if (this.isMidiPrioritized == midiIsPrioritized) return;

            this.isMidiPrioritized = midiIsPrioritized;
            AppendLog($"Sequencer/MIDI Priority changed. MIDI is prioritized: {this.isMidiPrioritized}");

            // --- Start of new logic ---

            // Case 1: Switched TO "MIDI playback prioritized"
            if (this.isMidiPrioritized)
            {
                // If sequencer is playing, it should no longer send data.
                // The check is in OnSequencerMidiValueGenerated.

                // If MPC is playing and a MIDI file is loaded, ensure the MIDI player is running.
                if (_lastMpcState == 2 && _midiFileLoaded)
                {
                    if (!_midiPlayer.IsPlaying)
                    {
                        _midiPlayer.Play();
                        AppendLog("MIDI playback (re)started due to priority change.");
                    }
                    // Force send current value to make it responsive
                    int currentCc = _midiPlayer.GetCurrentCcValue();
                    AppendLog($"Forcing current MIDI value on priority change: {currentCc}");
                    if (!_isInterrupting)
                    {
                        _lastSentMidiValue = currentCc;
                        foreach (var s in Slots)
                        {
                            s.ProcessAndSendMidi(currentCc);
                        }
                    }
                }
                // If MPC is not playing, the sequencer (if running) will take over as a fallback.
                // If sequencer is also not running, send a reset value.
                else if (!SequencerViewModel.IsPlaying)
                {
                    if (!_isInterrupting)
                    {
                        foreach (var s in Slots)
                        {
                            s.ProcessAndSendMidi(64);
                        }
                    }
                }
            }
            // Case 2: Switched TO "Sequencer overrides MIDI"
            else
            {
                // If MIDI player is running, stop it immediately.
                if (_midiPlayer.IsPlaying)
                {
                    _midiPlayer.Stop();
                    AppendLog("MIDI playback stopped due to priority change.");
                }

                // If sequencer is playing, its values will now be sent.
                // If not, send a reset value.
                if (!SequencerViewModel.IsPlaying)
                {
                    if (!_isInterrupting)
                    {
                        foreach (var s in Slots)
                        {
                            s.ProcessAndSendMidi(64);
                        }
                    }
                }
                // If sequencer is playing, we rely on the next generated value.
            }
            // --- End of new logic ---
        }

        private void AppendLog(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => AppendLog(text)));
                return;
            }

            string message = $"{DateTime.Now:HH:mm:ss} - {text}{Environment.NewLine}";
    
            txtLog.AppendText(message);

            if (txtLog.LineCount > 500)
            {
                int lineToRemove = txtLog.LineCount - 400;
                int firstLineIndex = txtLog.GetCharacterIndexFromLineIndex(lineToRemove);
                if (firstLineIndex > -1)
                {
                    txtLog.Text = txtLog.Text.Remove(0, firstLineIndex);
                }
            }
    
            txtLog.ScrollToEnd();
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // --- Cleanup ---
            UnregisterHotKeys();
            if (_source != null) _source.RemoveHook(HwndHook);

            AppendLog("MainWindow is closing. Stopping background tasks...");

            SaveSettings();

            _midiPlayer.Stop();

            if (_mpcController != null)
            {
                _mpcController.StopPolling();
            }
            if (SequencerViewModel != null)
            {
                SequencerViewModel.Dispose();
            }
            foreach (var slot in Slots)
            {
                slot.BleManager?.StopScan();
                slot.BleManager?.Disconnect();
            }
        }

        private void sldMidiDelay_ValueChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtMidiDelay == null)
            {
                return;
            }
            int delay = (int)e.NewValue;
            txtMidiDelay.Text = delay.ToString() + "ms";
            _midiPlayer.DelayOffsetMs = delay;
            AppendLog($"MIDI Delay updated to: {delay} ms");
        }

        private void chkAlwaysOnTop_Checked(object? sender, RoutedEventArgs e)
        {
            this.Topmost = true;
            AppendLog("Window set to always on top.");
        }

        private void chkAlwaysOnTop_Unchecked(object? sender, RoutedEventArgs e)
        {
            this.Topmost = false;
            AppendLog("Window no longer always on top.");
        }

        private void OnSequencerMidiValueGenerated(int ccValue)
        {
            if (IsGloballyPaused || _isInterrupting) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Only block if the sequencer is stopped AND the value is not the special stop value.
                if (!SequencerViewModel.IsPlaying && ccValue != 64) return;

                AppendLog($"Sequencer generated MIDI value: {ccValue}");
                _lastSentMidiValue = ccValue;

                if (!isMidiPrioritized) // false = Sequencer overrides MIDI
                {
                    if (_midiPlayer.IsPlaying)
                    {
                        _midiPlayer.Stop();
                        AppendLog("MIDI playback forcibly stopped due to Sequencer override.");
                    }
                    foreach (var slot in Slots)
                    {
                        slot.ProcessAndSendMidi(ccValue);
                    }
                }
                else // true = MIDI playback prioritized
                {
                    if (!_midiPlayer.IsPlaying)
                    {
                        foreach (var slot in Slots)
                        {
                            slot.ProcessAndSendMidi(ccValue);
                        }
                    }
                    // If MIDI player is playing, do nothing with the sequencer value.
                }
            }));
        }

        private void OnSequencerStopped()
        {
            AppendLog("Sequencer stopped.");
        }

        private void MainTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // This logic is no longer needed as there's no Auto Mode tab to switch from/to.
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length == 1 && Path.GetExtension(files[0]).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }

            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length == 1)
                {
                    var filePath = files[0];
                    if (Path.GetExtension(filePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        if (SequencerViewModel.LoadPresetFromPath(filePath))
                        {
                            AppendLog($"Loaded preset via drag-and-drop: {filePath}");
                        }
                        else
                        {
                            AppendLog($"Failed to load preset from: {filePath}");
                        }
                    }
                }
            }
        }

        // --- Interrupt Feature Methods ---
        private void InterruptButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_interruptMode == InterruptMode.Hold)
            {
                if (!_isInterrupting)
                {
                    StartInterrupt();
                }
            }
            else // Toggle Mode
            {
                if (_isInterrupting)
                {
                    StopInterrupt();
                }
                else
                {
                    StartInterrupt();
                }
            }
        }

        private void InterruptButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_interruptMode == InterruptMode.Hold)
            {
                if (_isInterrupting)
                {
                    StopInterrupt();
                }
            }
        }
        private void StartInterrupt()
        {
            if (IsGloballyPaused)
            {
                AppendLog("Cannot start interrupt while system is muted.");
                return;
            }

            if (_isInterrupting) return; // Already running

            _isInterrupting = true;
            AppendLog($"--- Interrupt Started: Duration: {_interruptDuration:F1}s, Intensity: {_interruptIntensity:F0}%, Direction: {_interruptDirection}, Mode: {_interruptMode} ---");

            if (_interruptMode == InterruptMode.Toggle)
            {
                _interruptTimer.Interval = TimeSpan.FromSeconds(_interruptDuration);
                _interruptTimer.Start();
            }

            SendInterruptMidi();
        }

        private void StopInterrupt()
        {
            if (!_isInterrupting) return; // Already stopped

            _interruptTimer.Stop();
            _isInterrupting = false;
            AppendLog("--- Interrupt Finished ---");

            // Restore the state based on the current context
            int valueToRestore;

            // First, check the highest priority source: a playing MIDI file.
            if (_midiFileLoaded && _midiPlayer.IsPlaying)
            {
                valueToRestore = _midiPlayer.GetCurrentCcValue();
                AppendLog($"Restoring to current MIDI player value: {valueToRestore}");
            }
            // If that's not running, check if the sequencer is playing. It's the fallback.
            else if (SequencerViewModel.IsPlaying)
            {
                // Sequencer is playing. Restore to its value.
                int? currentValue = SequencerViewModel.GetCurrentSequencerValue();
                if (currentValue.HasValue)
                {
                    valueToRestore = currentValue.Value;
                    AppendLog($"Restoring to current sequencer value: {valueToRestore}");
                }
                else
                {
                    // This is for constant patterns. Use the value from before the interrupt.
                    valueToRestore = _lastSentMidiValue;
                    AppendLog($"Sequencer has no new value. Restoring to pre-interrupt value: {valueToRestore}");
                }
            }
            else
            {
                // If neither is running, it's genuinely inactive. Restore to neutral.
                valueToRestore = 64;
                AppendLog($"Nothing active. Restoring to neutral value: {valueToRestore}");
            }

            _lastSentMidiValue = valueToRestore;
            foreach (var s in Slots)
            {
                s.ProcessAndSendMidi(valueToRestore);
            }
        }

        private void InterruptTimer_Tick(object? sender, EventArgs e)
        {
            // This tick now only serves to stop the interrupt in Toggle mode
            if (_interruptMode == InterruptMode.Toggle)
            {
                StopInterrupt();
            }
        }

        private void SendInterruptMidi()
        {
            if (!_isInterrupting) return;

            int midiValue;
            double intensityFactor = _interruptIntensity / 100.0;

            InterruptDirection finalDirection = _interruptDirection;
            if (finalDirection == InterruptDirection.FollowPrevious)
            {
                // 64 is neutral, > 64 is one direction, < 64 is another
                finalDirection = _lastSentMidiValue > 64 ? InterruptDirection.Clockwise : InterruptDirection.CounterClockwise;
            }

            if (finalDirection == InterruptDirection.Clockwise)
            {
                // Maps intensity [0, 100] to MIDI [64, 127]
                midiValue = 64 + (int)Math.Round(intensityFactor * 63);
            }
            else // CounterClockwise
            {
                // Maps intensity [0, 100] to MIDI [63, 0]
                midiValue = 63 - (int)Math.Round(intensityFactor * 63);
            }

            midiValue = Math.Clamp(midiValue, 0, 127);
            
            // We don't update _lastSentMidiValue here, to keep the pre-interrupt direction for "Follow"
            // AppendLog($"Sending Interrupt MIDI value: {midiValue}"); // This is too verbose
            foreach (var slot in Slots)
            {
                slot.ProcessAndSendMidi(midiValue);
            }

            // For continuous signal, re-trigger SendInterruptMidi
            // This is better than a fast timer tick for responsiveness
            if (_isInterrupting)
            {
                Dispatcher.BeginInvoke(new Action(SendInterruptMidi), DispatcherPriority.ContextIdle);
            }
        }


        private void sldInterruptDuration_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtInterruptDuration == null) return;
            _interruptDuration = e.NewValue;
            txtInterruptDuration.Text = $"{_interruptDuration:F1}s";
        }

        private void sldInterruptIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtInterruptIntensity == null) return;
            _interruptIntensity = e.NewValue;
            txtInterruptIntensity.Text = $"{_interruptIntensity:F0}%";
        }

        private void InterruptDirection_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.IsChecked != true) return;

            if (rb.Name == "rbInterruptDirCw")
                _interruptDirection = InterruptDirection.Clockwise;
            else if (rb.Name == "rbInterruptDirCcw")
                _interruptDirection = InterruptDirection.CounterClockwise;
            else if (rb.Name == "rbInterruptDirFollow")
                _interruptDirection = InterruptDirection.FollowPrevious;
        }

        private void InterruptMode_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton rb || rb.IsChecked != true) return;

            if (rb.Name == "rbInterruptModeToggle")
                _interruptMode = InterruptMode.Toggle;
            else if (rb.Name == "rbInterruptModeHold")
                _interruptMode = InterruptMode.Hold;
        }


        /// <summary>
        /// デバッグ用: 全てのスロットのGainとOffsetをリセットする
        /// </summary>
        private void ResetGainOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            AppendLog("--- Resetting Gain and Offset for all slots ---");
            foreach (var slot in Slots)
            {
                // 1. GainValueプロパティをリセット
                slot.GainValue = 0;
                AppendLog($"{slot.SlotName}: GainValue reset to 0.");

                // 2. "OffsetValue" というプロパティが存在するか不明なため、
                //    リフレクションを使って安全にリセットを試みる
                var offsetProperty = slot.GetType().GetProperty("OffsetValue");
                if (offsetProperty != null && offsetProperty.CanWrite)
                {
                    // OffsetValueプロパティが見つかった場合、値を0に設定
                    offsetProperty.SetValue(slot, 0.0);
                    AppendLog($"{slot.SlotName}: OffsetValue (found via reflection) reset to 0.");
                }
            }
        }
    }
}
