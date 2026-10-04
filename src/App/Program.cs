using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
// The HTTP client logs each request address, and the address holds the key.
builder.Logging.ClearProviders();
builder.Services.AddHttpClient();
using IHost host = builder.Build();
HttpClient http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
return await ConvertCommand.RunAsync(args, http, new InvocationConfiguration()).ConfigureAwait(false);
