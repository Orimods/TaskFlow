using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api;
using TaskFlow.Security;
using TaskFlowApp.Data;
using TaskEntity = TaskFlowApp.Models.Task;

namespace TaskFlow.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ApiTasksController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApiTasksController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? status, int? categoryId, int? userId, string? sort)
    {
        var query = _context.Tasks
            .Include(task => task.Category)
            .Include(task => task.User)
            .AsQueryable();

        if (!User.HasElevatedAccess())
        {
            var currentUserId = User.GetUserId();
            query = query.Where(task => task.UserId == currentUserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(task =>
                task.Title.Contains(normalizedSearch) ||
                (task.Description != null && task.Description.Contains(normalizedSearch)) ||
                (task.Category != null && task.Category.Name.Contains(normalizedSearch)) ||
                task.User.FullName.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(task => task.Status == status);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(task => task.CategoryId == categoryId);
        }

        if (userId.HasValue && User.HasElevatedAccess())
        {
            query = query.Where(task => task.UserId == userId);
        }

        query = sort switch
        {
            "title" => query.OrderBy(task => task.Title),
            "title_desc" => query.OrderByDescending(task => task.Title),
            "deadline_desc" => query.OrderByDescending(task => task.Deadline),
            "status" => query.OrderBy(task => task.Status).ThenBy(task => task.Deadline),
            "status_desc" => query.OrderByDescending(task => task.Status).ThenBy(task => task.Deadline),
            "category" => query.OrderBy(task => task.Category == null ? string.Empty : task.Category.Name),
            "category_desc" => query.OrderByDescending(task => task.Category == null ? string.Empty : task.Category.Name),
            "user" => query.OrderBy(task => task.User.FullName),
            "user_desc" => query.OrderByDescending(task => task.User.FullName),
            _ => query.OrderBy(task => task.Deadline)
        };

        var tasks = await query.Select(task => ToDto(task)).ToListAsync();
        return Ok(ApiResponse<IReadOnlyList<ApiTaskDto>>.Ok(tasks));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await LoadTaskAsync(id);
        if (task is null)
        {
            return NotFound(ApiResponse<object>.Fail("Задача не найдена."));
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        return Ok(ApiResponse<ApiTaskDto>.Ok(ToDto(task)));
    }

    [HttpPost]
    public async Task<IActionResult> Create(ApiTaskCreateRequest request)
    {
        var userId = User.HasElevatedAccess() && request.UserId.HasValue
            ? request.UserId.Value
            : User.GetUserId();

        var validationError = await ValidateRequestAsync(request, userId);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(validationError));
        }

        var task = new TaskEntity
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Deadline = request.Deadline,
            Status = request.Status,
            CategoryId = request.CategoryId,
            UserId = userId
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        var created = await LoadTaskAsync(task.Id);
        return CreatedAtAction(nameof(GetById), new { id = task.Id }, ApiResponse<ApiTaskDto>.Ok(ToDto(created!)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ApiTaskUpdateRequest request)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(item => item.Id == id);
        if (task is null)
        {
            return NotFound(ApiResponse<object>.Fail("Задача не найдена."));
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        var userId = User.HasElevatedAccess() && request.UserId.HasValue
            ? request.UserId.Value
            : User.GetUserId();

        var validationError = await ValidateRequestAsync(request, userId);
        if (validationError is not null)
        {
            return BadRequest(ApiResponse<object>.Fail(validationError));
        }

        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Deadline = request.Deadline;
        task.Status = request.Status;
        task.CategoryId = request.CategoryId;
        task.UserId = userId;

        await _context.SaveChangesAsync();

        var updated = await LoadTaskAsync(task.Id);
        return Ok(ApiResponse<ApiTaskDto>.Ok(ToDto(updated!)));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(item => item.Id == id);
        if (task is null)
        {
            return NotFound(ApiResponse<object>.Fail("Задача не найдена."));
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { deletedId = id }));
    }

    private async Task<TaskEntity?> LoadTaskAsync(int id)
    {
        return await _context.Tasks
            .Include(task => task.Category)
            .Include(task => task.User)
            .FirstOrDefaultAsync(task => task.Id == id);
    }

    private bool CanAccessTask(TaskEntity task)
    {
        return User.HasElevatedAccess() || task.UserId == User.GetUserId();
    }

    private async Task<string?> ValidateRequestAsync(ApiTaskCreateRequest request, int userId)
    {
        var allowedStatuses = new[] { "New", "In Progress", "Done" };
        if (!allowedStatuses.Contains(request.Status))
        {
            return "Недопустимый статус задачи.";
        }

        if (request.Status != "Done" && request.Deadline.Date < DateTime.Today)
        {
            return "Срок активной задачи не может быть раньше текущей даты.";
        }

        if (request.CategoryId.HasValue && !await _context.Categories.AnyAsync(category => category.Id == request.CategoryId.Value))
        {
            return "Выбранная категория не найдена.";
        }

        if (!await _context.Users.AnyAsync(user => user.Id == userId))
        {
            return "Выбранный пользователь не найден.";
        }

        return null;
    }

    private static ApiTaskDto ToDto(TaskEntity task)
    {
        return new ApiTaskDto(
            task.Id,
            task.Title,
            task.Description,
            task.Deadline,
            task.Status,
            task.CategoryId,
            task.Category?.Name,
            task.UserId,
            task.User.FullName);
    }
}
