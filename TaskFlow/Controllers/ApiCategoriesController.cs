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
[Route("api/categories")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class ApiCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApiCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(string? search, string? sort)
    {
        var query = _context.Categories
            .Include(category => category.Tasks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(category => category.Name.Contains(normalizedSearch));
        }

        query = sort switch
        {
            "name_desc" => query.OrderByDescending(category => category.Name),
            "tasks" => query.OrderBy(category => category.Tasks.Count).ThenBy(category => category.Name),
            "tasks_desc" => query.OrderByDescending(category => category.Tasks.Count).ThenBy(category => category.Name),
            _ => query.OrderBy(category => category.Name)
        };

        var categories = await query.Select(category => ToDto(category)).ToListAsync();
        return Ok(ApiResponse<IReadOnlyList<ApiCategoryDto>>.Ok(categories));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _context.Categories
            .Include(item => item.Tasks)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (category is null)
        {
            return NotFound(ApiResponse<object>.Fail("Категория не найдена."));
        }

        return Ok(ApiResponse<ApiCategoryDto>.Ok(ToDto(category)));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.ManagerOrAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create(ApiCategoryRequest request)
    {
        if (await _context.Categories.AnyAsync(category => category.Name == request.Name.Trim()))
        {
            return BadRequest(ApiResponse<object>.Fail("Категория с таким названием уже существует."));
        }

        var category = new Category { Name = request.Name.Trim() };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ApiResponse<ApiCategoryDto>.Ok(ToDto(category)));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.ManagerOrAdmin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ApiCategoryRequest request)
    {
        var category = await _context.Categories.Include(item => item.Tasks).FirstOrDefaultAsync(item => item.Id == id);
        if (category is null)
        {
            return NotFound(ApiResponse<object>.Fail("Категория не найдена."));
        }

        var name = request.Name.Trim();
        if (await _context.Categories.AnyAsync(item => item.Id != id && item.Name == name))
        {
            return BadRequest(ApiResponse<object>.Fail("Категория с таким названием уже существует."));
        }

        category.Name = name;
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<ApiCategoryDto>.Ok(ToDto(category)));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.ManagerOrAdmin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(item => item.Id == id);
        if (category is null)
        {
            return NotFound(ApiResponse<object>.Fail("Категория не найдена."));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok(new { deletedId = id }));
    }

    private static ApiCategoryDto ToDto(Category category)
    {
        return new ApiCategoryDto(category.Id, category.Name, category.Tasks.Count);
    }
}
