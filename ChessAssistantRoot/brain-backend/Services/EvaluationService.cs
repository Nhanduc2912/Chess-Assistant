using BrainBackend.Models;

namespace BrainBackend.Services;

/// <summary>
/// Phân loại nước đi dựa trên delta centipawns (so với nước trước).
/// </summary>
public class EvaluationService
{
    /// <summary>
    /// Tính classification từ delta.
    /// delta = score_hiện_tại - score_trước (tính theo góc nhìn người chơi active)
    /// </summary>
    public static string Classify(int delta)
    {
        return delta switch
        {
            < -300   => "Blunder",
            < -150   => "Mistake",
            < -50    => "Inaccuracy",
            < 0      => "Good",
            0        => "Best",
            _        => "Brilliant"
        };
    }

    /// <summary>
    /// Tính delta và classification từ hai kết quả liên tiếp.
    /// </summary>
    public (int delta, string classification) Evaluate(int previousScore, int currentScore)
    {
        var delta = currentScore - previousScore;
        return (delta, Classify(delta));
    }
}
