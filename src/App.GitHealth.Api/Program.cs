using App.GitHealth.Api.Features;
using App.GitHealth.Api.Features.Common;
using App.GitHealth.Api.Git;
using App.GitHealth.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddGitScanner(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddGitHealthApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = GitHealthResponseWriter.WriteAsync,
});
app.MapOpenApi();
app.MapGitHealthApi();
app.MapFallback("/api/{**path}", () => ApiProblems.Result(ApiProblems.NotFound(
    ApiErrorCodes.EndpointNotFound,
    "La route API demandée n’existe pas.")));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
