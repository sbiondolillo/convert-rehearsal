using Core;
using Reqnroll;
using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;
using Xunit;

namespace Specs.Steps;

[Binding]
public sealed class ConversionSteps
{
    private readonly Dictionary<string, string> _environment = [];
    private readonly List<string> _requests = [];
    private Func<HttpResponseMessage> _answer = () => throw new HttpRequestException("no answer");
    private string _output = "";
    private string _error = "";
    private int _exitCode = -1;

    [Given("the environment variable EXCHANGERATE_API_KEY holds {string}")]
    public void GivenTheKeyHolds(string key) => _environment["EXCHANGERATE_API_KEY"] = key;

    [Given("the environment variable EXCHANGERATE_API_KEY is unset")]
    public void GivenTheKeyIsUnset() => _environment.Remove("EXCHANGERATE_API_KEY");

    [Given("the service answers with the rate {decimal}")]
    public void GivenTheServiceAnswersWithTheRate(decimal rate) =>
        _answer = () => Json(HttpStatusCode.OK, string.Create(CultureInfo.InvariantCulture,
            $$"""{"result":"success","documentation":"https://www.exchangerate-api.com/docs","terms_of_use":"https://www.exchangerate-api.com/terms","time_last_update_unix":1790985602,"time_last_update_utc":"Sat, 03 Oct 2026 00:00:02 +0000","time_next_update_unix":1791072002,"time_next_update_utc":"Sun, 04 Oct 2026 00:00:02 +0000","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}"""));

    [Given("the service answers with status {int} and the error-type {string}")]
    public void GivenTheServiceAnswersWithAnError(int status, string errorType) =>
        _answer = () => Json((HttpStatusCode)status,
            $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");

    [Given("the service gives no answer")]
    public void GivenTheServiceGivesNoAnswer() => _answer = () => throw new HttpRequestException("no answer from https://v6.exchangerate-api.com/v6/test-key/pair/USD/EUR");

    [When("a person runs the program with {string}")]
    public async Task WhenAPersonRunsTheProgram(string arguments)
    {
        using var http = new HttpClient(new StubHandler(_requests, () => _answer()));
        using var output = new StringWriter();
        using var error = new StringWriter();
        RootCommand root = ConvertCommand.Create(http, name => _environment.GetValueOrDefault(name));
        _exitCode = await root.Parse(arguments).InvokeAsync(new InvocationConfiguration { Output = output, Error = error });
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

    [Then("standard error names {string}")]
    public void ThenStandardErrorNames(string name) => Assert.Contains(name, _error);

    [Then("standard error holds {string}")]
    public void ThenStandardErrorHolds(string text) => Assert.Contains(text, _error);

    [Then("standard error says the service lacks one of the codes {string} and {string}")]
    public void ThenStandardErrorSaysTheServiceLacks(string first, string second)
    {
        Assert.Contains("lacks one of the currency codes", _error);
        Assert.Contains(first, _error);
        Assert.Contains(second, _error);
    }

    [Then("standard error says the service gave no answer")]
    public void ThenStandardErrorSaysNoAnswer() => Assert.Contains("gave no answer", _error);

    [Then("standard output and standard error hold no text of the key {string}")]
    public void ThenNoTextOfTheKey(string key)
    {
        Assert.DoesNotContain(key, _output);
        Assert.DoesNotContain(key, _error);
    }

    [Then("the exit code is not 0")]
    public void ThenTheExitCodeIsNotZero() => Assert.NotEqual(0, _exitCode);

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
