using App.GitHealth.Api.Git;
using App.GitHealth.Api.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddGitScanner(builder.Configuration);
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = GitHealthResponseWriter.WriteAsync,
});
app.MapOpenApi();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
