namespace User.Capabilities;

public interface IUserCapability
{
    Task<AuthResponse?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string ipAddress, CancellationToken cancellationToken = default);
    Task<UserDto?> GetUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserDto?> UpdateUserAsync(int userId, UserDto userDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserDto>> GetUsersAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
}

