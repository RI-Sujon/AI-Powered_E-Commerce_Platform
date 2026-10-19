var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 8080 for Docker. WEB_PORT lets local dev run this
// alongside the API (which also hardcodes 8080) without a port clash - unset, behavior is
// identical to before.
var webPort = int.TryParse(Environment.GetEnvironmentVariable("WEB_PORT"), out var configuredPort)
    ? configuredPort
    : 8080;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(webPort);
});

// Add services to the container.
builder.Services.AddRazorPages();

var app = builder.Build();

// Health check endpoint (required for Docker HEALTHCHECK)
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Only redirect to HTTPS in non-container environments
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Root redirect
app.MapGet("/", context => 
{
    context.Response.Redirect("/Product");
    return Task.CompletedTask;
});

app.MapRazorPages();

app.Run();