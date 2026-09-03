using System.Text;

namespace App.GitHealth.Core.Assistant;

/// <summary>
/// Spells an enum name the way the two readers of a capture expect it. Both a person, in the
/// panel, and an agent, in a tool answer, receive these values; lowercasing a compound name
/// runs its words together — <c>BranchIsAncestorOfReference</c> becomes a wall of letters —
/// so the words are separated instead.
/// </summary>
public static class BriefingLabel
{
    /// <summary>Headroom for the spaces a split name gains, so the builder rarely grows.</summary>
    private const int SeparatorHeadroom = 8;

    public static string Words(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length + SeparatorHeadroom);
        foreach (var character in value)
        {
            if (char.IsUpper(character) && builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}
