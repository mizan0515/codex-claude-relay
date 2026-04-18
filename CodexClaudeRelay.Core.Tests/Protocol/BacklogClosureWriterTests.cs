using System.Text.Json;
using CodexClaudeRelay.Core.Protocol;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Protocol;

public class BacklogClosureWriterTests
{
    [Fact]
    public void UpsertClosure_appends_entry_when_session_id_missing()
    {
        var existing = new[]
        {
            new BacklogEntry("session-a", null, null, null, BacklogClosureWriter.OpenStatus),
        };

        var result = BacklogClosureWriter.UpsertClosure(
            existing,
            sessionId: "session-b",
            closedBySessionId: "session-c",
            closedAt: new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero),
            convergedTurn: 4);

        Assert.Equal(2, result.Count);
        Assert.Equal("session-a", result[0].SessionId);
        Assert.Equal(BacklogClosureWriter.OpenStatus, result[0].SessionStatus);
        Assert.Equal("session-b", result[1].SessionId);
        Assert.Equal(BacklogClosureWriter.ConvergedStatus, result[1].SessionStatus);
        Assert.Equal("session-c", result[1].ClosedBySessionId);
        Assert.Equal(4, result[1].ConvergedTurn);
    }

    [Fact]
    public void UpsertClosure_updates_matching_entry_without_duplicating()
    {
        var existing = new[]
        {
            new BacklogEntry("session-a", null, null, null, BacklogClosureWriter.OpenStatus),
            new BacklogEntry("session-b", null, null, null, BacklogClosureWriter.OpenStatus),
        };

        var result = BacklogClosureWriter.UpsertClosure(
            existing,
            sessionId: "session-a",
            closedBySessionId: "session-a",
            closedAt: new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero),
            convergedTurn: 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("session-a", result[0].SessionId);
        Assert.Equal(BacklogClosureWriter.ConvergedStatus, result[0].SessionStatus);
        Assert.Equal(2, result[0].ConvergedTurn);
        Assert.Equal("session-b", result[1].SessionId);
        Assert.Equal(BacklogClosureWriter.OpenStatus, result[1].SessionStatus);
    }

    [Fact]
    public void Render_produces_valid_json_with_all_expected_fields()
    {
        var entries = new[]
        {
            new BacklogEntry(
                "sid-1",
                ClosedBySessionId: "sid-1",
                ClosedAt: new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero),
                ConvergedTurn: 5,
                SessionStatus: BacklogClosureWriter.ConvergedStatus),
        };

        var json = BacklogClosureWriter.Render(entries);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(1, arr.GetArrayLength());
        var obj = arr[0];
        Assert.Equal("sid-1", obj.GetProperty("session_id").GetString());
        Assert.Equal("converged", obj.GetProperty("session_status").GetString());
        Assert.Equal("sid-1", obj.GetProperty("closed_by_session_id").GetString());
        Assert.Equal(5, obj.GetProperty("converged_turn").GetInt32());
        Assert.Contains("2026-04-18T14:00:00", obj.GetProperty("closed_at").GetString());
    }

    [Fact]
    public async Task WriteAsync_then_LoadAsync_round_trips_entries_atomically()
    {
        var tmpDir = Path.Combine(Path.GetTempPath(), "g7-backlog-" + Guid.NewGuid().ToString("N"));
        var outPath = Path.Combine(tmpDir, "backlog.json");
        var entries = new[]
        {
            new BacklogEntry("sid-open", null, null, null, BacklogClosureWriter.OpenStatus),
            new BacklogEntry(
                "sid-closed",
                "sid-closed",
                new DateTimeOffset(2026, 4, 18, 14, 30, 0, TimeSpan.Zero),
                7,
                BacklogClosureWriter.ConvergedStatus),
        };

        try
        {
            var bytes = await BacklogClosureWriter.WriteAsync(entries, outPath, CancellationToken.None);
            Assert.True(bytes > 0);
            Assert.True(File.Exists(outPath));
            Assert.False(File.Exists(outPath + ".tmp"));

            var loaded = await BacklogClosureWriter.LoadAsync(outPath, CancellationToken.None);
            Assert.Equal(2, loaded.Count);
            Assert.Equal("sid-open", loaded[0].SessionId);
            Assert.Equal(BacklogClosureWriter.OpenStatus, loaded[0].SessionStatus);
            Assert.Null(loaded[0].ClosedBySessionId);
            Assert.Equal("sid-closed", loaded[1].SessionId);
            Assert.Equal(BacklogClosureWriter.ConvergedStatus, loaded[1].SessionStatus);
            Assert.Equal(7, loaded[1].ConvergedTurn);
        }
        finally
        {
            if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_returns_empty_list_when_file_missing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "g7-backlog-missing-" + Guid.NewGuid().ToString("N") + ".json");
        var loaded = await BacklogClosureWriter.LoadAsync(missing, CancellationToken.None);
        Assert.Empty(loaded);
    }
}
