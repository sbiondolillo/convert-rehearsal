using System.Net;
using System.Text;

namespace Specs.Steps;

/// <summary>A stub of the HTTP handler: records each request and answers with a canned answer.</summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _body = "{}";
    private bool _fail;

    public List<string> Requests { get; } = [];

    public void Respond(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
        _fail = false;
    }

    public void Fail() => _fail = true;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add($"{request.Method} {request.RequestUri}");
        if (_fail)
        {
            throw new HttpRequestException($"connection failed for {request.RequestUri}");
        }

        return Task.FromResult(new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body, Encoding.UTF8, "application/json"),
        });
    }
}
