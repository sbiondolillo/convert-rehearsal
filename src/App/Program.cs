using Core;

using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(30) };
return await ConvertCommand.RunAsync(args, Environment.GetEnvironmentVariable, http, Console.Out, Console.Error).ConfigureAwait(false);
