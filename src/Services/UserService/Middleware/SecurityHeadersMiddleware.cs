using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace User.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Generate nonce for CSP
        var nonce = GenerateNonce();
        context.Items["csp-nonce"] = nonce;

        // Add security headers before processing a request
        AddSecurityHeaders(context, nonce);

        // Log security-relevant information
        LogSecurityInfo(context);

        await next(context);
    }

    private void AddSecurityHeaders(HttpContext context, string nonce)
    {
        var response = context.Response;
        var headers = response.Headers;

        // Enhanced Content Security Policy with nonce
        var csp = $"default-src 'self'; " +
                  $"script-src 'self' 'nonce-{nonce}' 'strict-dynamic'; " +
                  $"style-src 'self' 'nonce-{nonce}' 'unsafe-inline'; " +
                  $"img-src 'self' data: https:; " +
                  $"font-src 'self' https://fonts.gstatic.com; " +
                  $"connect-src 'self'; " +
                  $"frame-ancestors 'none'; " +
                  $"base-uri 'self'; " +
                  $"form-action 'self'; " +
                  $"upgrade-insecure-requests";

        headers["Content-Security-Policy"] = csp;

        // Additional security headers
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["X-XSS-Protection"] = "1; mode=block";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            
        // Cache control for sensitive responses
        if (context.Request.Path.StartsWithSegments("/api/users") ||
            context.Request.Path.StartsWithSegments("/api/auth"))
        {
            headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "0";
        }

        // Remove verbose error information in production
        if (!context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
        {
            headers.Remove("Server");
            headers["Server"] = "WebServer/1.0";
        }
    }

    private string GenerateNonce()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    private void LogSecurityInfo(HttpContext context)
    {
        var request = context.Request;
            
        // Log suspicious requests
        if (HasSuspiciousHeaders(request))
        {
            logger.LogWarning("Suspicious request detected from {IP}: {UserAgent}",
                context.Connection.RemoteIpAddress,
                request.Headers.UserAgent.ToString());
        }

        // Log authentication attempts
        if (request.Path.StartsWithSegments("/api/auth"))
        {
            logger.LogInformation("Authentication attempt from {IP} for path {Path}",
                context.Connection.RemoteIpAddress,
                request.Path);
        }
    }

    private bool HasSuspiciousHeaders(HttpRequest request)
    {
        var suspiciousPatterns = new[]
        {
            "sqlmap", "nikto", "burp", "havij", "pangolin",
            "nessus", "whatweb", "openvas", "nmap"
        };

        var userAgent = request.Headers.UserAgent.ToString().ToLower();
        return suspiciousPatterns.Any(pattern => userAgent.Contains(pattern));
    }
}

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Log request start
        logger.LogInformation("Request starting {Method} {Path} from {IP}",
            request.Method,
            request.Path,
            context.Connection.RemoteIpAddress);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
                
            // Log request completion
            logger.LogInformation("Request completed {Method} {Path} - {StatusCode} in {Duration}ms",
                request.Method,
                request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);

            // Log slow requests
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                logger.LogWarning("Slow request detected: {Method} {Path} took {Duration}ms",
                    request.Method,
                    request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}

// ===========================================
// JWT Token Service
// ===========================================

public interface IJwtTokenService
{
    string GenerateToken(Models.User user);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateToken(string token);
    bool IsTokenExpired(string token);
}

public class JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger) : IJwtTokenService
{
    public string GenerateToken(Models.User user)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);
            
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("jti", Guid.NewGuid().ToString()), // JWT ID for token tracking
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwtSettings["Issuer"],
            Audience = jwtSettings["Audience"],
            Expires = DateTime.UtcNow.AddMinutes(double.Parse(jwtSettings["ExpirationMinutes"])),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(secretKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JsonWebTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        logger.LogInformation("JWT token generated for user {UserId}", user.Id);
        return token;
    }

    public string GenerateRefreshToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[64];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var jwtSettings = configuration.GetSection("JwtSettings");
            var secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]);

            var tokenHandler = new JsonWebTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var result = tokenHandler.ValidateTokenAsync(token, validationParameters).Result;
            return result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity) : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }

    public bool IsTokenExpired(string token)
    {
        try
        {
            var tokenHandler = new JsonWebTokenHandler();
            var jsonToken = tokenHandler.ReadJsonWebToken(token);
            return jsonToken.ValidTo < DateTime.UtcNow;
        }
        catch
        {
            return true;
        }
    }
}

// ===========================================
// Password Hashing Service
// ===========================================

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool VerifyPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }
}

