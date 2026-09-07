var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 8080 for Docker
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
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