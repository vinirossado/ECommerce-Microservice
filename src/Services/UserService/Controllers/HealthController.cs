using Microsoft.AspNetCore.Mvc;

namespace User.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "Healthy", Service = "UserService", Timestamp = DateTime.UtcNow });
    }
}
