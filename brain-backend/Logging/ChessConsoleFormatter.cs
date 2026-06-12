using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace BrainBackend.Logging;

/// <summary>
/// Custom console formatter cung cấp output đẹp, có màu sắc, structured, dễ đọc.
/// Format: [HH:mm:ss.fff] [LEVEL] [Source] Message | Context
/// </summary>
public sealed class ChessConsoleFormatter : ConsoleFormatter
{
    public const string FormatterName = "chess";

    public ChessConsoleFormatter(IOptionsMonitor<ConsoleFormatterOptions> options)
        : base(FormatterName) { }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
        if (string.IsNullOrEmpty(message) && logEntry.Exception == null) return;

        var now = DateTime.Now;
        var level = logEntry.LogLevel;
        var category = GetShortCategory(logEntry.Category);

        // ── Timestamp
        textWriter.Write("\x1b[90m"); // dark gray
        textWriter.Write($"[{now:HH:mm:ss.fff}]");
        textWriter.Write("\x1b[0m ");

        // ── Level badge
        WriteLevel(textWriter, level);
        textWriter.Write(" ");

        // ── Category
        textWriter.Write("\x1b[36m"); // cyan
        textWriter.Write($"[{category}]");
        textWriter.Write("\x1b[0m ");

        // ── Message (với highlight theo level)
        WriteMessage(textWriter, level, message ?? "");

        // ── Exception detail (nếu có)
        if (logEntry.Exception != null)
        {
            textWriter.WriteLine();
            WriteException(textWriter, logEntry.Exception);
        }

        textWriter.WriteLine();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void WriteLevel(TextWriter w, LogLevel level)
    {
        var (badge, color) = level switch
        {
            LogLevel.Trace       => ("  TRACE ", "\x1b[90m"),         // dark gray
            LogLevel.Debug       => ("  DEBUG ", "\x1b[34m"),         // blue
            LogLevel.Information => ("   INFO ", "\x1b[32m"),         // green
            LogLevel.Warning     => ("   WARN ", "\x1b[33m"),         // yellow
            LogLevel.Error       => ("  ERROR ", "\x1b[31m"),         // red
            LogLevel.Critical    => ("   CRIT ", "\x1b[35;1m"),       // bold magenta
            _                    => ("    ??? ", "\x1b[37m"),
        };

        w.Write(color);
        w.Write(badge);
        w.Write("\x1b[0m");
    }

    private static void WriteMessage(TextWriter w, LogLevel level, string message)
    {
        // Parse "[Source] Message" pattern → highlight source in brackets
        var color = level switch
        {
            LogLevel.Trace       => "\x1b[90m",
            LogLevel.Debug       => "\x1b[37m",
            LogLevel.Information => "\x1b[97m",   // bright white
            LogLevel.Warning     => "\x1b[93m",   // bright yellow
            LogLevel.Error       => "\x1b[91m",   // bright red
            LogLevel.Critical    => "\x1b[95;1m", // bold bright magenta
            _                    => "\x1b[37m",
        };

        // Highlight inline keywords
        message = HighlightKeywords(message, level);

        w.Write(color);
        w.Write(message);
        w.Write("\x1b[0m");
    }

    private static void WriteException(TextWriter w, Exception ex)
    {
        // Separator line
        w.Write("\x1b[31m");
        w.Write("    ┌─ Exception ────────────────────────────────────────────────────────────");
        w.WriteLine("\x1b[0m");

        // Root exception
        WriteExceptionBlock(w, ex, 0);

        w.Write("\x1b[31m");
        w.Write("    └───────────────────────────────────────────────────────────────────────");
        w.Write("\x1b[0m");
    }

    private static void WriteExceptionBlock(TextWriter w, Exception ex, int depth)
    {
        var indent = new string(' ', 4 + depth * 2);
        var bar    = depth == 0 ? "│ " : "╰ ";

        // Exception type + message
        w.Write("\x1b[31m");
        w.Write($"{indent}{bar}");
        w.Write("\x1b[91;1m");
        w.Write(ex.GetType().Name);
        w.Write("\x1b[91m");
        w.Write(": ");
        w.Write("\x1b[93m");
        w.WriteLine(ex.Message);
        w.Write("\x1b[0m");

        // Stack trace — chỉ hiện frames liên quan đến code của mình
        if (ex.StackTrace != null)
        {
            var frames = ex.StackTrace
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim());

            foreach (var frame in frames)
            {
                var isOwnCode = frame.Contains("BrainBackend", StringComparison.OrdinalIgnoreCase);
                if (isOwnCode)
                {
                    w.Write("\x1b[90m");
                    w.Write($"{indent}│   ");
                    w.Write("\x1b[33m"); // yellow for own code
                    w.WriteLine(frame);
                }
                else
                {
                    w.Write("\x1b[90m");
                    w.Write($"{indent}│   ");
                    w.WriteLine(frame);
                }
                w.Write("\x1b[0m");
            }
        }

        // Inner exceptions
        if (ex is AggregateException aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions)
                WriteExceptionBlock(w, inner, depth + 1);
        }
        else if (ex.InnerException != null)
        {
            WriteExceptionBlock(w, ex.InnerException, depth + 1);
        }
    }

    private static string HighlightKeywords(string msg, LogLevel level)
    {
        // Tô màu FEN strings (rất dài, nhận dạng bằng dấu /)
        // Tô màu scores, classifications
        // Chúng ta giữ đơn giản, chỉ highlight một số key terms
        return msg; // message đã được tô qua level color
    }

    private static string GetShortCategory(string category)
    {
        // "BrainBackend.Controllers.AnalysisController" → "AnalysisController"
        var idx = category.LastIndexOf('.');
        return idx >= 0 ? category[(idx + 1)..] : category;
    }
}
