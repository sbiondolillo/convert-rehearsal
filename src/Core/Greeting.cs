namespace Core;

/// <summary>The greeting template and the formatting of a name into it.</summary>
public static class Greeting
{
    private const string Template = "Hello, {name}!";

    public static string Format(string name) => Template.Replace("{name}", name);
}
