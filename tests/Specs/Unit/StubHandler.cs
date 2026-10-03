using System.Net;
using System.Text;

namespace Specs.Unit;

/// <summary>A handler that records each request and returns a canned answer, or throws.</summary>
public sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public static StubHandler Json(HttpStatusCode status, string json) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    public static StubHandler Rate(string rate) =>
        Json(HttpStatusCode.OK, $"{{\"result\":\"success\",\"base_code\":\"USD\",\"target_code\":\"EUR\",\"conversion_rate\":{rate}}}");

    public static StubHandler Throwing() =>
        new(r => throw new HttpRequestException($"no answer from {r.RequestUri}"));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add($"{request.Method} {request.RequestUri}");
        return Task.FromResult(respond(request));
    }
}
