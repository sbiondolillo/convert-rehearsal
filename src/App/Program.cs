using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
// The HTTP client logs the request address, which holds the key.
builder.Logging.ClearProviders();
builder.Services.AddHttpClient<ExchangeRateClient>();
using IHost host = builder.Build();
RootCommand root = ConvertCommand.Create(host.Services.GetRequiredService<ExchangeRateClient>(), Environment.GetEnvironmentVariable);
ParseResult parse = root.Parse(args);
return await parse.InvokeAsync().ConfigureAwait(false);
