using System.CommandLine;

namespace Core;

/// <summary>The command tree of the console program: one optional argument, <c>name</c>, and the flag <c>--shout</c>.</summary>
public static class GreetCommand
{
    /// <summary>The root command, which prints the greeting of its argument <c>name</c>.</summary>
    public static RootCommand Create()
    {
        var name = new Argument<string>("name")
        {
            Description = "Who to greet.",
            Arity = ArgumentArity.ZeroOrOne,
            DefaultValueFactory = _ => "world",
        };
        var shout = new Option<bool>("--shout")
        {
            Description = "Print the greeting in upper case.",
        };
        var root = new RootCommand("Prints a greeting.");
        root.Arguments.Add(name);
        root.Options.Add(shout);
        root.SetAction(result =>
        {
            string greeting = Greeter.Greet(result.GetRequiredValue(name));
            result.InvocationConfiguration.Output.WriteLine(result.GetValue(shout) ? greeting.ToUpperInvariant() : greeting);
            return 0;
        });
        return root;
    }
}
