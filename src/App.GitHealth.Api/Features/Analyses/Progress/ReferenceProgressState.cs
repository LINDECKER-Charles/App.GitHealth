namespace App.GitHealth.Api.Features.Analyses;

/// <summary>How far a running analysis has got with one reference.</summary>
internal enum ReferenceProgressState
{
    /// <summary>Listed by the repository read, nothing compared yet.</summary>
    Listed,

    /// <summary>Being placed against the baseline.</summary>
    Measuring,

    /// <summary>Distance to the baseline known.</summary>
    Measured,

    /// <summary>Being asked who wrote the commits it adds.</summary>
    Enriching,

    /// <summary>Nothing left to read about it.</summary>
    Read,
}
