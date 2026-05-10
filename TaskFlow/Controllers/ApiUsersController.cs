using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api;
using TaskFlow.Security;
using TaskFlowApp.Data;
using TaskFlowApp.Models;

namespace TaskFlow.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.Admin)]
public class ApiUsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApiUsersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? role, string? sort)
    {
        var query = _context.Users
            .Include(user => user.Tasks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(user => user.FullName.Contains(normalizedSearch) || user.UserName.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(user => user.Role == role);
        }

        query = sort switch
        {
            "name_desc" => query.OrderByDescending(user => user.FullName),
            "login" => query.OrderBy(user => user.UserName),
            "login_desc" => query.OrderByDescending(user => user.UserName),
            "role" => query.OrderBy(user => user.Role).ThenBy(user => user.FullName),
            "role_desc" => query.OrderByDescending(user => user.Role).ThenBy(user => user.FullName),
            "tasks" => query.OrderBy(user => user.Tasks.Count).ThenBy(user => user.FullName),
            "tasks_desc" => query.OrderByDescending(user => user.Tasks.Count).ThenBy(user => user.FullName),
            _ => query.OrderBy(user => user.FullName)
        };

        var users = await query.Select(user => ToDto(user)).ToListAsync();
        return Ok(ApiResponse<IReadOnlyList<ApiUserDto>>.Ok(users));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _context.Users
            .Include(item => item.Tasks)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Пользователь не найден."));
        }

        return Ok(ApiResponse<ApiUserDto>.Ok(ToDto(user)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ApiUserCreateRequest request)
    {
        var validationError = await ValidateRequestAsync(request);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(validationError));
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            UserName = request.UserName.Trim(),
            Role = request.Role,
            RegisteredAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ApiResponse<ApiUserDto>.Ok(ToDto(user)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ApiUserUpdateRequest request)
    {
        var user = await _context.Users.Include(item => item.Tasks).FirstOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Пользователь не найден."));
        }

        var validationError = await ValidateRequestAsync(request, id);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(validationError));
        }

        user.FullName = request.FullName.Trim();
        user.UserName = request.UserName.Trim();
        user.Role = request.Role;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<ApiUserDto>.Ok(ToDto(user)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (id == User.GetUserId())
        {
            return BadRequest(ApiResponse<object>.Fail("Нельзя удалить текущего пользователя через активный токен."));
        }

        var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == id);
        if (user is null)
        {
            return NotFound(ApiResponse<object>.Fail("Пользователь не найден."));
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { deletedId = id }));
    }

    private async Task<string?> ValidateRequestAsync(ApiUserCreateRequest request, int? excludedUserId = null)
    {
        var roles = new[] { AppRoles.User, AppRoles.Manager, AppRoles.Admin };
        if (!roles.Contains(request.Role))
        {
            return "Недопустимая роль пользователя.";
        }

        var userName = request.UserName.Trim();
        if (await _context.Users.AnyAsync(user => user.UserName == userName && (!excludedUserId.HasValue || user.Id != excludedUserId.Value)))
        {
            return "Пользователь с таким логином уже существует.";
        }

        return null;
    }

    private static ApiUserDto ToDto(User user)
    {
        return new ApiUserDto(user.Id, user.FullName, user.UserName, user.Role, user.RegisteredAt, user.Tasks.Count);
    }
}
