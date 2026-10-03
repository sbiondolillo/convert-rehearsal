using System.CommandLine;
using Core;

namespace Specs.Unit;

/// <summary>Runs the command against a handler and an environment and captures what it writes.</summary>
public sealed record RunResult(int ExitCode, string Output, string Error);

public static class Runner
{
    public static async Task<RunResult> RunAsync(StubHandler handler, string? key, params string[] args)
    {
        using var http = new HttpClient(handler);
        using var output = new StringWriter();
        using var error = new StringWriter();
        RootCommand root = ConvertCommand.Create(http, name => name == ConvertCommand.KeyVariable ? key : null);
        int code = await root.Parse(args).InvokeAsync(new InvocationConfiguration { Output = output, Error = error });
        return new RunResult(code, output.ToString(), error.ToString());
    }
}
