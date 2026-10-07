using System.Net;
using System.Text;

namespace Specs.Steps;

/// <summary>Answers every request with one canned answer, or with no answer, and records the requests.</summary>
public sealed class StubHandler : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public string? Body { get; set; }

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add($"{request.Method} {request.RequestUri}");
        if (Body is null)
        {
            throw new HttpRequestException($"No answer from {request.RequestUri}");
        }

        return Task.FromResult(new HttpResponseMessage(Status)
        {
            Content = new StringContent(Body, Encoding.UTF8, "application/json"),
        });
    }
}
