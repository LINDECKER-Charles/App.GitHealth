namespace App.GitHealth.Core.Branches;

public sealed record CommitId
{
    public CommitId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!value.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                "A Git identifier must be a hexadecimal value.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
