namespace BrainBackend.Models;

public class MoveInfo
{
    public string Move { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Depth { get; set; }
}

public class AnalysisResult
{
    /// <summary>Top N best moves từ Stockfish (MultiPV).</summary>
    public List<MoveInfo> BestMoves { get; set; } = [];

    /// <summary>Evaluation hiện tại tính bằng centipawns (góc nhìn người chơi active).</summary>
    public int Evaluation { get; set; }

    /// <summary>Phân loại nước đi vừa đi: Blunder/Mistake/Inaccuracy/Good/Best/Brilliant.</summary>
    public string Classification { get; set; } = "Unknown";

    /// <summary>Delta centipawns so với nước trước (dùng để tính classification).</summary>
    public int Delta { get; set; }

    /// <summary>Bounding box của bàn cờ, relay lại cho UI.</summary>
    public BoardBBox Bbox { get; set; } = new();

    /// <summary>Đánh dấu người chơi ở cạnh dưới là Trắng (true) hay Đen (false).</summary>
    public bool IsWhiteBottom { get; set; } = true;

    /// <summary>FEN của position vừa phân tích.</summary>
    public string Fen { get; set; } = string.Empty;
}
