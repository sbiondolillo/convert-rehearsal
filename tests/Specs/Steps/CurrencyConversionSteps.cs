using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;
using Core;
using Reqnroll;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class CurrencyConversionSteps : IDisposable
{
    private const string Key = "test-key";

    private readonly List<string> _requests = [];
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private string? _previousKey;
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException("no answer");
    private int _exitCode = -1;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    [BeforeScenario]
    public void RememberKey() => _previousKey = Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable);

    [AfterScenario]
    public void RestoreKey() => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, _previousKey);

    [Given("the environment variable EXCHANGERATE_API_KEY holds {string}")]
    public static void GivenTheKeyHolds(string value) => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, value);

    [Given("the environment variable EXCHANGERATE_API_KEY is unset")]
    public static void GivenTheKeyIsUnset() => Environment.SetEnvironmentVariable(ConvertCommand.KeyVariable, null);

    [Given("the service answers the rate {decimal}")]
    public void GivenTheServiceAnswersTheRate(decimal rate)
    {
        string text = rate.ToString(CultureInfo.InvariantCulture);
        _answer = () => Json(HttpStatusCode.OK, $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{text}}}""");
    }

    [Given("the service answers status {int} with the error-type {string}")]
    public void GivenTheServiceAnswersAnError(int status, string errorType) =>
        _answer = () => Json((HttpStatusCode)status, $$"""{"result":"error","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() =>
        _answer = () => throw new HttpRequestException($"Connection refused (v6.exchangerate-api.com:443) {Key}");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRuns(string arguments)
    {
        using var http = new HttpClient(new StubHandler(this));
        _exitCode = await ConvertCommand.RunAsync(arguments.Split(" "), http, new InvocationConfiguration { Output = _output, Error = _error });
    }

    [Then("the program sent the request {string}")]
    public void ThenTheProgramSent(string request) => Assert.Equal([request], _requests);

    [Then("the program sent no request")]
    public void ThenTheProgramSentNoRequest() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd('\n'));

    [Then("standard output is empty")]
    public void ThenOutputIsEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds an error")]
    public void ThenErrorHoldsAnError() => Assert.NotEqual("", _error.ToString().Trim());

    [Then("standard error holds {string}")]
    public void ThenErrorHolds(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error names {string}")]
    public void ThenErrorNames(string name) => Assert.Contains(name, _error.ToString());

    [Then("standard error says the service lacks one of the codes {string} and {string}")]
    public void ThenErrorSaysLacks(string first, string second)
    {
        string error = _error.ToString();
        Assert.Contains("lacks one of the currency codes", error);
        Assert.Contains(first, error);
        Assert.Contains(second, error);
    }

    [Then("standard error says the service gave no answer")]
    public void ThenErrorSaysNoAnswer() => Assert.Contains("gave no answer", _error.ToString());

    [Then("standard error and standard output hold no text of the key")]
    public void ThenNoKey()
    {
        Assert.DoesNotContain(Key, _error.ToString());
        Assert.DoesNotContain(Key, _output.ToString());
    }

    [Then("the exit code is {int}")]
    public void ThenExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the exit code is not 0")]
    public void ThenExitCodeIsNotZero() => Assert.NotEqual(0, _exitCode);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(CurrencyConversionSteps steps) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            steps._requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(steps._answer());
        }
    }
}
