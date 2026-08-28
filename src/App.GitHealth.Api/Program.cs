var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapOpenApi();

app.Run();

public partial class Program;
