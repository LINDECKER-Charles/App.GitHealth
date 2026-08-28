using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Persistence.Models;

internal sealed record ProjectSettingsUpdate(
    Guid ProjectId,
    ProjectSettings Settings,
    DateTimeOffset ChangedAtUtc);
