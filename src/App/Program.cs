using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Services.AddHttpClient<ExchangeRateClient>().AddStandardResilienceHandler();
using IHost host = builder.Build();
RootCommand root = ConvertCommand.Create(host.Services.GetRequiredService<ExchangeRateClient>(), Environment.GetEnvironmentVariable);
ParseResult parse = root.Parse(args);
return await ConvertCommand.RunAsync(parse, null, CancellationToken.None).ConfigureAwait(false);
