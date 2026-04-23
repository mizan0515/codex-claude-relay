using CodexClaudeRelay.Core.Models;
using CodexClaudeRelay.Core.Policy;
using Xunit;

namespace CodexClaudeRelay.Core.Tests.Policy;

public class RelayApprovalPolicyTests
{
    [Fact]
    public void TryResolveDefaultToolReviewDecision_allows_generic_mcp_resource_reads()
    {
        const string payload = """
{
  "type": "item.started",
  "item": {
    "id": "item_20",
    "type": "mcp_tool_call",
    "server": "invalid",
    "tool": "read_mcp_resource",
    "arguments": {
      "server": "invalid",
      "uri": "invalid"
    },
    "status": "in_progress"
  }
}
""";

        var resolved = RelayApprovalPolicy.TryResolveDefaultToolReviewDecision(
            "mcp",
            "MCP Tool: mcp_tool_call",
            payload,
            out var decision,
            out var reason);

        Assert.True(resolved);
        Assert.Equal(RelayApprovalDecision.ApproveOnce, decision);
        Assert.Contains("read_mcp_resource", reason);
    }

    [Fact]
    public void TryResolveDefaultToolReviewDecision_allows_safe_unity_verification_tools()
    {
        const string payload = """
{
  "type": "item.started",
  "item": {
    "id": "item_21",
    "type": "mcp_tool_call",
    "server": "unityMCP",
    "tool": "read_console",
    "arguments": {
      "include_stacktrace": false
    },
    "status": "in_progress"
  }
}
""";

        var resolved = RelayApprovalPolicy.TryResolveDefaultToolReviewDecision(
            "mcp",
            "MCP Tool: mcp_tool_call",
            payload,
            out var decision,
            out var reason);

        Assert.True(resolved);
        Assert.Equal(RelayApprovalDecision.ApproveOnce, decision);
        Assert.Contains("Unity verification tool", reason);
        Assert.Contains("read_console", reason);
    }
}
