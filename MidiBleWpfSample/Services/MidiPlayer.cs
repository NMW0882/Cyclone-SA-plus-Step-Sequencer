using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers; // System.Timers.Timer を使用
using Timer = System.Timers.Timer;
using NAudio.Midi;

namespace MidiBleWpfSample.Services
{
    /// <summary>
    /// MIDI イベントとその絶対時間（ms）およびシーケンス番号を保持するラッパークラス
    /// </summary>
    public class TimedMidiEvent
    {
        public required MidiEvent MidiEvent { get; set; }
        public double AbsoluteTimeMs { get; set; }
        public int SequenceNumber { get; set; }
    }

    /// <summary>
    /// MIDI ファイル再生エンジン
    /// ・MIDI ファイルを読み込み、各イベントの再生タイミングを計算する
    /// ・Stopwatch と Timer を用いて再生・一時停止・再開・シークを管理する
    /// DelayOffsetMs を各イベントの発火予定時刻に加算し、
    /// 負の値の場合はイベントの発火タイミングを前倒しにします。
    /// </summary>
    public class MidiPlayer
    {
        private MidiFile? midiFile;
        private List<TimedMidiEvent>? events; // フラット化した MIDI イベントリスト
        private int currentEventIndex;
        private Stopwatch stopwatch;
        private Timer timer;
        private bool isPaused; // 一時停止状態かどうか
        private bool seekNoMidiLogged = false;

        /// <summary>
        /// シーク時に指定された再生位置(ms)に合わせるためのオフセット
        /// </summary>
        private long seekOffset = 0;

        /// <summary>
        /// ユーザー設定の再生全体のディレイ（ms）。
        /// 正なら遅延、負なら早送りのように各イベントの予定時刻に加算される
        /// </summary>
        public double DelayOffsetMs { get; set; } = -250;

        /// <summary>
        /// 再生すべき MIDI イベントを通知するイベント
        /// </summary>
        public event Action<TimedMidiEvent>? MidiEventReady;

        public MidiPlayer()
        {
            stopwatch = new Stopwatch();
            timer = new Timer(10);  // 10ms ごとのポーリング
            timer.Elapsed += Timer_Elapsed;
            isPaused = false;
        }

        /// <summary>
        /// 指定された MIDI ファイルを読み込み、イベントリストを生成する
        /// </summary>
        public void Load(string filePath)
        {
            midiFile = new MidiFile(filePath, false);
            events = FlattenMidiEvents(midiFile);
            currentEventIndex = 0;
            // シークオフセットをリセット
            seekOffset = 0;
            seekNoMidiLogged = false; // ロード時にフラグリセット
        }

        /// <summary>
        /// MIDI ファイル内の各トラックのイベントをフラットにまとめ、
        /// 各イベントの絶対時間（ms）とシーケンス番号を計算してリスト化する。
        /// ※ 簡易実装として一定のテンポ（500,000μs = 120 BPM）で計算
        /// </summary>
        private List<TimedMidiEvent> FlattenMidiEvents(MidiFile file)
        {
            double defaultTempo = 500000; // 500,000 μs per quarter note (120 BPM)
            double ticksPerQuarter = file.DeltaTicksPerQuarterNote;
            double msPerTick = (defaultTempo / 1000.0) / ticksPerQuarter;

            List<TimedMidiEvent> list = new List<TimedMidiEvent>();
            int seq = 0;
            foreach (IList<MidiEvent> track in file.Events)
            {
                long absoluteTick = 0;
                foreach (MidiEvent midiEvent in track)
                {
                    absoluteTick += midiEvent.DeltaTime;
                    double absoluteMs = absoluteTick * msPerTick;
                    list.Add(new TimedMidiEvent
                    {
                        MidiEvent = midiEvent,
                        AbsoluteTimeMs = absoluteMs,
                        SequenceNumber = seq++
                    });
                }
            }
            list.Sort((a, b) =>
            {
                int cmp = a.AbsoluteTimeMs.CompareTo(b.AbsoluteTimeMs);
                if (cmp == 0)
                    cmp = a.SequenceNumber.CompareTo(b.SequenceNumber);
                return cmp;
            });
            return list;
        }

        /// <summary>
        /// Timer の Elapsed イベントハンドラー。
        /// Stopwatch の経過時間＋ seekOffset＋ DelayOffsetMs を用いて、各イベントの発火予定時刻と比較し、
        /// 該当するイベントを MidiEventReady イベントとして発火する。
        /// </summary>
        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (events == null)
                return;
            if (isPaused)
                return;

