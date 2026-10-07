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
    private readonly Dictionary<string, string?> _environment = [];
    private readonly List<string> _requests = [];
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException("no answer");
    private int _exitCode;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    [Given("the environment variable {string} holds {string}")]
    public void GivenTheVariableHolds(string name, string value) => _environment[name] = value;

    [Given("the environment variable {string} is unset")]
    public void GivenTheVariableIsUnset(string name) => _environment[name] = null;

    [Given("the service answers the rate {double}")]
    public void GivenTheServiceAnswersTheRate(double rate) =>
        Answer(HttpStatusCode.OK, string.Create(CultureInfo.InvariantCulture,
            $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}"""));

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithError(int status, string errorType) =>
        Answer((HttpStatusCode)status, $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() =>
        _answer = () => throw new HttpRequestException("no answer from https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgram(string arguments)
    {
        using var http = new HttpClient(new StubHandler(this));
        RootCommand root = ConvertCommand.Create(http, name => _environment.GetValueOrDefault(name));
        _exitCode = await root.Parse(arguments.Split(' ')).InvokeAsync(new InvocationConfiguration { Output = _output, Error = _error });
    }

    [Then("the program sends the request {string}")]
    public void ThenTheProgramSendsTheRequest(string request) => Assert.Equal([request], _requests);

    [Then("the program sends no request")]
    public void ThenTheProgramSendsNoRequest() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd());

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Empty(_output.ToString());

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEmpty(_error.ToString());

    [Then("standard error names {string}")]
    public void ThenStandardErrorNames(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, _error.ToString());

    [Then("standard error says the service lacks one of the codes {string} and {string}")]
    public void ThenStandardErrorSaysTheServiceLacks(string first, string second)
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
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenTheExitCodeIsNot(int unexpected) => Assert.NotEqual(unexpected, _exitCode);

    private void Answer(HttpStatusCode status, string json) =>
        _answer = () => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(ConversionSteps steps) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            steps._requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(steps._answer());
        }
    }
}
