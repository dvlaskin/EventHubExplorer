using Domain.Configs;
using Infrastructure.IoC;
using WebUI.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(opt =>
    {
        opt.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(10);
        opt.DisconnectedCircuitMaxRetained = 1;
    });

// add app configuration
builder.Configuration.AddJsonFile("Data/appConfig.json", optional: true, reloadOnChange: true);
builder.Services.Configure<AppConfiguration>(builder.Configuration);

// add application services
builder.Services.AddInfrastructureServices();

builder.Services.Configure<HostOptions>(opt => opt.ShutdownTimeout = TimeSpan.FromSeconds(5));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
