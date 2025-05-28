using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using User.Capabilities;
using User.Models;

namespace User.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController(IUserCapability userService, ILogger<AuthController> logger) : ControllerBase
    {
        /// <summary>
        /// User login
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("AuthPolicy")]
        [ProducesResponseType(typeof(AuthResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(429)]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            var authResponse = await userService.AuthenticateAsync(request);

            if (authResponse == null)
            {
                logger.LogWarning("Failed login attempt for username: {Username} from IP: {IP}",
                    request.Username, HttpContext.Connection.RemoteIpAddress);
                
                return Unauthorized("Invalid username or password");
            }

            logger.LogInformation("Successful login for username: {Username}", request.Username);
            
            return Ok(authResponse);
        }

        /// <summary>
        /// User registration
        /// </summary>
        [HttpPost("register")]
        [EnableRateLimiting("AuthPolicy")]
        [ProducesResponseType(typeof(AuthResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(429)]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            var authResponse = await userService.RegisterAsync(request);

            if (authResponse == null)
            {
                return Conflict("Username or email already exists");
            }

            logger.LogInformation("New user registered: {Username}", request.Username);
            return CreatedAtAction(nameof(Register), authResponse);
        }

        /// <summary>
        /// Refresh JWT token using a valid refresh token
        /// </summary>
        [HttpPost("refresh")]
        [EnableRateLimiting("AuthPolicy")]
        [ProducesResponseType(typeof(AuthResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(429)]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return BadRequest("Refresh token is required");
            }

            // Get IP address for security tracking
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            var authResponse = await userService.RefreshTokenAsync(request.RefreshToken, ipAddress, cancellationToken);

            if (authResponse == null)
            {
                logger.LogWarning("Failed refresh token attempt from IP: {IP}", ipAddress);
                return Unauthorized("Invalid or expired refresh token");
            }

            logger.LogInformation("Token refreshed successfully");
            return Ok(authResponse);
        }

        /// <summary>
        /// Health check endpoint
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(200)]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
        }
    }
}

