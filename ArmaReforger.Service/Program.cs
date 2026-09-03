using ArmaReforger.Identity.Bohemia;
using ArmaReforger.Identity.Configuration;
using ArmaReforger.Service.Configuration;
using ArmaReforger.Identity.Steam;
using ArmaReforger.Service.Tokens;
using ArmaReforger.Service.Workers;

var builder = WebApplication.CreateBuilder(args);

// Ошибки регистрации зависимостей должны падать на старте, а не в первом запросе.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateOnBuild = true;
    options.ValidateScopes = true;
});

builder.Services
    .AddOptions<SteamOptions>()
    .Bind(builder.Configuration.GetSection(SteamOptions.SectionName));

builder.Services
    .AddOptions<BohemiaOptions>()
    .Bind(builder.Configuration.GetSection(BohemiaOptions.SectionName));

builder.Services
    .AddOptions<TokenRefreshOptions>()
    .Bind(builder.Configuration.GetSection(TokenRefreshOptions.SectionName));

builder.Services.AddSingleton<IBiTokenStore, InMemoryBiTokenStore>();
builder.Services.AddSingleton<ISteamAuthStateStore, FileSteamAuthStateStore>();
builder.Services.AddSingleton<ISteamTicketProvider, SteamTicketProvider>();

builder.Services
    .AddHttpClient<IBiIdentityClient, BiIdentityClient>((serviceProvider, httpClient) =>
    {
        var options = serviceProvider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<BohemiaOptions>>()
            .Value;

        httpClient.BaseAddress = options.IdentityBaseAddress;
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    });

builder.Services.AddHostedService<BiTokenRefreshWorker>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();
