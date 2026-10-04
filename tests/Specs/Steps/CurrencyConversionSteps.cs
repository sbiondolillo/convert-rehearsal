using Core;
using Reqnroll;
using System.CommandLine;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class CurrencyConversionSteps : IDisposable
{
    private readonly Dictionary<string, string?> _saved = [];
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private Func<HttpRequestMessage, HttpResponseMessage> _answer = _ => throw new InvalidOperationException("No request expected.");
    private StubHandler? _handler;
    private int _exitCode = -1;

    [Given("the environment variable {string} holds {string}")]
    public void GivenTheVariableHolds(string name, string value) => SetVariable(name, value);

    [Given("the environment variable {string} is unset")]
    public void GivenTheVariableIsUnset(string name) => SetVariable(name, null);

    [Given("the service answers the rate {word}")]
    public void GivenTheServiceAnswersTheRate(string rate) => _answer = _ => StubHandler.Rate(rate);

    [Given("the service answers with the error-type {string}")]
    public void GivenTheServiceAnswersWithTheErrorType(string errorType) => _answer = _ => StubHandler.Error(errorType);

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() =>
        _answer = _ => throw new HttpRequestException("No connection to https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgramWith(string arguments)
    {
        _handler = new StubHandler(_answer);
        using var http = new HttpClient(_handler);
        var configuration = new InvocationConfiguration { Output = _output, Error = _error };
        _exitCode = await ConvertCommand.Create(http).Parse(arguments.Split(' ')).InvokeAsync(configuration);
    }

    [Then("the program sends the request {string}")]
    public void ThenTheProgramSendsTheRequest(string request) => Assert.Equal([request], _handler!.Requests);

    [Then("the program sends no request")]
    public void ThenTheProgramSendsNoRequest() => Assert.Empty(_handler!.Requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string text) => Assert.Equal(text, _output.ToString().TrimEnd());

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEqual("", _error.ToString().Trim());

    [Then("standard error names {string}")]
    [Then("standard error holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error says the service lacks one of the codes {string} and {string}")]
    public void ThenStandardErrorSaysTheServiceLacksACode(string first, string second)
    {
        string text = _error.ToString();
        Assert.Contains("lacks one of the codes", text);
        Assert.Contains(first, text);
        Assert.Contains(second, text);
    }

    [Then("standard error says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => Assert.Contains("gave no answer", _error.ToString());

    [Then("standard error and standard output hold no text of the key {string}")]
    public void ThenNoTextOfTheKey(string key)
    {
        Assert.DoesNotContain(key, _error.ToString());
        Assert.DoesNotContain(key, _output.ToString());
    }

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int code) => Assert.Equal(code, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenTheExitCodeIsNot(int code) => Assert.NotEqual(code, _exitCode);

    public void Dispose()
    {
        foreach ((string name, string? value) in _saved)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        _output.Dispose();
        _error.Dispose();
    }

    private void SetVariable(string name, string? value)
    {
        _saved.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, value);
    }
}
