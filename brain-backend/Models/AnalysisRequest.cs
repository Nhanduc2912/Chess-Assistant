namespace BrainBackend.Models;

public class BoardBBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

public class AnalysisRequest
{
    /// <summary>FEN string của vị trí cờ cần phân tích.</summary>
    public string Fen { get; set; } = string.Empty;

    /// <summary>Bounding box của bàn cờ trên màn hình (pixels).</summary>
    public BoardBBox Bbox { get; set; } = new();

    /// <summary>Đánh dấu người chơi ở cạnh dưới là Trắng (true) hay Đen (false).</summary>
    public bool IsWhiteBottom { get; set; } = true;
}
