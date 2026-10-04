using System.Net;
using System.Text;

namespace Specs;

/// <summary>A stub handler that records each request and answers from a function.</summary>
public sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add($"{request.Method} {request.RequestUri}");
        return Task.FromResult(answer(request));
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Rate(string rate) =>
        Json(HttpStatusCode.OK, $$"""{"result":"success","base_code":"USD","target_code":"EUR","conversion_rate":{{rate}}}""");

    public static HttpResponseMessage Error(string errorType) =>
        Json(
            errorType is "unsupported-code" ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden,
            $$"""{"result":"error","documentation":"https://www.exchangerate-api.com/docs","terms-of-use":"https://www.exchangerate-api.com/terms","error-type":"{{errorType}}"}""");
}
