using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using User;
using User.Capabilities;
using User.Context;
using User.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/userservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Database Configuration
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication Configuration
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"] ?? string.Empty);

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretKey),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true
        };

        // Enhanced JWT events for security
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
                
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Log.Information("JWT Token validated for user: {UserId}",
                    context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                
                return Task.CompletedTask;
            }
        };
    });

// Authorization Policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("UserOrAdmin", policy =>
        policy.RequireRole("UserService", "Admin"));

    options.AddPolicy("ResourceOwner", policy =>
        policy.Requirements.Add(new ResourceOwnerRequirement()));
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
            
    options.AddPolicy("AuthPolicy", context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? context.Request.Headers.Host.ToString(),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        Log.Warning("Rate limit exceeded for {IP}", context.HttpContext.Connection.RemoteIpAddress);
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Try again later.", token);
    };
});

builder.Services.AddScoped<IUserCapability, UserCapability>();
builder.Services.AddScoped<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// Memory Caching
builder.Services.AddMemoryCache();

// Health Checks
// builder.Services.AddHealthChecks()
//     .AddDbContext<UserDbContext>()
//     .AddCheck("memory", () =>
//     {
//         var allocatedBytes = GC.GetTotalMemory(false);
//         var maxMemory = 1024 * 1024 * 500; // 500MB threshold
//         return allocatedBytes < maxMemory ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy() : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"Memory usage: {allocatedBytes / 1024 / 1024}MB");
//     });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UserService Service API",
        Version = "v1",
        Description = "Microservice for user management with comprehensive security"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security Headers Middleware - CRITICAL FOR SECURITY
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Content Security Policy - Prevents XSS attacks
    headers.ContentSecurityPolicy = "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'";

    // Prevent MIME type sniffing
    headers["X-Content-Type-Options"] = "nosniff";

    // Prevent clickjacking
    headers["X-Frame-Options"] = "DENY";

    // XSS Protection (legacy but still useful)
    headers["X-XSS-Protection"] = "1; mode=block";

    // Force HTTPS (if in production)
    if (!app.Environment.IsDevelopment())
    {
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    }

    // Control referrer information
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Permissions Policy (formerly Feature Policy)
    headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=(), usb=()";

    // Remove server information
    headers.Remove("Server");
    headers["Server"] = "WebServer";

    await next();
});

app.UseRateLimiter();

app.UseHttpsRedirection();

app.UseCors("AllowedOrigins");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseGlobalExceptionHandler();

app.MapControllers();

// app.MapHealthChecks("/health");
// app.MapHealthChecks("/health/ready");
// app.MapHealthChecks("/health/live");

// Graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() => { Log.Information("Application is shutting down..."); });

try
{
    Log.Information("Starting UserService Service...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}

namespace User
{
    // ===========================================
// Models and DTOs
// ===========================================

    public class LoginRequest
    {
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; } = string.Empty;

        [JsonIgnore]
        public string? IpAddress { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Username { get; } = string.Empty;
        public string Email { get; } = string.Empty;
        public string Password { get; } = string.Empty;
        
        [JsonIgnore]
        public string? IpAddress { get; set; } = string.Empty;

    }

    public class AuthResponse
    {
        public string Token { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public DateTime ExpiresAt { get; init; }
        public UserDto User { get; init; } = new();
    }

    public class UserDto
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public DateTime? LastLoginAt { get; init; }
    }

// ===========================================
// Authorization Requirements
// ===========================================

    public class ResourceOwnerRequirement : IAuthorizationRequirement
    {
    }

    public class ResourceOwnerAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ResourceOwnerRequirement requirement)
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

            // Allow admins to access any resource
            if (userRole == "Admin")
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Check if a user is accessing their own resource
            if (context.Resource is not HttpContext httpContext)
            {
                return Task.CompletedTask;
            }
            
            var resourceUserId = httpContext.Request.RouteValues["userId"]?.ToString();
            if (userId == resourceUserId)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}

