using System.CommandLine;
using System.Net;
using Core;
using Reqnroll;
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
    private int _exitCode = -1;

    public void Dispose()
    {
        _output.Dispose();
        _error.Dispose();
    }

    [Given("the environment variable {string} holds {string}")]
    public void GivenTheEnvironmentVariableHolds(string name, string value) => _environment[name] = value;

    [Given("the environment variable {string} is unset")]
    public void GivenTheEnvironmentVariableIsUnset(string name) => _environment[name] = null;

    [Given("the environment variable {string} is empty")]
    public void GivenTheEnvironmentVariableIsEmpty(string name) => _environment[name] = "";

    [Given(@"the service answers the rate (.*)")]
    public void GivenTheServiceAnswersTheRate(string rate) =>
        _answer = () => Json(HttpStatusCode.OK, $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}""");

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithStatusAndErrorType(int status, string errorType) =>
        _answer = () => Json((HttpStatusCode)status, $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() => _answer = () => throw new HttpRequestException("no answer");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgramWith(string arguments)
    {
        using var http = new HttpClient(new StubHandler(_requests, () => _answer()));
        RootCommand root = ConvertCommand.Create(
            new ExchangeRateClient(http),
            name => _environment.GetValueOrDefault(name));
        _exitCode = await root.Parse(arguments.Split(' ')).InvokeAsync(
            new InvocationConfiguration { Output = _output, Error = _error }).ConfigureAwait(false);
    }

    [Then("the program sent the request {string}")]
    public void ThenTheProgramSentTheRequest(string request) => Assert.Equal([request], _requests);

    [Then("the program sent no request")]
    public void ThenTheProgramSentNoRequest() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) => Assert.Equal(expected, _output.ToString().TrimEnd('\r', '\n'));

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _output.ToString());

    [Then("standard output does not hold {string}")]
    public void ThenStandardOutputDoesNotHold(string text) => Assert.DoesNotContain(text, _output.ToString());

    [Then("standard error is not empty")]
    public void ThenStandardErrorIsNotEmpty() => Assert.NotEqual("", _error.ToString());

    [Then("standard error holds one line, and it names {string}")]
    public void ThenStandardErrorHoldsOneLineAndItNames(string text) => AssertOneLineHolding(text);

    [Then("standard error holds one line, and it holds {string}")]
    public void ThenStandardErrorHoldsOneLineAndItHolds(string text) => AssertOneLineHolding(text);

    [Then("standard error holds one line, and it says the service lacks one of the codes {string} and {string}")]
    public void ThenStandardErrorSaysTheServiceLacksCodes(string first, string second)
    {
        string line = AssertOneLineHolding("lacks");
        Assert.Contains(first, line);
        Assert.Contains(second, line);
    }

    [Then("standard error holds one line, and it says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => AssertOneLineHolding("gave no answer");

    [Then("standard error does not hold {string}")]
    public void ThenStandardErrorDoesNotHold(string text) => Assert.DoesNotContain(text, _error.ToString());

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the exit code is not {int}")]
    public void ThenTheExitCodeIsNot(int unexpected) => Assert.NotEqual(unexpected, _exitCode);

    private string AssertOneLineHolding(string text)
    {
        string[] lines = _error.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string line = Assert.Single(lines);
        Assert.Contains(text, line);
        return line;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
}

internal sealed class StubHandler(List<string> requests, Func<HttpResponseMessage> answer) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Add($"{request.Method} {request.RequestUri}");
        return Task.FromResult(answer());
    }
}
