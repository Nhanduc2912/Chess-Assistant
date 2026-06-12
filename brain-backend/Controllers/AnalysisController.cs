using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BrainBackend.Hubs;
using BrainBackend.Models;
using BrainBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BrainBackend.Controllers;

[ApiController]
[Route("api/analyze")]
public class AnalysisController : ControllerBase
{
    private readonly StockfishService _stockfish;
    private readonly FenCacheService _cache;
    private readonly EvaluationService _evaluator;
    private readonly IHubContext<ChessHub> _hubContext;
    private readonly ILogger<AnalysisController> _logger;

    // Lưu score trước để tính delta
    private static int _lastScore = 0;

    // Latest result for REST polling (Chrome Extension)
    private static AnalysisResult? _latestResult = null;

    // Flag to prevent concurrent analysis requests
    private static bool _isAnalyzing = false;

    // Request stats
    private static int _totalRequests  = 0;
    private static int _cacheHits      = 0;
    private static int _analysisOk     = 0;
    private static int _analysisFailed = 0;
    private static int _busy429        = 0;
    private static DateTime _serviceStart = DateTime.UtcNow;

    public AnalysisController(
        StockfishService stockfish,
        FenCacheService cache,
        EvaluationService evaluator,
        IHubContext<ChessHub> hubContext,
        ILogger<AnalysisController> logger)
    {
        _stockfish = stockfish;
        _cache = cache;
        _evaluator = evaluator;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/analysis
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Analyze([FromBody] AnalysisRequest request)
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalRequests);

        _logger.LogDebug(
            "[AnalysisController] → Request #{Req} | FEN: {Fen}",
            _totalRequests, request.Fen);

        // 1. Validate FEN cơ bản (syntax check đơn giản)
        if (!IsValidFen(request.Fen))
        {
            _logger.LogWarning(
                "[AnalysisController] ✗ Invalid FEN received" +
                "\n  → FEN: {Fen}" +
                "\n  → Rejected at validation",
                request.Fen);
            return BadRequest(new { error = "Invalid FEN string." });
        }

        // 2. Kiểm tra Stockfish status
        if (_stockfish.IsRestarting)
        {
            // Engine đang restart — không phải lỗi cố định, client nên retry
            _logger.LogDebug(
                "[AnalysisController] ⏳ Engine RESTARTING — 503 (transient)" +
                "\n  → FEN: {Fen}" +
                "\n  → Client should retry in ~1-2s",
                request.Fen);
            return StatusCode(503, new { error = "Engine is restarting, retry shortly.", restarting = true });
        }

        if (!_stockfish.IsAlive)
        {
            Interlocked.Increment(ref _analysisFailed);
            _logger.LogError(
                "[AnalysisController] ✗ Engine DEAD (not restarting)" +
                "\n  → FEN: {Fen}" +
                "\n  → Engine process is not running and is not auto-restarting",
                request.Fen);
            return StatusCode(503, new { error = "Stockfish engine is unavailable.", restarting = false });
        }

        // 2b. Skip if already analyzing (prevents Stockfish pipe saturation)
        if (_isAnalyzing)
        {
            Interlocked.Increment(ref _busy429);
            _logger.LogDebug(
                "[AnalysisController] ⚡ 429 Busy — engine occupied, rejecting request" +
                "\n  → FEN: {Fen} | Total busy rejections: {Busy}",
                request.Fen, _busy429);
            return StatusCode(429, new { status = "busy", message = "Analysis in progress, retry shortly." });
        }

        // 3. Check cache
        var fenHash = ComputeSha256(request.Fen);
        if (_cache.TryGet(fenHash, out var cached) && cached != null)
        {
            Interlocked.Increment(ref _cacheHits);
            _logger.LogInformation(
                "[AnalysisController] ⚡ Cache HIT | Hash={Hash} | Class={Class} | Score={Score} | Elapsed={Elapsed}ms | HitRate={Rate:P0}",
                fenHash[..8], cached.Classification, cached.Evaluation,
                sw.ElapsedMilliseconds,
                _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0);
            // Relay bbox và orientation mới nhất
            cached.Bbox = request.Bbox;
            cached.IsWhiteBottom = request.IsWhiteBottom;
            _latestResult = cached;
            await ChessHub.BroadcastAnalysis(_hubContext, cached);
            return Ok(cached);
        }

        _isAnalyzing = true;

