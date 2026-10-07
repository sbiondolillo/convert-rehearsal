using Core;
using System.CommandLine;
using System.Net;
using System.Text;
using Xunit;

namespace Specs.Unit;

public sealed class ConvertCommandTests
{
    private sealed class Handler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    private static async Task<(int Code, string Out)> RunAsync(string json, string arguments)
    {
        using var http = new HttpClient(new Handler(json));
        var output = new StringWriter();
        RootCommand root = ConvertCommand.Create(http, _ => "k");
        int code = await root.Parse(arguments.Split(' ')).InvokeAsync(new InvocationConfiguration { Output = output, Error = new StringWriter() });
        return (code, output.ToString().TrimEnd());
    }

    [Theory]
    [InlineData("""{"result":"success","conversion_rate":0.8888}""", "10 USD EUR", "8.89 EUR")]
    [InlineData("""{"result":"success","conversion_rate":157.8014}""", "2.50 USD JPY", "394.50 JPY")]
    [InlineData("""{"result":"success","conversion_rate":0.5}""", "0.05 usd eur", "0.03 EUR")]
    public async Task ConvertsAndRoundsAwayFromZero(string json, string arguments, string expected)
    {
        (int code, string output) = await RunAsync(json, arguments);
        Assert.Equal(0, code);
        Assert.Equal(expected, output);
    }

    [Fact]
    public async Task AnswerWithoutErrorTypeFails()
    {
        (int code, string output) = await RunAsync("""{"result":"error"}""", "1 USD EUR");
        Assert.Equal(1, code);
        Assert.Empty(output);
    }
}
