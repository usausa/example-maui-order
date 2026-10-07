using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<TableOrder.Web.Kitchen.Components.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// System
builder.Services.AddSingleton(TimeProvider.System);

await builder.Build().RunAsync();
