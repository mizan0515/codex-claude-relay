using System.Text.Json;
using CodexClaudeRelay.Core.Models;

namespace CodexClaudeRelay.Core.Policy;

// Maps a RelayPendingApproval to a Korean-language explanation block aimed at
// a non-developer operator. Rule-based on purpose: zero-dependency, zero-latency,
// predictable. The English original is preserved separately so the audit trail
// never loses the raw command text.
public static class ApprovalRiskExplainer
{
    public sealed record Explanation(
        string RiskIcon,
        string RiskLabel,
        string Headline,
        string Impact,
        string ActionHint);

    public static Explanation Explain(RelayPendingApproval approval)
    {
        var (icon, label) = MapRisk(approval.RiskLevel);
        var (headline, impact) = ClassifyBody(approval);
        var hint = MapHint(approval.RiskLevel);
        return new Explanation(icon, label, headline, impact, hint);
    }

    // Build a multi-line Korean block suitable for prepending to the existing
    // approval textbox. Keeps the English original accessible below.
    public static string Render(RelayPendingApproval approval)
    {
        var e = Explain(approval);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{e.RiskIcon} 위험도: {e.RiskLabel}");
        sb.AppendLine($"무엇: {e.Headline}");
        sb.AppendLine($"영향: {e.Impact}");
        sb.AppendLine($"권고: {e.ActionHint}");
        sb.AppendLine("─────────────── 원문 (English) ───────────────");
        return sb.ToString();
    }

    private static (string icon, string label) MapRisk(string risk) => risk?.ToLowerInvariant() switch
    {
        "critical" => ("🔴", "매우 위험 (되돌릴 수 없음)"),
        "high" => ("🟠", "위험 (신중히 확인)"),
        "medium" => ("🟡", "주의 (일반 작업)"),
        "low" => ("🟢", "안전 (읽기 전용)"),
        _ => ("⚪", "알 수 없음")
    };

    private static string MapHint(string risk) => risk?.ToLowerInvariant() switch
    {
        "critical" => "원문을 반드시 확인하고, 확신이 없으면 거부하세요.",
        "high" => "대상 경로/브랜치가 맞는지 확인 후 허용하세요.",
        "medium" => "내용을 훑어보고 이상 없으면 허용해도 됩니다.",
        "low" => "읽기 작업이므로 허용해도 안전합니다.",
        _ => "판단이 어려우면 거부 후 원문을 확인하세요."
    };

    // Pattern-matches category + payload → Korean headline/impact pair.
    // Order matters: more specific patterns first.
    private static (string headline, string impact) ClassifyBody(RelayPendingApproval approval)
    {
        var category = (approval.Category ?? string.Empty).ToLowerInvariant();
        var payload = ExtractCommandText(approval.Payload);
        var text = (payload + " " + approval.Message).ToLowerInvariant();

        if (ContainsAny(text, "rm -rf", "rm --recursive", "del /s", "del /f", "rmdir /s", "remove-item -recurse"))
            return ("파일/폴더를 영구 삭제", "대상 폴더와 하위 파일을 되돌릴 수 없이 지웁니다. 휴지통에도 남지 않습니다.");

        if (ContainsAny(text, "git push --force", "git push -f", "git push --force-with-lease"))
            return ("원격 저장소에 강제 푸시", "원격 브랜치의 커밋 기록을 덮어씁니다. 다른 협업자의 변경이 사라질 수 있습니다.");

        if (ContainsAny(text, "git reset --hard"))
            return ("로컬 변경 강제 초기화", "저장되지 않은 편집과 커밋이 사라집니다. 복구가 어렵습니다.");

        if (ContainsAny(text, "format ", "mkfs", "diskpart"))
            return ("디스크 포맷/파티션 변경", "디스크 전체 데이터가 지워질 수 있습니다.");

        if (ContainsAny(text, "shutdown", "reboot ", "restart-computer"))
            return ("시스템 종료/재시작", "현재 실행 중인 모든 작업이 중단됩니다.");

        if (ContainsAny(text, "reg delete", "reg add", "set-itemproperty hklm", "remove-itemproperty hklm"))
            return ("Windows 레지스트리 수정", "시스템 설정이 영구적으로 바뀝니다. 부팅 실패로 이어질 수 있습니다.");

        if (ContainsAny(text, "schtasks", "new-scheduledtask"))
            return ("예약 작업(스케줄러) 등록/변경", "컴퓨터에 상시 동작하는 작업이 추가됩니다.");

        if (ContainsAny(text, "curl ", "invoke-webrequest", "wget ", "iwr "))
            return ("외부 네트워크로 데이터 송수신", "외부 서버와 통신합니다. 응답 내용이 로컬에 저장될 수 있습니다.");

        if (ContainsAny(text, "chmod ", "icacls", "takeown"))
            return ("파일 권한 변경", "접근 제어 설정이 바뀝니다. 보안 경계가 약해질 수 있습니다.");

        if (category.Contains("git"))
        {
            if (ContainsAny(text, "git commit", "git add"))
                return ("git 커밋 생성/스테이징", "저장소 기록에 새 커밋이 쌓입니다. 푸시 전까지는 로컬 한정입니다.");
            if (ContainsAny(text, "git push"))
                return ("원격 저장소에 푸시", "로컬 커밋이 GitHub 등 원격에 올라갑니다. 공개 저장소면 모두에게 보입니다.");
            if (ContainsAny(text, "git checkout", "git switch", "git branch"))
                return ("git 브랜치 전환/생성", "작업 문맥이 바뀝니다. 저장되지 않은 편집은 잃을 수 있습니다.");
            return ("git 명령 실행", "저장소 상태를 읽거나 바꿉니다. 원문에서 동사를 확인하세요.");
        }

        if (category.Contains("shell") || category.Contains("bash") || category.Contains("pwsh"))
            return ("셸 명령 실행", "임의의 시스템 명령이 실행됩니다. 원문을 꼭 확인하세요.");

        if (category.Contains("mcp"))
            return ("MCP 도구 호출", "외부 MCP 서버의 기능을 호출합니다. 도구별로 영향이 다릅니다.");

        if (category.Contains("write") || category.Contains("edit") || category.Contains("file"))
            return ("파일 생성/수정", "지정된 경로에 파일이 쓰여집니다. 기존 내용이 덮어쓰일 수 있습니다.");

        if (category.Contains("read"))
            return ("파일 읽기", "지정된 파일 내용을 읽습니다. 쓰기 작업은 없습니다.");

        if (category.Contains("web"))
            return ("웹 페이지 접근", "외부 웹사이트에 접속합니다. 응답이 로그로 남을 수 있습니다.");

        return (approval.Title ?? "작업 승인 요청", "원문을 확인한 뒤 허용/거부를 결정하세요.");
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Payload is JSON — best-effort extract a command-ish field if present.
    private static string ExtractCommandText(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            foreach (var name in new[] { "command", "cmd", "script", "args", "url", "path", "target" })
            {
                if (root.TryGetProperty(name, out var v))
                {
                    if (v.ValueKind == JsonValueKind.String) return v.GetString() ?? string.Empty;
                    if (v.ValueKind == JsonValueKind.Array)
                        return string.Join(" ", v.EnumerateArray().Select(e => e.ToString()));
                }
            }
        }
        catch (JsonException) { /* fall through */ }
        return payload;
    }
}
