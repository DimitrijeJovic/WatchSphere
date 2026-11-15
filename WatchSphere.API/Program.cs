using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WatchSphere.API.Data;
using WatchSphere.API.Interfaces;
using WatchSphere.API.Middleware;
using WatchSphere.API.Services;
using WatchSphere.API.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- persistence

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

// Pinned rather than auto-detected: AutoDetect opens a connection on every
// startup and on every design-time command, so the app would not even build a
// migration without a reachable server.
var serverVersion = ServerVersion.Parse(
    builder.Configuration["Database:ServerVersion"] ?? "8.0.36-mysql");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        mySql => mySql.EnableRetryOnFailure(3)));

// ---------------------------------------------------------------------- auth

// Constructed once so a missing or too-short signing key stops the app here
// rather than failing on the first login.
var jwtSettings = new JwtSettings(builder.Configuration);

builder.Services.AddSingleton(jwtSettings);
builder.Services.AddScoped<JwtService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = jwtSettings.SigningKey,

            // Default is five minutes of grace, which keeps an expired token
            // working well past its stated lifetime.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ------------------------------------------------------------ domain services

builder.Services.AddScoped<ILibraryService, LibraryService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IEpisodeProgressService, EpisodeProgressService>();

// External catalogues.
//
// Each client is registered against its concrete type: AddHttpClient names the
// configuration after the type argument, so registering both against
// ICatalogProvider would give them one shared name and the second base address
// would silently win for both. The interface is mapped separately, which is
// what lets the import service take them all as IEnumerable<ICatalogProvider>.
builder.Services.AddHttpClient<KitsuProvider>(client =>
{
    client.BaseAddress = new Uri("https://kitsu.io/api/edge/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.api+json");
});

builder.Services.AddHttpClient<TvMazeProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.tvmaze.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<TmdbProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddTransient<ICatalogProvider>(sp => sp.GetRequiredService<KitsuProvider>());
builder.Services.AddTransient<ICatalogProvider>(sp => sp.GetRequiredService<TvMazeProvider>());
builder.Services.AddTransient<ICatalogProvider>(sp => sp.GetRequiredService<TmdbProvider>());

builder.Services.AddScoped<ICatalogImportService, CatalogImportService>();

// IMDb publishes a dataset rather than an API, so this one downloads a file
// and needs a longer leash than a normal call.
builder.Services.AddHttpClient("imdb", client => client.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddScoped<IImdbRatingService, ImdbRatingService>();

// Keeps running shows current on its own. Switched off with Sync:Enabled.
builder.Services.AddHostedService<CatalogSyncService>();

// ------------------------------------------------------------------- web layer

builder.Services.AddControllers();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Login and register are the endpoints worth guessing at, so they get their
// own bucket rather than sharing the global one.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? new[] { "http://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("VuePolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WatchSphere API",
        Version = "v1",
        Description = "Catalogue, watch tracking and reviews for anime, movies and series."
    });

    // Lets the Swagger UI send a bearer token, so the protected endpoints are
    // testable without a separate REST client.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/Auth/login."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

var app = builder.Build();

// ------------------------------------------------------------------- pipeline

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("VuePolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// A database that cannot be reached is a configuration mistake, not a bug, so
// it gets a readable message instead of a stack trace.
try
{
    await DatabaseSeeder.SeedAsync(app.Services, app.Configuration);
}
catch (MySqlConnector.MySqlException ex)
{
    var cannotConnect = ex.ErrorCode is MySqlConnector.MySqlErrorCode.UnableToConnectToHost
        or MySqlConnector.MySqlErrorCode.AccessDenied
        or MySqlConnector.MySqlErrorCode.UnknownDatabase;

    if (cannotConnect)
    {
        app.Logger.LogCritical(
            "Cannot reach MySQL: {Message}\n" +
            "Check ConnectionStrings:DefaultConnection in appsettings.json - the server must be " +
            "running and the user and password must match.",
            ex.Message);
    }
    else
    {
        // Reached the server but a statement failed, which almost always means
        // a migration met data or schema it did not expect.
        app.Logger.LogCritical(
            "The database schema could not be brought up to date: {Message}",
            ex.Message);
    }

    return 1;
}

app.Run();

return 0;
