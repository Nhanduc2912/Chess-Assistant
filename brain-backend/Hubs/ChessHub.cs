using BrainBackend.Models;
using Microsoft.AspNetCore.SignalR;

namespace BrainBackend.Hubs;

/// <summary>
/// SignalR Hub — emit "ReceiveAnalysis" tới tất cả connected clients.
/// URL: /chessHub
/// </summary>
public class ChessHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Broadcast kết quả phân tích tới tất cả clients.
    /// </summary>
    public static async Task BroadcastAnalysis(
        IHubContext<ChessHub> hubContext,
        AnalysisResult result)
    {
        await hubContext.Clients.All.SendAsync("ReceiveAnalysis", result);
    }
}
