using System.CommandLine;
using System.Net;
using Core;
using Reqnroll;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class CurrencyConversionSteps : IDisposable
{
    private readonly StubHandler _handler = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private readonly string? _savedKey = Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable);
    private int _exitCode;

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, _savedKey);
        _handler.Dispose();
        _output.Dispose();
        _error.Dispose();
    }

    [Given("the environment variable EXCHANGERATE_API_KEY holds {string}")]
    public static void GivenTheKeyHolds(string key) => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, key);

    [Given("the environment variable EXCHANGERATE_API_KEY is unset")]
    public static void GivenTheKeyIsUnset() => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, null);

    [Given("the environment variable EXCHANGERATE_API_KEY is empty")]
    public static void GivenTheKeyIsEmpty() => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, "");

    [Given("the service answers the rate {word}")]
    public void GivenTheServiceAnswersTheRate(string rate) =>
        _handler.Body = $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}""";

    [Given("the service answers with the error-type {string}")]
    public void GivenTheServiceAnswersWithTheErrorType(string errorType)
    {
        _handler.Status = errorType is "unsupported-code" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
        _handler.Body = $$"""{"result":"error","error-type":"{{errorType}}"}""";
    }

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() => _handler.Body = null;

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgramWith(string arguments)
    {
        using var http = new HttpClient(_handler, disposeHandler: false);
        RootCommand root = ConvertCommand.Create(http);
        ParseResult parse = root.Parse(arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        _exitCode = await parse.InvokeAsync(new InvocationConfiguration { Output = _output, Error = _error }, TestContext.Current.CancellationToken);
    }

    [Then("the program sent the request {string}")]
    public void ThenTheProgramSentTheRequest(string request) => Assert.Equal([request], _handler.Requests);

    [Then("the program sent no request")]
    public void ThenTheProgramSentNoRequest() => Assert.Empty(_handler.Requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string text) => Assert.Equal(text, _output.ToString().TrimEnd());

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEqual("", _error.ToString());

    [Then("standard error names {string}")]
    public void ThenStandardErrorNames(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error names {string} and {string}")]
    public void ThenStandardErrorNamesBoth(string first, string second)
    {
        Assert.Contains(first, _error.ToString());
        Assert.Contains(second, _error.ToString());
    }

    [Then("standard error holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error says the service lacks one of the two codes")]
    public void ThenStandardErrorSaysTheServiceLacksACode() => Assert.Contains("lacks one of the two codes", _error.ToString());

    [Then("standard error says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => Assert.Contains("gave no answer", _error.ToString());

    [Then("standard error and standard output hold no text of the key")]
    public void ThenNoTextOfTheKey()
    {
        Assert.DoesNotContain("test-key", _error.ToString());
        Assert.DoesNotContain("test-key", _output.ToString());
    }

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int code) => Assert.Equal(code, _exitCode);

    [Then("the exit code is not 0")]
    public void ThenTheExitCodeIsNotZero() => Assert.NotEqual(0, _exitCode);
}
