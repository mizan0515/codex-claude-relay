using System.Text;
using System.Text.Json;

namespace CodexClaudeRelay.Core.Protocol;

public sealed record BacklogEntry(
    string SessionId,
    string? ClosedBySessionId,
    DateTimeOffset? ClosedAt,
    int? ConvergedTurn,
    string SessionStatus);

public static class BacklogClosureWriter
{
    public const string ConvergedStatus = "converged";
    public const string OpenStatus = "open";

    public static IReadOnlyList<BacklogEntry> UpsertClosure(
        IReadOnlyList<BacklogEntry> existing,
        string sessionId,
        string closedBySessionId,
        DateTimeOffset closedAt,
        int convergedTurn)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(closedBySessionId);

        var next = new List<BacklogEntry>(existing.Count + 1);
        var replaced = false;
        foreach (var entry in existing)
        {
            if (!replaced && string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal))
            {
                next.Add(entry with
                {
                    ClosedBySessionId = closedBySessionId,
                    ClosedAt = closedAt,
                    ConvergedTurn = convergedTurn,
                    SessionStatus = ConvergedStatus,
                });
                replaced = true;
            }
            else
            {
                next.Add(entry);
            }
        }

        if (!replaced)
        {
            next.Add(new BacklogEntry(sessionId, closedBySessionId, closedAt, convergedTurn, ConvergedStatus));
        }

        return next;
    }

    public static string Render(IReadOnlyList<BacklogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        sb.Append('[').Append('\n');
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.Append("  {\n");
            sb.Append("    \"session_id\": ").Append(JsonString(e.SessionId)).Append(",\n");
            sb.Append("    \"session_status\": ").Append(JsonString(e.SessionStatus)).Append(",\n");
            sb.Append("    \"closed_by_session_id\": ").Append(JsonStringOrNull(e.ClosedBySessionId)).Append(",\n");
            sb.Append("    \"closed_at\": ").Append(e.ClosedAt.HasValue ? JsonString(e.ClosedAt.Value.ToString("O")) : "null").Append(",\n");
            sb.Append("    \"converged_turn\": ").Append(e.ConvergedTurn.HasValue ? e.ConvergedTurn.Value.ToString() : "null").Append('\n');
            sb.Append(i == entries.Count - 1 ? "  }\n" : "  },\n");
        }
        sb.Append(']').Append('\n');
        return sb.ToString();
    }

    public static async Task<long> WriteAsync(
        IReadOnlyList<BacklogEntry> entries,
        string outPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outPath);
        var body = Render(entries);

        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tmp = outPath + ".tmp";
        await File.WriteAllTextAsync(tmp, body, ct).ConfigureAwait(false);
        File.Move(tmp, outPath, overwrite: true);

        return new FileInfo(outPath).Length;
    }

    public static async Task<IReadOnlyList<BacklogEntry>> LoadAsync(
        string inPath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inPath);
        if (!File.Exists(inPath))
            return Array.Empty<BacklogEntry>();

        await using var stream = File.OpenRead(inPath);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<BacklogEntry>();

        var list = new List<BacklogEntry>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            list.Add(new BacklogEntry(
                SessionId: GetString(el, "session_id") ?? string.Empty,
                ClosedBySessionId: GetString(el, "closed_by_session_id"),
                ClosedAt: GetDateTime(el, "closed_at"),
                ConvergedTurn: GetInt(el, "converged_turn"),
                SessionStatus: GetString(el, "session_status") ?? OpenStatus));
        }
        return list;
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? GetInt(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static DateTimeOffset? GetDateTime(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(v.GetString(), out var dt) ? dt : null;

    private static string JsonString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string JsonStringOrNull(string? value) => value is null ? "null" : JsonString(value);
}
