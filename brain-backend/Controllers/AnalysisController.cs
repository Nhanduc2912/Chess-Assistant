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
        // 1. Validate FEN cơ bản (syntax check đơn giản)
        if (!IsValidFen(request.Fen))
        {
            _logger.LogWarning("[AnalysisController] Invalid FEN received: {Fen}", request.Fen);
            return BadRequest(new { error = "Invalid FEN string." });
        }

        // 2. Kiểm tra Stockfish status
        if (!_stockfish.IsAlive)
        {
            _logger.LogError("[AnalysisController] Stockfish is not running for FEN: {Fen}", request.Fen);
            return StatusCode(503, new { error = "Stockfish engine is unavailable." });
        }

        // 2b. Skip if already analyzing (prevents Stockfish pipe saturation)
        if (_isAnalyzing)
        {
            return StatusCode(429, new { status = "busy", message = "Analysis in progress, retry shortly." });
        }

        // 3. Check cache
        var fenHash = ComputeSha256(request.Fen);
        if (_cache.TryGet(fenHash, out var cached) && cached != null)
        {
            _logger.LogInformation("[AnalysisController] Cache HIT for FEN hash: {Hash}", fenHash[..8]);
            // Relay bbox và orientation mới nhất
            cached.Bbox = request.Bbox;
            cached.IsWhiteBottom = request.IsWhiteBottom;
            _latestResult = cached; // update for polling (backward compatibility)
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
                _logger.LogError("[AnalysisController] Stockfish timeout on FEN: {Fen}", request.Fen);
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

            _logger.LogInformation(
                "[AnalysisController] Analysis done. Score={Score}, Class={Class}, Moves={Count}",
                currentScore, classification, moves.Count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AnalysisController] Error analyzing FEN: {Fen}", request.Fen);
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
        return Ok(new
        {
            stockfishAlive = _stockfish.IsAlive,
            cacheEntries = _cache.Count,
            timestamp = DateTime.UtcNow
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
        _lastScore     = 0;
        _latestResult  = null;
        _isAnalyzing   = false;
        _cache.Clear();
        _logger.LogInformation("[AnalysisController] Game state reset (new game detected).");
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
