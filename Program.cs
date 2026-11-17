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
builder.Services.AddScoped<ProfileColorService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<UserLookupService>();
builder.Services.AddScoped<PostFeedService>();
builder.Services.AddScoped<MusicCatalogService>();
builder.Services.AddScoped<ConcertService>();
builder.Services.AddScoped<TwoFactorService>();
builder.Services.AddScoped<ArtistSubscriptionService>();
builder.Services.AddScoped<FriendshipService>();
builder.Services.AddScoped<UserBlockService>();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp =>
{
    var baseAddress = new Uri(
        builder.Configuration["BackendApi:BaseUrl"]
        ?? builder.HostEnvironment.BaseAddress);
    return new HttpClient(new BackendAuthorizationHandler(sp, baseAddress)
    {
        InnerHandler = new HttpClientHandler()
    }) { BaseAddress = baseAddress };
});
builder.Services.Configure<BackendApiOptions>(
    builder.Configuration.GetSection("BackendApi"));

await builder.Build().RunAsync();
