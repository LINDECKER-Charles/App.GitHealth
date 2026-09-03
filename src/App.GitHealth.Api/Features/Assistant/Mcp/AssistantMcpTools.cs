using System.Text.Json;
using System.Text.Json.Nodes;
using App.GitHealth.Core.Assistant;

namespace App.GitHealth.Api.Features.Assistant.Mcp;

/// <summary>
/// The four tools the bridge publishes, declared once as the JSON the protocol expects.
/// They only ever read a capture that was already taken: there is deliberately no tool that
/// runs Git, reaches the file system or writes anything at all.
/// </summary>
internal static class AssistantMcpTools
{
    public const string GetCapture = AssistantPrompt.CaptureTool;
    public const string ListBranches = AssistantPrompt.ListTool;
    public const string GetBranch = AssistantPrompt.BranchTool;
    public const string CountBranches = AssistantPrompt.CountTool;

    private const string Declarations = """
    [
      {
        "name": "get_capture",
        "description": "The repository, its baseline, the moment of the capture, how many branches it holds, the policy in force and how to read one. Call this first.",
        "inputSchema": {
          "type": "object", "properties": {}, "additionalProperties": false
        }
      },
      {
        "name": "list_branches",
        "description": "The measured branches, oldest activity first. Every filter is optional and an unknown value matches nothing. Page with skip and take.",
        "inputSchema": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "verdict": {
              "type": "string",
              "description": "keep, merged, review, cleanup candidate or excluded"
            },
            "topology": {
              "type": "string",
              "description": "synchronized, ahead, merged, diverged or unrelated"
            },
            "activity": {
              "type": "string",
              "description": "active, aging, inactive or unknown"
            },
            "author": {
              "type": "string",
              "description": "fragment of the tip author's display name"
            },
            "nameContains": {
              "type": "string",
              "description": "fragment of the branch reference name"
            },
            "isProtected": { "type": "boolean" },
            "isExcluded": { "type": "boolean" },
            "skip": { "type": "integer", "minimum": 0 },
            "take": { "type": "integer", "minimum": 1, "maximum": 500 }
          }
        }
      },
      {
        "name": "get_branch",
        "description": "Every measurement held for one branch, spelled as list_branches spells it.",
        "inputSchema": {
          "type": "object",
          "additionalProperties": false,
          "properties": { "branch": { "type": "string" } },
          "required": ["branch"]
        }
      },
      {
        "name": "count_branches",
        "description": "How many branches fall in each value of one field, over the whole capture rather than over a page of it.",
        "inputSchema": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "groupBy": {
              "type": "string",
              "enum": ["verdict", "topology", "activity", "author"]
            }
          },
          "required": ["groupBy"]
        }
      }
    ]
    """;

    private static readonly JsonArray Catalog = Parse();

    /// <summary>A fresh copy each time: a node belongs to one parent, and a reply owns it.</summary>
    public static JsonArray Declare() => (JsonArray)Catalog.DeepClone();

    public static bool IsKnown(string? name) => name is GetCapture
        or ListBranches
        or GetBranch
        or CountBranches;

    private static JsonArray Parse() =>
        JsonNode.Parse(Declarations) as JsonArray
        ?? throw new JsonException("The assistant tool declarations are not a JSON array.");
}
