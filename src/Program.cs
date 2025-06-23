using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using DotnetDemoapp;

var builder = WebApplication.CreateBuilder(args);

// ✅ Bind to all IPs on port 5000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000); // Allow external access via EC2 IP
});

builder.Services.AddApplicationInsightsTelemetry();

// Make Azure AD auth an optional feature if the config is present
if (builder.Configuration.GetSection("AzureAd").Exists() &&
    builder.Configuration.GetSection("AzureAd").GetValue<string>("ClientId") != "")
{
    builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration)
                    .EnableTokenAcquisitionToCallDownstreamApi()
                    .AddMicrosoftGraph()
                    .AddInMemoryTokenCaches();
}

builder.Services.AddRazorPages().AddMicrosoftIdentityUI();

var app = builder.Build();

// Conditionally enable auth middleware
if (builder.Configuration.GetSection("AzureAd").Exists() &&
    builder.Configuration.GetSection("AzureAd").GetValue<string>("ClientId") != "")
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers(); // Needed for Microsoft.Identity.Web.UI
}

app.UseStaticFiles();
app.MapRazorPages();
app.UseStatusCodePages("text/html", "<!doctype html><h1>&#128163;HTTP error! Status code: {0}</h1>");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// API routes for monitoring data and weather
app.MapGet("/api/monitor", async () =>
{
    return new
    {
        cpuPercentage = Convert.ToInt32(await ApiHelper.GetCpuUsageForProcess()),
        workingSet = Environment.WorkingSet
    };
});

app.MapGet("/api/weather/{posLat:double}/{posLong:double}", async (double posLat, double posLong) =>
{
    string apiKey = builder.Configuration.GetValue<string>("Weather:ApiKey");
    (int status, string data) = await ApiHelper.GetOpenWeather(apiKey, posLat, posLong);
    return status == 200 ? Results.Content(data, "application/json") : Results.StatusCode(status);
});

// Start the app
app.Run();

