using System.CommandLine;
using System.Net;
using System.Text;
using Core;
using Reqnroll;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class ConversionSteps
{
    private readonly Dictionary<string, string> _environment = [];
    private readonly List<string> _requests = [];
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException("no stub answer");
    private string _key = "";
    private string _output = "";
    private string _error = "";
    private int _exitCode = -1;

    [Given("the environment variable EXCHANGERATE_API_KEY holds {string}")]
    public void GivenTheKeyHolds(string value)
    {
        _key = value;
        _environment["EXCHANGERATE_API_KEY"] = value;
    }

    [Given("the environment variable EXCHANGERATE_API_KEY is unset")]
    public void GivenTheKeyIsUnset() => _environment.Remove("EXCHANGERATE_API_KEY");

    [Given("the service answers with the rate {decimal}")]
    public void GivenTheServiceAnswersWithTheRate(decimal rate) =>
        _answer = () => Json(HttpStatusCode.OK, FormattableString.Invariant($$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}"""));

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithAnError(int status, string errorType) =>
        _answer = () => Json((HttpStatusCode)status, $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() => _answer = () => throw new HttpRequestException("connection refused");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgram(string arguments)
    {
        using var http = new HttpClient(new StubHandler(this));
        RootCommand root = ConvertCommand.Create(new ExchangeRateClient(http), name => _environment.GetValueOrDefault(name));
        using var output = new StringWriter();
        using var error = new StringWriter();
        _exitCode = await ConvertCommand.RunAsync(root.Parse(arguments), new InvocationConfiguration { Output = output, Error = error }, TestContext.Current.CancellationToken);
        _output = output.ToString();
        _error = error.ToString();
    }

    [Then("the program sends the request {string}")]
    public void ThenTheProgramSendsTheRequest(string request) => Assert.Equal([request], _requests);

    [Then("the program sends no request")]
    public void ThenTheProgramSendsNoRequest() => Assert.Empty(_requests);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) => Assert.Equal(expected, _output.TrimEnd());

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal("", _output);

    [Then("standard error holds an error")]
    public void ThenStandardErrorHoldsAnError() => Assert.NotEqual("", _error.Trim());

    [Then("standard error holds a line that names {string}")]
    public void ThenStandardErrorNames(string text) => AssertErrorLine(line => line.Contains(text, StringComparison.Ordinal));

    [Then("standard error holds a line that holds {string}")]
    public void ThenStandardErrorHolds(string text) => AssertErrorLine(line => line.Contains(text, StringComparison.Ordinal));

    [Then("standard error holds a line that says the service lacks one of the two codes and names {string} and {string}")]
    public void ThenStandardErrorSaysLacks(string first, string second) =>
        AssertErrorLine(line => line.Contains("lacks", StringComparison.Ordinal)
            && line.Contains(first, StringComparison.Ordinal)
            && line.Contains(second, StringComparison.Ordinal));

    [Then("standard error holds a line that says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => AssertErrorLine(line => line.Contains("gave no answer", StringComparison.Ordinal));

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the exit code is not 0")]
    public void ThenTheExitCodeIsNot0() => Assert.NotEqual(0, _exitCode);

    [Then("standard output and standard error hold no text of the key")]
    public void ThenNoTextOfTheKey()
    {
        Assert.NotEqual("", _key);
        Assert.DoesNotContain(_key, _output, StringComparison.Ordinal);
        Assert.DoesNotContain(_key, _error, StringComparison.Ordinal);
    }

    private void AssertErrorLine(Func<string, bool> predicate) =>
        Assert.Contains(_error.Split('\n'), line => predicate(line));

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(ConversionSteps steps) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            steps._requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(steps._answer());
        }
    }
}
