using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Services.AddHttpClient<RateClient>();
using IHost host = builder.Build();
RootCommand root = ConvertCommand.Create(host.Services.GetRequiredService<RateClient>(), Environment.GetEnvironmentVariable);
ParseResult parse = root.Parse(args);
return await parse.InvokeAsync().ConfigureAwait(false);
