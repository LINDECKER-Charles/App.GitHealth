using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace App.GitHealth.Api.Features.LocalApi;

/// <summary>
/// The secret an orchestrator authenticates with. It is handed over once, when it is issued,
/// and only its fingerprint is kept: a settings file read by anything else yields nothing
/// usable. Losing it is not a dead end — issuing a new one is one call, and it revokes the
/// previous one in the same move.
/// </summary>
internal static class LocalApiToken
{
    /// <summary>Characters kept in clear, enough to recognise which token is in force.</summary>
    public const int PrefixLength = 6;

    private const int EntropyByteCount = 32;

    public static LocalApiTokenIssue Issue(DateTimeOffset issuedAtUtc)
    {
        var token = Base64Url.EncodeToString(
            RandomNumberGenerator.GetBytes(EntropyByteCount));
        return new LocalApiTokenIssue(
            token,
            Fingerprint(token),
            token[..PrefixLength],
            issuedAtUtc);
    }

    /// <summary>
    /// Compares in constant time. A presented token that is not the right one must take the
    /// same time to reject whatever it got right, so a caller learns nothing by trying.
    /// </summary>
    public static bool Matches(string? presented, string? fingerprint)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(fingerprint))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        if (!Convert.TryFromBase64String(fingerprint, expected, out var written)
            || written != SHA256.HashSizeInBytes)
        {
            return false;
        }

        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(presented), actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Fingerprint(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>
/// A freshly issued token: the secret itself, which leaves in the answer and is never seen
/// again, and what is kept of it on disk.
/// </summary>
internal sealed record LocalApiTokenIssue(
    string Token,
    string Fingerprint,
    string Prefix,
    DateTimeOffset IssuedAtUtc);
