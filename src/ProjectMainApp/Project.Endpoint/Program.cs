using Project.Application.Extensions;
using Project.Data;
using Project.Core;
using Project.Core.Caching;
using Project.Core.Log;
using Microsoft.EntityFrameworkCore;
using Boooks.Net.Endpoint.ActionFilters;
using Project.Endpoint.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for Docker (listen on port 8080)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// --- Serilog ---
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

// --- JWT key validation at startup ---
const string JwtKeyPlaceholder = "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS";
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured. Set the 'Jwt__Key' environment variable or use User Secrets.");
if (jwtKey == JwtKeyPlaceholder && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Jwt:Key must not be the default placeholder in non-development environments. " +
        "Set the 'Jwt__Key' environment variable.");

// --- Authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// --- Rate limiting ---
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddManagersDependencyGroup();
builder.Services.AddScoped<IApplicationContext, ApplicationContext>();
builder.Services.AddScoped<ICacheProvider, InMemoryCacheProvider>();
builder.Services.AddTransient<ILogProvider, SerilogProvider>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- Health Checks ---
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<AddRequiredHeaderParameter>();
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS - Enhanced for Docker
builder.Services.AddCors(options =>
{
    // Development: allow any origin for convenience (local testing/tools)
    options.AddPolicy("DevCors", cors =>
    {
        cors.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });

    // Staging/Production: restrict to the known deployed Web app origins
    options.AddPolicy("AllowWebApp", cors =>
    {
        var allowedOrigins = new List<string>
        {
            "https://ecommerce-web-dev.whitewater-3611f9ba.eastus.azurecontainerapps.io",
            "https://ecommerce-web-staging.whitewater-3611f9ba.eastus.azurecontainerapps.io",
            "https://ecommerce-web-prod.whitewater-3611f9ba.eastus.azurecontainerapps.io"
        };

        // Allow extending via configuration (e.g. custom domains) without a code change
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (configuredOrigins is not null)
        {
            allowedOrigins.AddRange(configuredOrigins);
        }

        cors.WithOrigins(allowedOrigins.Distinct().ToArray())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply CORS based on environment
if (app.Environment.IsDevelopment())
{
    app.UseCors("DevCors");  // Allow all origins in dev
}
else
{
    app.UseCors("AllowWebApp");  // Restrict to specific origins in staging/prod
}
//app.UseHttpsRedirection(); // Already commented out - perfect for Docker!
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Auto-apply migrations in Development
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
        Log.Information("✅ Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "⚠️  Migration failed - database may not be ready yet");
    }
}

app.Run();