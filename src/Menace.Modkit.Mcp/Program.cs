using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Menace.Modkit.App.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (stdout is reserved for MCP protocol)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Register services
builder.Services.AddSingleton<ModpackManager>();
builder.Services.AddSingleton<CompilationService>();
builder.Services.AddSingleton(sp => new DeployManager(sp.GetRequiredService<ModpackManager>()));
builder.Services.AddSingleton<SecurityScanner>();

// Register MCP server with stdio transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "menace-modkit",
            Version = Menace.ModkitVersion.MelonVersion
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
