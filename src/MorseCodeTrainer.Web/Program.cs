// Copyright © 2026 myNOC LLC. All rights reserved.
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MorseCodeTrainer.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
WebHostConfiguration.Configure(builder.RootComponents, builder.Services);
await builder.Build().RunAsync();
