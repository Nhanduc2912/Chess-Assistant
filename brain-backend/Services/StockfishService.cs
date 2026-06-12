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

    // Guards concurrent restart vs analysis: startup MUST hold _lock so that
    // AnalyzeInternal() cannot read _stdout while StartProcess() is consuming it.
    // Volatile so IsAlive check sees latest state across threads.
    private volatile bool _isRestarting = false;

    // Thống kê runtime
    private int _restartCount = 0;
    private int _totalAnalyzed = 0;
    private int _totalErrors = 0;
    private int _totalCancelled = 0;
    private DateTime _startedAt;

    // Config
    private readonly int _multiPv;
    private readonly int _depth;
    private readonly int _threads;
    private readonly int _hashMb;
    private readonly int _timeoutMs;
    private readonly string _enginePath;

    public bool IsAlive      => _process is { HasExited: false } && !_isRestarting;
    public bool IsRestarting  => _isRestarting;

    public StockfishService(IConfiguration config, ILogger<StockfishService> logger)
    {
        _config = config;
        _logger = logger;

        _multiPv     = config.GetValue("Stockfish:MultiPV", 3);
        _depth       = config.GetValue("Stockfish:Depth", 15);
        _threads     = config.GetValue("Stockfish:Threads", 2);
        _hashMb      = config.GetValue("Stockfish:HashMB", 128);
        _timeoutMs   = config.GetValue("Stockfish:TimeoutMs", 8000);
        _enginePath  = config.GetValue("Stockfish:EnginePath", "Engine/stockfish.exe")!;

        _logger.LogInformation(
            "[StockfishService] Initializing with config → MultiPV={MultiPV} | Depth={Depth} | Threads={Threads} | Hash={Hash}MB | Timeout={Timeout}ms",
            _multiPv, _depth, _threads, _hashMb, _timeoutMs);

        StartProcess();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Process lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    private void StartProcess()
    {
        try
        {
            var exePath = Path.IsPathRooted(_enginePath)
                ? _enginePath
                : Path.Combine(AppContext.BaseDirectory, _enginePath);

            if (!File.Exists(exePath))
            {
                _logger.LogCritical(
                    "[StockfishService] ✗ Engine binary NOT FOUND at: {Path}" +
                    "\n  → Check 'Stockfish:EnginePath' in appsettings.json" +
                    "\n  → BaseDir: {Base}",
                    exePath, AppContext.BaseDirectory);
                return;
            }

            _logger.LogDebug("[StockfishService] Starting engine: {Path}", exePath);

            var psi = new ProcessStartInfo
            {
                FileName             = exePath,
                UseShellExecute      = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow       = true
            };

            _process = new Process { StartInfo = psi };
            _process.Exited += OnProcessExited;
            _process.EnableRaisingEvents = true;
            _process.Start();
            _startedAt = DateTime.UtcNow;

            _stdin  = _process.StandardInput;
            _stdout = _process.StandardOutput;

            // UCI handshake
            _logger.LogDebug("[StockfishService] UCI handshake in progress...");
            SendCommand("uci");
            WaitForResponse("uciok", 3000);
            SendCommand($"setoption name MultiPV value {_multiPv}");
            SendCommand($"setoption name Threads value {_threads}");
            SendCommand($"setoption name Hash value {_hashMb}");
            SendCommand("isready");
            WaitForResponse("readyok", 3000);

            _logger.LogInformation(
                "[StockfishService] ✓ Engine READY — PID={PID} | MultiPV={MultiPV} | Depth={Depth} | Threads={Threads} | Hash={Hash}MB | Timeout={Timeout}ms | Restart#{Restart}",
                _process.Id, _multiPv, _depth, _threads, _hashMb, _timeoutMs, _restartCount);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "[StockfishService] ✗ FAILED to start Stockfish engine" +
                "\n  → EnginePath: {Path}" +
                "\n  → This will cause all analysis requests to fail",
                _enginePath);
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        if (_disposed) return;

        var uptime = DateTime.UtcNow - _startedAt;
        _restartCount++;
        _isRestarting = true;

        // Cancel any in-flight analysis so AnalyzeInternal returns fast and releases _lock
        _currentCts?.Cancel();

        _logger.LogCritical(
            "[StockfishService] ✗ Engine process DIED unexpectedly" +
            "\n  → ExitCode: {ExitCode}" +
            "\n  → Uptime: {Uptime:hh\\:mm\\:ss}" +
            "\n  → Restart attempt: #{Restart}" +
            "\n  → Stats: Analyzed={Analyzed} | Errors={Errors} | Cancelled={Cancelled}" +
            "\n  → Waiting for lock before restart...",
            _process?.ExitCode, uptime, _restartCount,
            _totalAnalyzed, _totalErrors, _totalCancelled);

        // ─── CRITICAL FIX ─────────────────────────────────────────────────────
        // Acquire the analysis lock BEFORE StartProcess().
        // StartProcess() reads from _stdout (UCI handshake) via WaitForResponse().
        // AnalyzeInternal() ALSO reads from _stdout (readyok wait + search loop).
        // StreamReader is NOT thread-safe — concurrent reads cause ArgumentOutOfRangeException.
        // Holding _lock here ensures StartProcess() and AnalyzeInternal() never
        // read _stdout at the same time.
        // ─────────────────────────────────────────────────────────────────────
        _lock.Wait(); // blocking — wait for any in-flight AnalyzeInternal to finish
        try
        {
            Thread.Sleep(500); // brief pause before restarting
            StartProcess();
        }
        finally
        {
            _isRestarting = false;
            _lock.Release();
            _logger.LogInformation(
                "[StockfishService] Lock released after restart — engine ready for new requests");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // I/O helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void SendCommand(string cmd)
    {
        try
        {
            _stdin?.WriteLine(cmd);
            _stdin?.Flush();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StockfishService] Failed to send command '{Cmd}' — pipe may be closed", cmd);
        }
    }

    private void WaitForResponse(string token, int timeoutMs)
    {
        if (_stdout == null) return;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                var line = _stdout.ReadLine();
                if (line == null) break; // stream closed
                if (line.Contains(token)) return;
            }
            catch
            {
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public analysis API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Phân tích FEN, trả về list MoveInfo (MultiPV).
    /// Returns null nếu Stockfish không respond kịp timeout.
    /// </summary>
    public async Task<List<MoveInfo>?> AnalyzeAsync(string fen, CancellationToken ct = default)
    {
        if (!IsAlive)
        {
            _logger.LogError(
                "[StockfishService] ✗ Engine NOT RUNNING — cannot analyze FEN: {Fen}" +
                "\n  → IsAlive={Alive} | RestartCount={Restart}",
                fen, IsAlive, _restartCount);
            return null;
        }

        // Cancel previous search if any
        _currentCts?.Cancel();
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _currentCts.Token;

        await _lock.WaitAsync(ct);
        try
        {
            if (linkedCt.IsCancellationRequested)
            {
                Interlocked.Increment(ref _totalCancelled);
                return null;
            }
            return await Task.Run(() => AnalyzeInternal(fen, linkedCt), linkedCt);
        }
        finally
        {
            _lock.Release();
        }
    }

    private List<MoveInfo>? AnalyzeInternal(string fen, CancellationToken ct)
    {
        if (_stdout == null || _stdin == null)
        {
            _logger.LogError("[StockfishService] ✗ Streams are null — engine not initialized properly");
            return null;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            // Sync with isready before sending position
            SendCommand("isready");

            // Wait for readyok (with timeout)
            var readySw = Stopwatch.StartNew();
            while (readySw.ElapsedMilliseconds < 1000)
            {
                if (ct.IsCancellationRequested) return null;

                string? readLine = null;
                try
                {
                    var readTask = Task.Run(() => _stdout!.ReadLine());
                    if (readTask.Wait(TimeSpan.FromMilliseconds(1000 - readySw.ElapsedMilliseconds)))
                    {
                        readLine = readTask.Result;
                    }
                }
                catch (AggregateException aex) when (aex.InnerException is ArgumentOutOfRangeException or ObjectDisposedException or InvalidOperationException)
                {
                    _logger.LogWarning(
                        "[StockfishService] Stream read failed during readyok wait — engine likely died" +
                        "\n  → FEN: {Fen}" +
                        "\n  → Cause: {Cause}",
                        fen, aex.InnerException.GetType().Name);
                    return null;
                }

                if (readLine == null) break;
                if (readLine.StartsWith("readyok")) break;
            }

            // Start search
            SendCommand($"position fen {fen}");
            SendCommand("go movetime 600");

            _logger.LogDebug(
                "[StockfishService] → Search started | FEN: {Fen} | movetime=600ms | timeout={Timeout}ms",
                fen, _timeoutMs);

            var results   = new Dictionary<int, MoveInfo>();
            var searchSw  = Stopwatch.StartNew();

            while (searchSw.ElapsedMilliseconds < _timeoutMs && !ct.IsCancellationRequested)
            {
                string? line = null;
                var remaining = _timeoutMs - (int)searchSw.ElapsedMilliseconds;
                if (remaining <= 0) break;

                try
                {
                    var readTask = Task.Run(() => _stdout!.ReadLine());
                    if (!readTask.Wait(TimeSpan.FromMilliseconds(remaining)))
                    {
                        // Timeout — send stop
                        _logger.LogWarning(
                            "[StockfishService] ⚠ Search timeout reached ({Timeout}ms) for FEN: {Fen}" +
                            "\n  → Sending stop, partial results: {Count} PVs found",
                            _timeoutMs, fen, results.Count);
                        SendCommand("stop");
                        readTask.Wait(TimeSpan.FromMilliseconds(500));
                        break;
                    }
                    line = readTask.Result;
                }
                catch (AggregateException aex) when (aex.InnerException is ArgumentOutOfRangeException
                                                   or ObjectDisposedException
                                                   or InvalidOperationException)
                {
                    // Stockfish process died mid-read — stream is disposed
                    Interlocked.Increment(ref _totalErrors);
                    _logger.LogError(
                        "[StockfishService] ✗ Stream closed mid-analysis (engine died)" +
                        "\n  → FEN: {Fen}" +
                        "\n  → Elapsed: {Elapsed}ms" +
                        "\n  → Partial results: {Count} PVs" +
                        "\n  → Exception: {ExType}: {ExMsg}" +
                        "\n  → The engine will be restarted automatically by OnProcessExited",
                        fen, searchSw.ElapsedMilliseconds, results.Count,
                        aex.InnerException.GetType().Name, aex.InnerException.Message);
                    return null;
                }

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
                Interlocked.Increment(ref _totalCancelled);
                SendCommand("stop");
                try { _stdout.ReadLine(); } catch { /* consume bestmove, ignore errors */ }
                _logger.LogDebug(
                    "[StockfishService] Analysis cancelled for FEN: {Fen} after {Elapsed}ms",
                    fen, sw.ElapsedMilliseconds);
                return null;
            }

            if (results.Count == 0)
            {
                Interlocked.Increment(ref _totalErrors);
                _logger.LogWarning(
                    "[StockfishService] ⚠ No results returned for FEN: {Fen}" +
                    "\n  → Elapsed: {Elapsed}ms | Engine alive: {Alive}" +
                    "\n  → This may indicate an invalid position or stream issue",
                    fen, sw.ElapsedMilliseconds, IsAlive);
                return null;
            }

            Interlocked.Increment(ref _totalAnalyzed);
            var ordered = results.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();

            _logger.LogDebug(
                "[StockfishService] ✓ Analysis complete | Elapsed={Elapsed}ms | PVs={Count} | Best: {Move} (Score={Score}, Depth={Depth}) | Total analyzed: {Total}",
                sw.ElapsedMilliseconds, ordered.Count,
                ordered[0].Move, ordered[0].Score, ordered[0].Depth,
                _totalAnalyzed);

            return ordered;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _totalErrors);
            _logger.LogError(ex,
                "[StockfishService] ✗ Unexpected error during analysis" +
                "\n  → FEN: {Fen}" +
                "\n  → Elapsed: {Elapsed}ms" +
                "\n  → Engine alive: {Alive}" +
                "\n  → Total errors so far: {Errors}",
                fen, sw.ElapsedMilliseconds, IsAlive, _totalErrors);
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parsing helpers (unchanged)
    // ─────────────────────────────────────────────────────────────────────────

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
                return mate > 0 ? 30000 : -30000;
        }

        return 0;
    }

    private static string? ParseFirstMove(string line)
    {
        var pvIdx = line.IndexOf(" pv ", StringComparison.Ordinal);
        if (pvIdx < 0) return null;
        var rest = line[(pvIdx + 4)..].TrimStart();
        var end = rest.IndexOf(' ');
        return end < 0 ? rest : rest[..end];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Diagnostics
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Trả về snapshot trạng thái engine cho health check.</summary>
    public object GetDiagnostics() => new
    {
        alive        = IsAlive,
        restarting   = IsRestarting,
        pid          = _process?.Id,
        uptime       = (DateTime.UtcNow - _startedAt).ToString(@"hh\:mm\:ss"),
        restartCount = _restartCount,
        stats        = new
        {
            analyzed  = _totalAnalyzed,
            errors    = _totalErrors,
            cancelled = _totalCancelled,
        },
        config = new
        {
            multiPv   = _multiPv,
            depth     = _depth,
            threads   = _threads,
            hashMb    = _hashMb,
            timeoutMs = _timeoutMs,
        }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Dispose
    // ─────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation(
            "[StockfishService] Shutting down — Stats: Analyzed={Analyzed} | Errors={Errors} | Cancelled={Cancelled} | Restarts={Restarts}",
            _totalAnalyzed, _totalErrors, _totalCancelled, _restartCount);

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
