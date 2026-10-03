using Core;
using Microsoft.Extensions.Hosting;
using System.CommandLine;

using IHost host = Host.CreateApplicationBuilder().Build();
// A plain client, not one of the host's factory: its logging would put the address, and so the key, in the logs.
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
RootCommand root = ConvertCommand.Create(http, Environment.GetEnvironmentVariable);
ParseResult parse = root.Parse(args);
return await parse.InvokeAsync().ConfigureAwait(false);
