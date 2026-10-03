namespace Specs;

/// <summary>Records each request and answers it from a stub.</summary>
public sealed class StubHandler(List<string> requests, Func<HttpResponseMessage> answer) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        requests.Add($"{request.Method} {request.RequestUri}");
        return Task.FromResult(answer());
    }
}
