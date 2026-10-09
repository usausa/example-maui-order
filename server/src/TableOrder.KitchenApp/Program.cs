using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

using TableOrder.Client.Rest;
using TableOrder.Client.SignalR;
using TableOrder.KitchenApp;
using TableOrder.KitchenApp.Components;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// System
builder.Services.AddSingleton(TimeProvider.System);

// Browser
builder.Services.AddSingleton<BrowserStorage>();
builder.Services.AddSingleton<BrowserDeviceKey>();
builder.Services.AddSingleton<KioskScreen>();

// State
builder.Services.AddSingleton<StartupState>();
builder.Services.AddSingleton<Settings>();
builder.Services.AddSingleton<IDeviceContext>(static provider => provider.GetRequiredService<Settings>());
builder.Services.AddSingleton<StoreState>();
builder.Services.AddSingleton<TicketState>();
builder.Services.AddSingleton<StockState>();

// API (接続先はアプリを配ったサーバ。通知は WebSocket か Long Polling でつなぐ)
builder.Services.AddSingleton(new OrderServerOptions());
builder.Services.AddSingleton<RestConnection>();
builder.Services.AddSingleton<IDeviceApi, RestDeviceApi>();
builder.Services.AddSingleton<IKitchenApi, RestKitchenApi>();
builder.Services.AddSingleton<IOrderEvents, SignalROrderEvents>();

// Usecase
builder.Services.AddSingleton<KitchenUsecase>();

// Shell
builder.Services.AddSingleton<KitchenEventReceiver>();
builder.Services.AddSingleton<StatusReporter>();

//--------------------------------------------------------------------------------
// Run
//--------------------------------------------------------------------------------
var host = builder.Build();

host.Services.GetRequiredService<ILogger<App>>().InfoApplicationStart(AppInfo.Version);

await host.RunAsync();
