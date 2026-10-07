using Core;
using Reqnroll;
using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class ConversionSteps : IDisposable
{
    private readonly List<string> _requests = [];
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private string? _key;
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException();
    private int _exitCode = -1;

    [Given("the environment variable {string} is unset")]
    public void GivenUnset(string name) => _key = null;

    [Given("the environment variable {string} is empty")]
    public void GivenEmpty(string name) => _key = "";

    [Given("the environment variable {string} holds {string}")]
    public void GivenHolds(string name, string value) => _key = value;

    [Given("the service answers with the rate {decimal}")]
    public void GivenRate(decimal rate)
    {
        string json = $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate.ToString(CultureInfo.InvariantCulture)}}}""";
        _answer = () => Json(HttpStatusCode.OK, json);
    }

    [Given("the service answers with the error-type {string}")]
    public void GivenErrorType(string errorType)
    {
        string json = $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""";
        HttpStatusCode status = errorType is "unsupported-code" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
        _answer = () => Json(status, json);
    }

    [Given("the service gives no answer")]
    public void GivenNoAnswer() => _answer = () => throw new HttpRequestException("no answer from " + _key);

    [When("a person runs the program with {string}")]
    public async Task WhenRuns(string arguments)
    {
        using var http = new HttpClient(new StubHandler(this));
        RootCommand root = ConvertCommand.Create(http, () => _key);
        ParseResult parse = root.Parse(arguments);
        _exitCode = await parse.InvokeAsync(new InvocationConfiguration { Output = _output, Error = _error }).ConfigureAwait(false);
    }

    [Then("the program sends the request {string}")]
    public void ThenSends(string request) => Assert.Equal([request], _requests);

    [Then("the program sends no request")]
    public void ThenSendsNone() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd('\r', '\n'));

    [Then("standard output is empty")]
    public void ThenOutputEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds text")]
    public void ThenErrorHoldsText() => Assert.False(string.IsNullOrWhiteSpace(_error.ToString()));

    [Then("standard error has a line that names {string}")]
    public void ThenErrorNames(string text) => Assert.Contains(ErrorLines(), line => line.Contains(text, StringComparison.Ordinal));

    [Then("standard error has a line that holds {string}")]
    public void ThenErrorHolds(string text) => ThenErrorNames(text);

    [Then("standard error has a line that says the service lacks one of the codes and names {string} and {string}")]
    public void ThenErrorLacks(string first, string second) =>
        Assert.Contains(ErrorLines(), line => line.Contains("lacks one of the codes", StringComparison.Ordinal)
            && line.Contains(first, StringComparison.Ordinal)
            && line.Contains(second, StringComparison.Ordinal));

    [Then("standard error has a line that says the service gave no answer")]
    public void ThenErrorNoAnswer() => Assert.Contains(ErrorLines(), line => line.Contains("gave no answer", StringComparison.Ordinal));

    [Then("standard output and standard error hold no text of the key")]
    public void ThenNoKey()
    {
        Assert.False(string.IsNullOrEmpty(_key));
        Assert.DoesNotContain(_key, _output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_key, _error.ToString(), StringComparison.Ordinal);
    }

    [Then("the exit code is {int}")]
    public void ThenExit(int code) => Assert.Equal(code, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenExitNot(int code) => Assert.NotEqual(code, _exitCode);

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    private string[] ErrorLines() => _error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(ConversionSteps steps) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            steps._requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(steps._answer());
        }
    }
}
