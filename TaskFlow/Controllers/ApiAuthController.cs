using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Api;
using TaskFlowApp.Data;

namespace TaskFlow.Controllers;

[ApiController]
[Route("api/auth")]
public class ApiAuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public ApiAuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users
            .OrderBy(user => user.FullName)
            .Select(user => new ApiAuthUserDto(user.Id, user.FullName, user.UserName, user.Role))
            .ToListAsync();

        return Ok(ApiResponse<IReadOnlyList<ApiAuthUserDto>>.Ok(users));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(ApiLoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == request.UserId);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Пользователь не найден."));
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(GetTokenLifetimeMinutes());
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.GivenName, user.UserName),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var response = new ApiLoginResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt,
            new ApiAuthUserDto(user.Id, user.FullName, user.UserName, user.Role));

        return Ok(ApiResponse<ApiLoginResponse>.Ok(response));
    }

    private int GetTokenLifetimeMinutes()
    {
        return int.TryParse(_configuration["Jwt:ExpiresMinutes"], out var minutes) ? minutes : 120;
    }
}
