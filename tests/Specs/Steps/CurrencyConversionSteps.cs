using System.Net;
using Reqnroll;
using Specs.Unit;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class CurrencyConversionSteps
{
    private const string Key = "test-key";

    private string? _key;
    private StubHandler _handler = StubHandler.Rate("1");
    private RunResult _result = new(0, "", "");

    [Given("the environment variable {string} holds {string}")]
    public void GivenTheVariableHolds(string name, string value)
    {
        Assert.Equal("EXCHANGERATE_API_KEY", name);
        _key = value;
    }

    [Given("the environment variable {string} is unset")]
    public void GivenTheVariableIsUnset(string name)
    {
        Assert.Equal("EXCHANGERATE_API_KEY", name);
        _key = null;
    }

    [Given(@"the service answers with the rate ([\d.]+)")]
    public void GivenTheServiceAnswersWithTheRate(string rate) => _handler = StubHandler.Rate(rate);

    [Given("the service answers with the status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithAnError(int status, string errorType) =>
        _handler = StubHandler.Json((HttpStatusCode)status, $"{{\"result\":\"error\",\"error-type\":\"{errorType}\"}}");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() => _handler = StubHandler.Throwing();

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgram(string arguments) =>
        _result = await Runner.RunAsync(_handler, _key, arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ConfigureAwait(false);

    [Then("the program sends the request {string}")]
    public void ThenTheProgramSendsTheRequest(string request) => Assert.Equal([request], _handler.Requests);

    [Then("the program sends no request")]
    public void ThenTheProgramSendsNoRequest() => Assert.Empty(_handler.Requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) =>
        Assert.Equal(expected + Environment.NewLine, _result.Output);

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _result.Output);

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEmpty(_result.Error.Trim());

    [Then("the exit code is not 0")]
    public void ThenTheExitCodeIsNotZero() => Assert.NotEqual(0, _result.ExitCode);

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int code) => Assert.Equal(code, _result.ExitCode);

    [Then("standard error is a line that names {string}")]
    public void ThenStandardErrorNames(string text) => Assert.Contains(text, OneErrorLine(), StringComparison.Ordinal);

    [Then("standard error is a line that holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, OneErrorLine(), StringComparison.Ordinal);

    [Then("standard error is a line that says the service lacks one of the two codes and names {string} and {string}")]
    public void ThenStandardErrorSaysLacks(string first, string second)
    {
        string line = OneErrorLine();
        Assert.Contains("lacks", line, StringComparison.Ordinal);
        Assert.Contains(first, line, StringComparison.Ordinal);
        Assert.Contains(second, line, StringComparison.Ordinal);
    }

    [Then("standard error is a line that says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() =>
        Assert.Contains("gave no answer", OneErrorLine(), StringComparison.Ordinal);

    [Then("standard output and standard error hold no text of the key")]
    public void ThenNoTextOfTheKey() =>
        Assert.DoesNotContain(Key, _result.Output + _result.Error, StringComparison.Ordinal);

    private string OneErrorLine()
    {
        string[] lines = _result.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Assert.Single(lines);
    }
}
