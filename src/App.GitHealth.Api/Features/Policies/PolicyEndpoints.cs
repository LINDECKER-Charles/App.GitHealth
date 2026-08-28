using App.GitHealth.Api.Features.Common;

namespace App.GitHealth.Api.Features.Policies;

internal static class PolicyEndpoints
{
    public static IEndpointRouteBuilder MapPolicyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/projects/{projectId:guid}/policy")
            .WithTags("Policies");
        group.MapPut("/", UpdateAsync);
        group.MapPost("/preview", PreviewAsync);
        return endpoints;
    }

    private static async Task<IResult> UpdateAsync(
        Guid projectId,
        PolicyUpdateRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<PolicyService>();
        var result = await service.UpdateAsync(
            projectId,
            request,
            context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }

    private static async Task<IResult> PreviewAsync(
        Guid projectId,
        PolicyUpdateRequest request,
        HttpContext context)
    {
        var service = context.RequestServices.GetRequiredService<PolicyService>();
        var result = await service.PreviewAsync(
            projectId,
            request,
            context.RequestAborted);
        return result.IsSuccess ? Results.Ok(result.Value) : ApiProblems.Result(result.Failure!);
    }
}
