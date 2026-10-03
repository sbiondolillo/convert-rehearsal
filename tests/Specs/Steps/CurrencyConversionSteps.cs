using System.Globalization;
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
    private string? _key;
    private int _exitCode = -1;

    public void Dispose()
    {
        _handler.Dispose();
        _output.Dispose();
        _error.Dispose();
    }

    [Given("the environment variable EXCHANGERATE_API_KEY holds {string}")]
    public void GivenKeyHolds(string key) => _key = key;

    [Given("the environment variable EXCHANGERATE_API_KEY is unset")]
    public void GivenKeyUnset() => _key = null;

    [Given("the environment variable EXCHANGERATE_API_KEY is empty")]
    public void GivenKeyEmpty() => _key = "";

    [Given("the service answers with the rate {string}")]
    public void GivenRateText(string rate) => GivenRate(decimal.Parse(rate, CultureInfo.InvariantCulture));

    [Given("the service answers with the rate {float}")]
    public void GivenRateFloat(double rate) => GivenRate((decimal)rate);

    private void GivenRate(decimal rate) =>
        _handler.Respond(HttpStatusCode.OK, $$"""{"result":"success","conversion_rate":{{rate.ToString(CultureInfo.InvariantCulture)}}}""");

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenError(int status, string errorType) =>
        _handler.Respond((HttpStatusCode)status, $$"""{"result":"error","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenNoAnswer() => _handler.Fail();

    [When("a person runs the program with {string}")]
    public async Task WhenRun(string arguments)
    {
        using var http = new HttpClient(_handler);
        _exitCode = await ConvertCommand.RunAsync(
            arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            name => name == ConvertCommand.KeyVariable ? _key : null,
            http,
            _output,
            _error);
    }

    [Then("the program sends the request {string}")]
    public void ThenRequest(string request) => Assert.Equal([request], _handler.Requests);

    [Then("the program sends no request")]
    public void ThenNoRequest() => Assert.Empty(_handler.Requests);

    [Then("standard output is {string}")]
    public void ThenOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd('\n', '\r'));

    [Then("standard output is empty")]
    public void ThenOutputEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds an error")]
    public void ThenErrorHeld() => Assert.NotEqual("", _error.ToString().Trim());

    [Then("standard error holds a line that names {string}")]
    public void ThenErrorNames(string text) => Assert.Contains(text, _error.ToString(), StringComparison.Ordinal);

    [Then("standard error holds a line that holds {string}")]
    public void ThenErrorHolds(string text) => Assert.Contains(text, _error.ToString(), StringComparison.Ordinal);

    [Then("standard error holds a line that says the service lacks one of the two codes and names {string} and {string}")]
    public void ThenErrorLacks(string first, string second)
    {
        string line = _error.ToString().Trim();
        Assert.DoesNotContain('\n', line);
        Assert.Contains("lacks", line, StringComparison.Ordinal);
        Assert.Contains(first, line, StringComparison.Ordinal);
        Assert.Contains(second, line, StringComparison.Ordinal);
    }

    [Then("standard error holds a line that says the service gave no answer")]
    public void ThenErrorNoAnswer() => Assert.Contains("gave no answer", _error.ToString(), StringComparison.Ordinal);

    [Then("standard error and standard output hold no text of the key {string}")]
    public void ThenNoKey(string key)
    {
        Assert.DoesNotContain(key, _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(key, _output.ToString(), StringComparison.Ordinal);
    }

    [Then("the exit code is {int}")]
    public void ThenExit(int code) => Assert.Equal(code, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenExitNot(int code) => Assert.NotEqual(code, _exitCode);
}
