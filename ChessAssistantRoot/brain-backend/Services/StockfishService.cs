using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using BrainBackend.Models;
using Microsoft.Extensions.Logging;

namespace BrainBackend.Services;

/// <summary>
/// Wrapper cho Stockfish process. Spawn 1 process khi start, giao tiếp qua stdin/stdout.
/// Auto-restart nếu process crash.
/// </summary>
public class StockfishService : IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<StockfishService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private CancellationTokenSource? _currentCts;

    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private bool _disposed;

    // Config
    private readonly int _multiPv;
    private readonly int _depth;
    private readonly int _threads;
    private readonly int _hashMb;
    private readonly int _timeoutMs;
    private readonly string _enginePath;

    public bool IsAlive => _process is { HasExited: false };

    public StockfishService(IConfiguration config, ILogger<StockfishService> logger)
    {
        _config = config;
        _logger = logger;

        _multiPv = config.GetValue("Stockfish:MultiPV", 3);
        _depth = config.GetValue("Stockfish:Depth", 15);
        _threads = config.GetValue("Stockfish:Threads", 2);
        _hashMb = config.GetValue("Stockfish:HashMB", 128);
        _timeoutMs = config.GetValue("Stockfish:TimeoutMs", 8000);
        _enginePath = config.GetValue("Stockfish:EnginePath", "Engine/stockfish.exe")!;

        StartProcess();
    }

    private void StartProcess()
    {
        try
        {
            var exePath = Path.IsPathRooted(_enginePath)
                ? _enginePath
                : Path.Combine(AppContext.BaseDirectory, _enginePath);

            if (!File.Exists(exePath))
            {
                _logger.LogCritical("[StockfishService] Stockfish binary not found at: {Path}", exePath);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _process = new Process { StartInfo = psi };
            _process.Exited += OnProcessExited;
            _process.EnableRaisingEvents = true;
            _process.Start();

            _stdin = _process.StandardInput;
            _stdout = _process.StandardOutput;

            // UCI handshake
            SendCommand("uci");
            WaitForResponse("uciok", 3000);
            SendCommand($"setoption name MultiPV value {_multiPv}");
            SendCommand($"setoption name Threads value {_threads}");
            SendCommand($"setoption name Hash value {_hashMb}");
            SendCommand("isready");
            WaitForResponse("readyok", 3000);

            _logger.LogInformation("[StockfishService] Stockfish started. MultiPV={MultiPV}, Depth={Depth}, Threads={Threads}",
                _multiPv, _depth, _threads);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "[StockfishService] Failed to start Stockfish.");
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        _logger.LogCritical("[StockfishService] Stockfish process died, restarting...");
        Thread.Sleep(1000);
        StartProcess();
    }

    private void SendCommand(string cmd)
    {
        _stdin?.WriteLine(cmd);
        _stdin?.Flush();
    }

    private void WaitForResponse(string token, int timeoutMs)
    {
        if (_stdout == null) return;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            var line = _stdout.ReadLine();
            if (line != null && line.Contains(token)) return;
        }
    }

    /// <summary>
    /// Phân tích FEN, trả về list MoveInfo (MultiPV).
    /// Returns null nếu Stockfish không respond kịp timeout.
    /// </summary>
    public async Task<List<MoveInfo>?> AnalyzeAsync(string fen, CancellationToken ct = default)
    {
        if (!IsAlive)
        {
            _logger.LogError("[StockfishService] Stockfish is not running.");
            return null;
        }

        // Cancel previous search if any
        _currentCts?.Cancel();
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _currentCts.Token;

        await _lock.WaitAsync(ct);
        try
        {
            if (linkedCt.IsCancellationRequested) return null;
            return await Task.Run(() => AnalyzeInternal(fen, linkedCt), linkedCt);
        }
        finally
        {
            _lock.Release();
        }
    }

    private List<MoveInfo>? AnalyzeInternal(string fen, CancellationToken ct)
    {
        if (_stdout == null || _stdin == null) return null;

        try
        {
            // The semaphore guarantees no concurrent searches.
            // Previous search completed (received bestmove) before lock was released.
            // So stdout is clean — no need to send "stop".
            // Just sync with "isready" to be absolutely sure.
            SendCommand("isready");

            // Wait for readyok (with timeout via async ReadLine)
            var readySw = Stopwatch.StartNew();
            while (readySw.ElapsedMilliseconds < 1000)
            {
                if (ct.IsCancellationRequested) return null;
                var readTask = Task.Run(() => _stdout!.ReadLine());
                if (readTask.Wait(TimeSpan.FromMilliseconds(1000 - readySw.ElapsedMilliseconds)))
                {
                    var line = readTask.Result;
                    if (line == null) break;
                    if (line.StartsWith("readyok")) break;
                }
                else break; // timeout
            }

            // Start search
            SendCommand($"position fen {fen}");
            // Use movetime for consistent speed. 600ms is a good balance.
            SendCommand($"go movetime 600");

            var results = new Dictionary<int, MoveInfo>();
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < _timeoutMs && !ct.IsCancellationRequested)
            {
                // Use async ReadLine with timeout to prevent permanent blocking
                var readTask = Task.Run(() => _stdout!.ReadLine());
                var remaining = _timeoutMs - (int)sw.ElapsedMilliseconds;
                if (remaining <= 0) break;

                if (!readTask.Wait(TimeSpan.FromMilliseconds(remaining)))
                {
                    // Timeout — send stop to unblock Stockfish
                    SendCommand("stop");
                    // Try to read the bestmove response (max 500ms)
                    readTask.Wait(TimeSpan.FromMilliseconds(500));
                    break;
                }

                var line = readTask.Result;
                if (line == null) break;
                if (line.StartsWith("bestmove")) break;
                if (!line.StartsWith("info") || !line.Contains("multipv")) continue;

                var pvNum = ParseInt(line, "multipv");
                if (pvNum <= 0) continue;

                var depth = ParseInt(line, "depth");
                var score = ParseScore(line);
                var move  = ParseFirstMove(line);

                if (move != null)
                {
                    results[pvNum] = new MoveInfo { Move = move, Score = score, Depth = depth };
                }
            }

            if (ct.IsCancellationRequested)
            {
                SendCommand("stop");
                _stdout.ReadLine(); // consume bestmove
                return null;
            }

            return results.Count > 0
                ? results.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList()
                : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StockfishService] Error during analysis of FEN: {Fen}", fen);
            return null;
        }
    }

    private static int ParseInt(string line, string token)
    {
        var idx = line.IndexOf(token, StringComparison.Ordinal);
        if (idx < 0) return 0;
        var rest = line[(idx + token.Length)..].TrimStart();
        var end = rest.IndexOf(' ');
        var numStr = end < 0 ? rest : rest[..end];
        return int.TryParse(numStr, out var val) ? val : 0;
    }

    private static int ParseScore(string line)
    {
        // "score cp 45" hoặc "score mate 3"
        var cpIdx = line.IndexOf("score cp", StringComparison.Ordinal);
        if (cpIdx >= 0)
        {
            var rest = line[(cpIdx + 8)..].TrimStart();
            var end = rest.IndexOf(' ');
            var numStr = end < 0 ? rest : rest[..end];
            if (int.TryParse(numStr, out var cp)) return cp;
        }

        var mateIdx = line.IndexOf("score mate", StringComparison.Ordinal);
        if (mateIdx >= 0)
        {
            var rest = line[(mateIdx + 10)..].TrimStart();
            var end = rest.IndexOf(' ');
            var numStr = end < 0 ? rest : rest[..end];
            if (int.TryParse(numStr, out var mate))
                return mate > 0 ? 30000 : -30000; // đại diện cho mate
        }

        return 0;
    }

    private static string? ParseFirstMove(string line)
    {
        // " pv e2e4 d7d5 ..." → "e2e4"
        var pvIdx = line.IndexOf(" pv ", StringComparison.Ordinal);
        if (pvIdx < 0) return null;
        var rest = line[(pvIdx + 4)..].TrimStart();
        var end = rest.IndexOf(' ');
        return end < 0 ? rest : rest[..end];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            SendCommand("quit");
            _process?.WaitForExit(2000);
            _process?.Kill();
            _process?.Dispose();
        }
        catch { /* ignore */ }

        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
