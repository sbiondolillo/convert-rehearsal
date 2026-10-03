using Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

HostApplicationBuilder builder = Host.CreateApplicationBuilder();
builder.Services.AddHttpClient();
using IHost host = builder.Build();
HttpClient http = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient();
RootCommand root = ConvertCommand.Create(http, Environment.GetEnvironmentVariable);
ParseResult parse = root.Parse(args);
return await parse.InvokeAsync().ConfigureAwait(false);
