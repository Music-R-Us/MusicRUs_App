using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MusicRUs.Services;
using Microsoft.Extensions.Options;
using MusicRUs.Layout;
using MusicRUs.Models;
using MusicRUs;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

//Add services to the container.
builder.Services.AddScoped<BackendApiClient>();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.AddScoped<ForumProfileService>();


builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(
        builder.Configuration["BackendApi:BaseUrl"]
        ?? builder.HostEnvironment.BaseAddress)
});
builder.Services.Configure<BackendApiOptions>(
    builder.Configuration.GetSection("BackendApi"));

await builder.Build().RunAsync();
