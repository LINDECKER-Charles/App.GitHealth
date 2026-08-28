using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace App.GitHealth.Api.Tests.Persistence;

internal sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;

    public string ApplicationName { get; set; } = typeof(Program).Assembly.FullName!;

    public string ContentRootPath { get; set; } = contentRootPath;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
