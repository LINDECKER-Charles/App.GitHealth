using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace App.GitHealth.Api.Features.Snapshots;

internal static class SnapshotCursor
{
    public static string Encode(SnapshotCursorData cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static bool TryDecode(string encoded, out SnapshotCursorData? cursor)
    {
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(encoded);
            cursor = JsonSerializer.Deserialize<SnapshotCursorData>(bytes);
            return cursor is not null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            cursor = null;
            return false;
        }
    }
}
