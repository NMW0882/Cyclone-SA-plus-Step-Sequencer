using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace MidiBleWpfSample.Services.PlayerControllers
{
    public class MpcController
    {
        private readonly HttpClient _httpClient;
        private readonly string _host;
        private readonly int _port;
        private CancellationTokenSource? _cancellationTokenSource;
        private int _pollIntervalMs;

        // 前回取得した再生位置
        private long _previousPosition = 0;
        // 前回取得した state
        private int _previousState = -1;
        // 前回取得したファイルパス
        private string? _previousFilePath;

        // 各種イベント
        public event Action<long>? PositionUpdated;
        public event Action<double>? PlaybackRateUpdated;
        public event Action<bool>? PlaybackPausedUpdated;  // 既存の paused イベント
        public event Action? LoopBoundaryDetected;

        /// <summary>
        /// state が変わったら通知するイベント (-1,0,1,2)
        /// </summary>
        public event Action<int>? StateChanged;

        /// <summary>
        /// ファイルパスが更新されたときに通知するイベント (state != -1の場合)
        /// </summary>
        public event Action<string>? FileNameUpdated;

        public MpcController(string host = "127.0.0.1", int port = 13579)
        {
            _host = host;
            _port = port;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        }

        private string BaseUrl => $"http://{_host}:{_port}";

        public void StartPolling(int intervalMs = 50)
        {
            _pollIntervalMs = intervalMs;
            _cancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => PollLoop(_cancellationTokenSource.Token));
        }

        public void StopPolling()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task PollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    string url = $"{BaseUrl}/variables.html";
                    var response = await _httpClient.GetAsync(url, token);
                    if (response.IsSuccessStatusCode)
                    {
                        string html = await response.Content.ReadAsStringAsync();

                        // (1) 再生位置
                        long pos = ParsePosition(html);
                        // ループ境界と判断するのは、前回との差が1000ms以上かつ、現在の位置が1000ms未満の場合に限定
                        if (_previousPosition > 0 && (_previousPosition - pos) > 1000 && pos < 1000)
                        {
                            LoopBoundaryDetected?.Invoke();
                        }
                        _previousPosition = pos;
                        PositionUpdated?.Invoke(pos);

                        // (2) 再生速度
                        double rate = ParsePlaybackRate(html);
                        PlaybackRateUpdated?.Invoke(rate);

                        // (3) paused フラグ
                        bool isPaused = ParsePaused(html);
                        PlaybackPausedUpdated?.Invoke(isPaused);

                        // (4) state
                        int currentState = ParseState(html);
                        if (currentState != _previousState)
                        {
                            // state が変わったらイベント通知
                            _previousState = currentState;
                            StateChanged?.Invoke(currentState);
                        }

                        // (5) filepath
                        // state が -1(未ロード) の場合はスキップする
                        if (currentState != -1)
                        {
                            string filePath = ParseFilePath(html);
                            if (!string.IsNullOrEmpty(filePath) && filePath != _previousFilePath)
                            {
                                _previousFilePath = filePath;
                                FileNameUpdated?.Invoke(filePath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MpcController] Polling Error: {ex.Message}");
                }
                await Task.Delay(_pollIntervalMs, token);
            }
        }

        // --- 各種パースメソッド ---

        private long ParsePosition(string html)
        {
            var match = Regex.Match(html, @"<p\s+id\s*=\s*""position"">\s*(\d+)\s*</p>", RegexOptions.IgnoreCase);
            if (match.Success && long.TryParse(match.Groups[1].Value, out long pos))
                return pos;
            return 0;
        }

        private double ParsePlaybackRate(string html)
        {
            var match = Regex.Match(html, @"<p\s+id\s*=\s*""playbackrate"">\s*([\d\.]+)\s*</p>", RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value, out double rate))
                return rate;
            return 1.0;
        }

        private bool ParsePaused(string html)
        {
            var match = Regex.Match(html, @"<p\s+id\s*=\s*""paused"">\s*(\d+)\s*</p>", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int paused))
                return (paused == 1);
            return false;
        }

        /// <summary>
        /// <p id="state">-1,0,1,2</p>
        /// -1:未ロード, 0:停止, 1:一時停止, 2:再生
        /// </summary>
        private int ParseState(string html)
        {
            var match = Regex.Match(html, @"<p\s+id\s*=\s*""state"">\s*(-?\d+)\s*</p>", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int st))
                return st;
            return -1; // 取得失敗時は -1 扱い
        }

        private string? ParseFilePath(string html)
        {
            var match = Regex.Match(html, @"<p\s+id\s*=\s*""filepath"">\s*(.*?)\s*</p>", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return null;
        }

        /// <summary>
        /// WM_COMMAND を送るサンプル (必要に応じて)
        /// </summary>
        public async Task SendCommandAsync(int wm_command)
        {
            try
            {
                string url = $"{BaseUrl}/command.html?wm_command={wm_command}";
                await _httpClient.GetAsync(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SendCommandAsync Error: {ex.Message}");
            }
        }
    }
}
