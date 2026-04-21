using AsteriskManager.Components;
using AsteriskManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = 512 * 1024;
});

builder.Services.Configure<AsteriskSettings>(
    builder.Configuration.GetSection("Asterisk"));
builder.Services.Configure<MqttSettings>(
    builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<AutoUpdateSettings>(
    builder.Configuration.GetSection("AutoUpdate"));
builder.Services.AddHttpClient();
builder.Services.AddScoped<AsteriskService>();
builder.Services.AddScoped<WifiService>();
builder.Services.AddScoped<PjsipManagementService>();
builder.Services.AddHostedService<MqttService>();
builder.Services.AddHostedService<AutoUpdateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
