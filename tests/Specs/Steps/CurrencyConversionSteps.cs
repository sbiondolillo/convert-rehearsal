using System.CommandLine;
using System.Net;
using System.Text;
using Core;
using Reqnroll;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class CurrencyConversionSteps : IDisposable
{
    private readonly List<string> _requests = [];
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private string? _key;
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException("no stub answer");
    private int _exitCode = -1;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    [Given("EXCHANGERATE_API_KEY holds {string}")]
    public void GivenTheKeyHolds(string key) => _key = key;

    [Given("EXCHANGERATE_API_KEY is unset")]
    public void GivenTheKeyIsUnset() => _key = null;

    [Given("EXCHANGERATE_API_KEY is empty")]
    public void GivenTheKeyIsEmpty() => _key = "";

    [Given("the service answers with the rate {word}")]
    public void GivenTheServiceAnswersWithTheRate(string rate) =>
        _answer = () => Json(HttpStatusCode.OK, $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}""");

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithAnError(int status, string errorType) =>
        _answer = () => Json((HttpStatusCode)status, $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() =>
        _answer = () => throw new HttpRequestException("Connection refused (v6.exchangerate-api.com:443)");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgram(string arguments)
    {
        using var http = new HttpClient(new StubHandler(this));
        RootCommand root = ConvertCommand.Create(new RateClient(http), name => name is RateClient.KeyVariable ? _key : null);
        var configuration = new InvocationConfiguration { Output = _output, Error = _error };
        _exitCode = await root.Parse(arguments).InvokeAsync(configuration).ConfigureAwait(false);
    }

    [Then("the program sent the request {string}")]
    public void ThenTheProgramSentTheRequest(string request) => Assert.Equal([request], _requests);

    [Then("the program sent no request")]
    public void ThenTheProgramSentNoRequest() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd());

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEqual("", _error.ToString().Trim());

    [Then("standard error names {string}")]
    public void ThenStandardErrorNames(string name) => Assert.Contains(name, _error.ToString());

    [Then("standard error holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error says the service lacks one of the codes {string} and {string}")]
    public void ThenStandardErrorSaysTheServiceLacks(string first, string second)
    {
        string text = _error.ToString();
        Assert.Contains("lacks one of", text);
        Assert.Contains(first, text);
        Assert.Contains(second, text);
    }

    [Then("standard error says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => Assert.Contains("gave no answer", _error.ToString());

    [Then("standard output and standard error hold no text of the key")]
    public void ThenNoTextOfTheKey()
    {
        Assert.NotNull(_key);
        Assert.DoesNotContain(_key, _output.ToString());
        Assert.DoesNotContain(_key, _error.ToString());
    }

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenTheExitCodeIsNot(int unexpected) => Assert.NotEqual(unexpected, _exitCode);

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
