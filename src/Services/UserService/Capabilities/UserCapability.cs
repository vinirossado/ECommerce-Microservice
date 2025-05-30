using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using User.Context;
using User.Middleware;

namespace User.Capabilities;

public class UserCapability(
    UserDbContext context,
    IJwtTokenService jwtService,
    IPasswordHasher passwordHasher,
    ILogger<UserCapability> logger,
    IMemoryCache cache)
    : IUserCapability
{
    public async Task<AuthResponse?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive, cancellationToken);

        if (user == null)
        {
            logger.LogWarning("Login attempt failed: User {Username} not found", request.Username);
            return null;
        }

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            logger.LogWarning("Login attempt failed: User {Username} account is locked", request.Username);
            return null;
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(30);
                logger.LogWarning("User {Username} account locked due to too many failed attempts", request.Username);
            }

            await context.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;

        var token = jwtService.GenerateToken(user);
        var refreshToken = jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new Models.RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            CreatedByIp = request.IpAddress!
        };

        context.RefreshTokens.Add(refreshTokenEntity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} authenticated successfully", request.Username);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            User = MapToDto(user)
        };
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var userExists = await context.Users.AnyAsync(u =>
                u.Username == request.Username || u.Email == request.Email,
            cancellationToken);

        if (userExists)
        {
            logger.LogWarning("Registration failed: Username {Username} or Email {Email} already exists", request.Username, request.Email);
            return null;
        }

        var newUser = new Models.User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHasher.HashPassword(request.Password),
            Role = "User",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.Users.Add(newUser);
        await context.SaveChangesAsync(cancellationToken);

        var token = jwtService.GenerateToken(newUser);
        var refreshToken = jwtService.GenerateRefreshToken();

        var refreshTokenEntity = new Models.RefreshToken
        {
            Token = refreshToken,
            UserId = newUser.Id,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            CreatedByIp = request.IpAddress!
        };

        context.RefreshTokens.Add(refreshTokenEntity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {Username} registered successfully", request.Username);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            User = MapToDto(newUser)
        };
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken cancellationToken = default)
    {
        var refreshTokenEntity = await context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken, cancellationToken);

        if (refreshTokenEntity == null)
        {
            logger.LogWarning("Token refresh failed: Refresh token not found");
            return null;
        }

        if (!refreshTokenEntity.IsActive || refreshTokenEntity.IsExpired)
        {
            logger.LogWarning("Token refresh failed: Refresh token is expired or inactive");
            return null;
        }

        if (!refreshTokenEntity.User.IsActive)
        {
            logger.LogWarning("Token refresh failed: User account is inactive");
            return null;
        }

        refreshTokenEntity.IsActive = false;
        refreshTokenEntity.RevokedAt = DateTime.UtcNow;
        refreshTokenEntity.RevokedByIp = ipAddress;
        refreshTokenEntity.ReasonRevoked = "Replaced by new token";

        var newRefreshToken = jwtService.GenerateRefreshToken();

        refreshTokenEntity.ReplacedByToken = newRefreshToken;

        var jwtToken = jwtService.GenerateToken(refreshTokenEntity.User);

        var newRefreshTokenEntity = new Models.RefreshToken
        {
            Token = newRefreshToken,
            UserId = refreshTokenEntity.UserId,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            CreatedByIp = ipAddress
        };

        context.RefreshTokens.Add(newRefreshTokenEntity);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Refresh token rotated successfully for user {UserId}", refreshTokenEntity.UserId);

        return new AuthResponse
        {
            Token = jwtToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            User = MapToDto(refreshTokenEntity.User)
        };
    }

    public async Task<UserDto?> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user_{userId}";
        if (cache.TryGetValue(cacheKey, out UserDto? cachedUser))
        {
            logger.LogInformation("User {UserId} retrieved from cache", userId);
            return cachedUser;
        }

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
        {
            logger.LogWarning("User {UserId} not found", userId);
            return null;
        }

        var userDto = MapToDto(user);

        cache.Set(cacheKey, userDto, TimeSpan.FromMinutes(10));

        logger.LogInformation("User {UserId} retrieved from database", userId);
        return userDto;
    }

    public async Task<UserDto?> UpdateUserAsync(int userId, UserDto userDto, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

        if (user == null)
        {
            logger.LogWarning("Update failed: User {UserId} not found", userId);
            return null;
        }

        if (user.Username != userDto.Username &&
            await context.Users.AnyAsync(u => u.Username == userDto.Username && u.Id != userId, cancellationToken))
        {
            logger.LogWarning("Update failed: Username {Username} already exists", userDto.Username);
            return null;
        }

        if (user.Email != userDto.Email &&
            await context.Users.AnyAsync(u => u.Email == userDto.Email && u.Id != userId, cancellationToken))
        {
            logger.LogWarning("Update failed: Email {Email} already exists", userDto.Email);
            return null;
        }

        user.Username = userDto.Username;
        user.Email = userDto.Email;

        if (user.Role != userDto.Role && userDto.Role == "Admin")
        {
            logger.LogWarning("Attempt to change role to Admin for user {UserId} - additional verification required", userId);
            //TODO: Additional security measure - role changes to Admin should be verified
        }
        else
        {
            user.Role = userDto.Role;
        }

        await context.SaveChangesAsync(cancellationToken);

        cache.Remove($"user_{userId}");

        logger.LogInformation("User {UserId} updated successfully", userId);
        return MapToDto(user);
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            logger.LogWarning("Delete failed: User {UserId} not found", userId);
            return false;
        }

        user.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);

        cache.Remove($"user_{userId}");

        logger.LogInformation("User {UserId} deleted (marked as inactive) successfully", userId);
        return true;
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var skip = (page - 1) * pageSize;

        var users = await context.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        logger.LogInformation("Retrieved {Count} users for page {Page} with page size {PageSize}",
            users.Count, page, pageSize);

        return users.Select(MapToDto);
    }

    private static UserDto MapToDto(Models.User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role,
            Username = user.Username,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }
}