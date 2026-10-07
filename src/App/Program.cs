using Core;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

using IHost host = Host.CreateApplicationBuilder().Build();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
RootCommand root = ConvertCommand.Create(http, () => Environment.GetEnvironmentVariable(ConvertCommand.KeyVariable));
ParseResult parse = root.Parse(args);
return await parse.InvokeAsync().ConfigureAwait(false);
