// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Serialization;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Models;
using ParseM3UNet.Helpers;

var builder = WebApplication.CreateSlimBuilder();
SettingsModel settingsModel = JsonSerializer.Deserialize<SettingsModel>(await File.ReadAllTextAsync(Env.ConfigFile), JsonUtils.JsonOption)!;
RegexStatic.Instance = new RegexStatic(settingsModel);

builder.WebHost.ConfigureKestrel(a => a.ListenAnyIP(settingsModel.Http.ListenPort));
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Services.AddSingleton<SettingsModel>(a => settingsModel);
builder.Services.AddScoped<PeriodicSync>();
builder.Services.AddScoped<M3UParser>();
builder.Services.AddScoped<StrmBuilder>();
builder.Services.AddScoped<KnownDirectory>();
builder.Services.AddScoped<LocalFileSync>();
builder.Services.AddSingleton<FileDownloader>();
builder.Services.AddHostedService<PeriodicSync>();
var app = builder.Build();



app.Use(async (context, next) => await HttpRequestHandler.HandleRequest(context, next));

await app.StartAsync();

await app.WaitForShutdownAsync();

