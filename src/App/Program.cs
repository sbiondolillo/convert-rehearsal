using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

namespace App;

/// <summary>The console program: it builds the Generic Host and parses the arguments with the command tree.</summary>
public static class Program
{
    /// <summary>Runs the program on the arguments and returns its exit code.</summary>
    /// <param name="args">The arguments of the command line.</param>
    /// <param name="configureServices">Changes the services after the host's own setup, for a test.</param>
    /// <param name="configuration">Where the command writes, or the console when null.</param>
    public static async Task<int> RunAsync(
        string[] args,
        Action<IServiceCollection>? configureServices = null,
        InvocationConfiguration? configuration = null)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        // The request address holds the key of the service, so no logger may print it.
        builder.Logging.ClearProviders();
        builder.Services.AddHttpClient();
        configureServices?.Invoke(builder.Services);

        using IHost host = builder.Build();
        HttpClient http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
        ParseResult parse = ConvertCommand.Create(http).Parse(args);
        return await parse.InvokeAsync(configuration ?? new InvocationConfiguration()).ConfigureAwait(false);
    }

    private static Task<int> Main(string[] args) => RunAsync(args);
}
