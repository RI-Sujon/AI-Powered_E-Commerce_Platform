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
// ⬇️ ADD THESE FOR KEY VAULT
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
// AI (Phase 3): abstractions used by the /ai/selftest endpoint and later features
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 🔐 KEY VAULT CONFIGURATION (NEW!)
// ============================================
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];

if (!string.IsNullOrEmpty(keyVaultUri))
{
    Console.WriteLine($"🔐 Connecting to Key Vault: {keyVaultUri}");
    
    try
    {
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // Try managed identity first, then Azure CLI, then other methods
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false,
            ExcludeInteractiveBrowserCredential = true
        });
        
        var secretClient = new SecretClient(new Uri(keyVaultUri), credential);
        
        builder.Configuration.AddAzureKeyVault(secretClient, 
            new Azure.Extensions.AspNetCore.Configuration.Secrets.KeyVaultSecretManager());
        
        Console.WriteLine("✅ Key Vault connected successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Key Vault connection failed: {ex.Message}");
        if (!builder.Environment.IsDevelopment())
        {
            throw; // Fail fast in non-dev environments if Key Vault fails
        }
    }
}
else
{
    Console.WriteLine("ℹ️ No Key Vault configured - using local configuration");
}

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

// ============================================
// JWT KEY VALIDATION (UPDATED FOR KEY VAULT)
// ============================================
const string JwtKeyPlaceholder = "CHANGE_THIS_TO_A_STRONG_SECRET_KEY_AT_LEAST_32_CHARS";

// Try to get JWT key from Key Vault first, then fall back to configuration
var jwtKey = builder.Configuration["JwtSecretKey"]  // From Key Vault
    ?? builder.Configuration["Jwt:Key"]              // From appsettings
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Set the 'Jwt__Key' environment variable, " +
        "configure Key Vault, or use User Secrets.");

if (jwtKey == JwtKeyPlaceholder && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException(
        "Jwt:Key must not be the default placeholder in non-development environments. " +
        "Set the 'Jwt__Key' environment variable or configure Key Vault.");

var jwtIssuer = builder.Configuration["JwtIssuer"]     // From Key Vault
    ?? builder.Configuration["Jwt:Issuer"]             // From appsettings
    ?? "ECommerceAPI";

var jwtAudience = builder.Configuration["JwtAudience"] // From Key Vault
    ?? builder.Configuration["Jwt:Audience"]           // From appsettings
    ?? "ECommerceClients";

Console.WriteLine($"🔑 JWT Issuer: {jwtIssuer}, Audience: {jwtAudience}");

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
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
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
builder.Services.AddAiDependencyGroup(builder.Configuration); // IChatClient + IEmbeddingGenerator (Azure OpenAI)
builder.Services.AddScoped<IApplicationContext, ApplicationContext>();
builder.Services.AddScoped<ICacheProvider, InMemoryCacheProvider>();
builder.Services.AddTransient<ILogProvider, SerilogProvider>();

// ============================================
// DATABASE CONNECTION (UPDATED FOR KEY VAULT)
// ============================================
var connectionString = builder.Configuration["PostgresConnectionString"]  // From Key Vault
    ?? builder.Configuration.GetConnectionString("DefaultConnection");    // From appsettings

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Database connection string not found in configuration or Key Vault");
}

Console.WriteLine($"📊 Database: {connectionString.Split(';')[0]}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- Health Checks ---
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString);

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

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// --- AI wiring self-test (Phase 3) ---------------------------------------------------
// Confirms the app can reach Azure OpenAI with its managed identity. Costs a fraction of a
// cent per call (one embedding + a ~5-token chat completion).
// TODO(phase 7): gate behind admin auth or remove before real production traffic.
app.MapGet("/ai/selftest", async (HttpContext ctx) =>
{
    var chat = ctx.RequestServices.GetService<IChatClient>();
    var embeddings = ctx.RequestServices.GetService<IEmbeddingGenerator<string, Embedding<float>>>();

    if (chat is null || embeddings is null)
        return Results.Json(
            new { ok = false, reason = "AzureOpenAI:Endpoint not configured" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var vector = await embeddings.GenerateVectorAsync("waterproof hiking jacket");
    var reply = await chat.GetResponseAsync("Reply with exactly: OK");

    return Results.Json(new
    {
        ok = true,
        chat = reply.Text,
        embeddingDimensions = vector.Length,
        tokensIn = reply.Usage?.InputTokenCount,
        tokensOut = reply.Usage?.OutputTokenCount
    });
});

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

Console.WriteLine("🚀 Application started successfully");

app.Run();