using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Projects;

internal static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects").WithTags("Projects");
        group.MapPost("/validate", ValidateAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{projectId:guid}", GetAsync);
        group.MapPut("/{projectId:guid}/repository", RelocateAsync);
        group.MapPut("/{projectId:guid}/settings", UpdateAsync);
        group.MapPut("/{projectId:guid}/organization", OrganizeAsync);
        group.MapDelete("/{projectId:guid}", DeleteAsync);
        return endpoints;
    }

    private static async Task<IResult> DeleteAsync(Guid projectId, HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ProjectDeletionService>();
        var result = await service.DeleteAsync(projectId, context.RequestAborted);
        return result.IsSuccess ? Results.NoContent() : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> ValidateAsync(
        ValidateRepositoryRequest request,
        RepositoryValidator validator,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request.Path, cancellationToken);
        return result.IsSuccess
            ? Results.Ok(ProjectResponseMapper.Map(result.Value!))
            : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> CreateAsync(
        CreateProjectRequest request,
        ProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? Results.Created($"/api/projects/{result.Value!.Id}", result.Value)
            : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> ListAsync(
        ProjectService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid projectId,
        ProjectService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(projectId, cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> UpdateAsync(
        Guid projectId,
        ProjectSettingsRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ProjectService>();
        var result = await service.UpdateAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> OrganizeAsync(
        Guid projectId,
        ProjectOrganizationRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ProjectOrganizationService>();
        var result = await service.UpdateAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> RelocateAsync(
        Guid projectId,
        RelocateProjectRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<ProjectService>();
        var result = await service.RelocateAsync(projectId, request, context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