        // 4. Phân tích bằng Stockfish
        try
        {
            var moves = await _stockfish.AnalyzeAsync(request.Fen);

            if (moves == null || moves.Count == 0)
            {
                Interlocked.Increment(ref _analysisFailed);
                _logger.LogError(
                    "[AnalysisController] ✗ Analysis FAILED — no results returned" +
                    "\n  → FEN: {Fen}" +
                    "\n  → Elapsed: {Elapsed}ms" +
                    "\n  → Engine alive: {Alive}" +
                    "\n  → Possible causes: timeout, stream error, engine crash" +
                    "\n  → Failed requests total: {Failed}",
                    request.Fen, sw.ElapsedMilliseconds, _stockfish.IsAlive, _analysisFailed);
                return StatusCode(500, new { error = "Stockfish analysis failed or timed out." });
            }

            var currentScore = moves[0].Score;

            // Stockfish always reports score from WHITE's perspective.
            // Normalize to the active player's perspective:
            var activeColor = request.Fen.Split(' ').Length > 1 ? request.Fen.Split(' ')[1] : "w";
            var activeScore = activeColor == "b" ? -currentScore : currentScore;

            // User perspective for classification
            var isUserTurn = (request.IsWhiteBottom && activeColor == "w") || (!request.IsWhiteBottom && activeColor == "b");
            var userScore = isUserTurn ? activeScore : -activeScore;

            var (delta, classification) = _evaluator.Evaluate(_lastScore, userScore);
            _lastScore = userScore;

            var result = new AnalysisResult
            {
                BestMoves = moves,
                Evaluation = activeScore,
                Classification = classification,
                Delta = delta,
                Bbox = request.Bbox,
                IsWhiteBottom = request.IsWhiteBottom,
                Fen = request.Fen
            };

            // 5. Lưu cache
            _cache.Set(fenHash, result);

            // 6. Store latest for REST polling (backward compatibility)
            _latestResult = result;

            // 7. Broadcast qua SignalR
            await ChessHub.BroadcastAnalysis(_hubContext, result);

            Interlocked.Increment(ref _analysisOk);
            _logger.LogInformation(
                "[AnalysisController] ✓ Analysis OK | Score={Score} | Class={Class} | BestMove={Move} | PVs={Count} | Elapsed={Elapsed}ms | Total={Total} | CacheHit%={Rate:P0}",
                currentScore, classification, moves[0].Move, moves.Count,
                sw.ElapsedMilliseconds, _analysisOk,
                _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0);

            return Ok(result);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _analysisFailed);
            _logger.LogError(ex,
                "[AnalysisController] ✗ Unhandled exception during analysis" +
                "\n  → FEN: {Fen}" +
                "\n  → Elapsed: {Elapsed}ms" +
                "\n  → Engine alive: {Alive}",
                request.Fen, sw.ElapsedMilliseconds, _stockfish.IsAlive);
            return StatusCode(500, new { error = "Internal server error during analysis." });
        }
        finally
        {
            _isAnalyzing = false;
        }
    }

    /// <summary>GET /api/analyze/status — Health check endpoint</summary>
    [HttpGet("status")]
    public IActionResult Status()
    {
        var uptime = DateTime.UtcNow - _serviceStart;
        return Ok(new
        {
            stockfishAlive = _stockfish.IsAlive,
            cacheEntries   = _cache.Count,
            timestamp      = DateTime.UtcNow,
            uptime         = uptime.ToString(@"hh\:mm\:ss"),
            requests = new
            {
                total    = _totalRequests,
                ok       = _analysisOk,
                failed   = _analysisFailed,
                cacheHit = _cacheHits,
                busy429  = _busy429,
                cacheHitRate = _totalRequests > 0
                    ? Math.Round((double)_cacheHits / _totalRequests * 100, 1)
                    : 0.0
            }
        });
    }

    /// <summary>GET /api/analyze/diagnostics — Deep engine diagnostics</summary>
    [HttpGet("diagnostics")]
    public IActionResult Diagnostics()
    {
        var uptime = DateTime.UtcNow - _serviceStart;
        _logger.LogDebug("[AnalysisController] Diagnostics requested");
        return Ok(new
        {
            service = new
            {
                uptime         = uptime.ToString(@"hh\:mm\:ss"),
                uptimeSeconds  = (int)uptime.TotalSeconds,
                startedAt      = _serviceStart,
                lastAnalysis   = _latestResult?.Classification,
            },
            requests = new
            {
                total          = _totalRequests,
                analysisOk     = _analysisOk,
                analysisFailed = _analysisFailed,
                cacheHits      = _cacheHits,
                busy429        = _busy429,
                cacheHitPct    = _totalRequests > 0
                    ? Math.Round((double)_cacheHits / _totalRequests * 100, 1)
                    : 0.0,
                successPct     = _totalRequests > 0
                    ? Math.Round((double)_analysisOk / _totalRequests * 100, 1)
                    : 0.0
            },
            engine     = _stockfish.GetDiagnostics(),
            cache      = new { entries = _cache.Count },
            timestamp  = DateTime.UtcNow
        });
    }

    /// <summary>GET /api/analyze/latest — Returns the most recent analysis (for Chrome Extension polling)</summary>
    [HttpGet("latest")]
    public IActionResult Latest()
    {
        if (_latestResult == null)
            return NoContent(); // 204 — no analysis yet
        return Ok(_latestResult);
    }

    /// <summary>POST /api/analyze/reset — Called when a new game starts; clears per-game state</summary>
    [HttpPost("reset")]
    public IActionResult Reset()
    {
        var prevStats = new { _analysisOk, _analysisFailed, _cacheHits, _totalRequests };
        _lastScore     = 0;
        _latestResult  = null;
        _isAnalyzing   = false;
        _cache.Clear();
        _logger.LogInformation(
            "[AnalysisController] ↺ Game state RESET" +
            "\n  → Previous game stats: Requests={Req} | OK={Ok} | Failed={Fail} | CacheHit={Cache}",
            prevStats._totalRequests, prevStats._analysisOk,
            prevStats._analysisFailed, prevStats._cacheHits);
        return Ok(new { status = "reset" });
    }

    private static bool IsValidFen(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen)) return false;

        var parts = fen.Trim().Split(' ');
        if (parts.Length < 1) return false;

        // Kiểm tra part đầu (board) có đủ 8 ranks không
        var ranks = parts[0].Split('/');
        if (ranks.Length != 8) return false;

        bool hasWhiteKing = false;
        bool hasBlackKing = false;

        // Mỗi rank phải có tổng 8 ô
        foreach (var rank in ranks)
        {
            var count = 0;
            foreach (var c in rank)
            {
                if (char.IsDigit(c)) count += c - '0';
                else if ("PNBRQKpnbrqk".Contains(c))
                {
                    count++;
                    if (c == 'K') hasWhiteKing = true;
                    if (c == 'k') hasBlackKing = true;
                }
                else return false;
            }
            if (count != 8) return false;
        }

        return hasWhiteKing && hasBlackKing;
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
