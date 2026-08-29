using App.GitHealth.Core.Projects;

namespace App.GitHealth.Api.Persistence.Models;

internal sealed record ProjectOrganizationUpdate(
    Guid ProjectId,
    ProjectOrganization Organization,
    DateTimeOffset ChangedAtUtc);