            double elapsedMs = stopwatch.ElapsedMilliseconds + seekOffset + DelayOffsetMs;
            // 負の値にならないようにクランプ
            if (elapsedMs < 0) elapsedMs = 0;

            while (currentEventIndex < events.Count)
            {
                // 各イベントの予定発火時刻に DelayOffsetMs を加算
                double scheduledTime = events[currentEventIndex].AbsoluteTimeMs + DelayOffsetMs;
                if (scheduledTime <= elapsedMs)
                {
                    MidiEventReady?.Invoke(events[currentEventIndex]);
                    currentEventIndex++;
                }
                else
                {
                    break;
                }
            }
            if (currentEventIndex >= events.Count)
            {
                Stop();
            }
        }

        /// <summary>
        /// 再生を開始または再開する。
        /// ※このメソッドは再生中でなければ（Stop/Resume）タイマーを開始する。
        /// </summary>
        public void Play()
        {
            isPaused = false;
            stopwatch.Start();
            timer.Start();
        }

        /// <summary>
        /// 再生を一時停止する。再生位置は保持される。
        /// </summary>
        public void Pause()
        {
            if (!isPaused)
            {
                isPaused = true;
                timer.Stop();
                stopwatch.Stop();
            }
        }

        /// <summary>
        /// 一時停止状態から再生を再開する。
        /// </summary>
        public void Resume()
        {
            if (isPaused)
            {
                isPaused = false;
                stopwatch.Start();
                timer.Start();
            }
        }

        /// <summary>
        /// 再生を停止し、再生位置を先頭に戻す。
        /// </summary>
        public void Stop()
        {
            timer.Stop();
            stopwatch.Stop();
            currentEventIndex = 0;
            seekOffset = 0;
            isPaused = false;
        }

        /// <summary>
        /// 指定された再生位置（ms）にシークする。
        /// シーク後は、再生中ならその状態を継続し、停止・一時停止中ならそのまま維持する。
        /// DelayOffsetMs が負の場合、CurrentPosition を目標値 ms に合わせるように調整する。
        /// </summary>
        public void Seek(long ms)
        {
            if (events == null)
            {
                if (!seekNoMidiLogged)
                {
                    Debug.WriteLine("MIDIPlayer.Seek called but no MIDI file is loaded.");
                    seekNoMidiLogged = true;
                }
                return;
            }

            // MIDIファイルがロードされている場合はフラグをリセット
            seekNoMidiLogged = false;

            bool wasPlaying = timer.Enabled && !isPaused;
            int index = events.FindIndex(ev => ev.AbsoluteTimeMs >= ms);
            currentEventIndex = (index >= 0) ? index : events.Count;

            // 目標の CurrentPosition = stopwatch.Elapsed + seekOffset + DelayOffsetMs
            // シーク直後は stopwatch.Elapsed = 0 なので、理想的には seekOffset + DelayOffsetMs = ms
            // よって seekOffset = ms - DelayOffsetMs（DelayOffsetMs が負の場合も含む）
            seekOffset = ms - (long)DelayOffsetMs;

            stopwatch.Reset();
            stopwatch.Start();
            timer.Stop();
            if (wasPlaying)
            {
                timer.Start();
            }
        }


        /// <summary>
        /// 現在の MIDI 再生位置（ms）を返す。
        /// 計算式: stopwatch.ElapsedMilliseconds + seekOffset + DelayOffsetMs, 負の場合は 0 にクランプする。
        /// </summary>
        public long CurrentPosition
        {
            get
            {
                long pos = stopwatch.ElapsedMilliseconds + seekOffset + (long)DelayOffsetMs;
                return pos < 0 ? 0 : pos;
            }
        }

        public bool IsPlaying
        {
            get { return timer.Enabled && !isPaused; }
        }

        public int GetCurrentCcValue()
        {
            // イベントリストが空の場合はセンター値を返す
            if (events == null || events.Count == 0)
                return 64;

            long currentPos = CurrentPosition; // 現在の再生位置（ms）
                                               // 後ろから走査して、再生位置以下の最新のCCイベントを探す
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].AbsoluteTimeMs <= currentPos)
                {
                    if (events[i].MidiEvent is ControlChangeEvent ccEvent && (int)ccEvent.Controller == 10)
                    {
                        return ccEvent.ControllerValue;
                    }
                }
            }
            // 見つからなければ、デフォルト値（センター値）を返す
            return 64;
        }

    }
}
