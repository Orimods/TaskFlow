using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Security;
using TaskFlowApp.Data;
using TaskFlowApp.Models;

namespace TaskFlow.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class UserController : Controller
{
    private readonly AppDbContext _context;

    public UserController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, string? role, string? sort)
    {
        var query = _context.Users
            .Include(u => u.Tasks)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(u => u.FullName.Contains(normalizedSearch) || u.UserName.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        query = sort switch
        {
            "name_desc" => query.OrderByDescending(u => u.FullName),
            "login" => query.OrderBy(u => u.UserName),
            "login_desc" => query.OrderByDescending(u => u.UserName),
            "role" => query.OrderBy(u => u.Role).ThenBy(u => u.FullName),
            "role_desc" => query.OrderByDescending(u => u.Role).ThenBy(u => u.FullName),
            "tasks" => query.OrderBy(u => u.Tasks.Count).ThenBy(u => u.FullName),
            "tasks_desc" => query.OrderByDescending(u => u.Tasks.Count).ThenBy(u => u.FullName),
            _ => query.OrderBy(u => u.FullName)
        };

        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.Sort = sort;
        ViewBag.Roles = new SelectList(new[] { AppRoles.User, AppRoles.Manager, AppRoles.Admin }, role);

        return View(await query.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var user = await _context.Users
            .Include(u => u.Tasks)
            .ThenInclude(t => t.Category)
            .FirstOrDefaultAsync(m => m.Id == id);

        return user is null ? NotFound() : View(user);
    }

    public IActionResult Create()
    {
        PopulateRoles();
        return View(new User { RegisteredAt = DateTime.UtcNow });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,FullName,UserName,Role,RegisteredAt")] User user)
    {
        if (!ModelState.IsValid)
        {
            PopulateRoles(user.Role);
            return View(user);
        }

        if (await UserNameExistsAsync(user.UserName))
        {
            ModelState.AddModelError(nameof(user.UserName), "Пользователь с таким логином уже существует.");
            PopulateRoles(user.Role);
            return View(user);
        }

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Не удалось сохранить пользователя. Проверьте данные и повторите попытку.");
            PopulateRoles(user.Role);
            return View(user);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var user = await _context.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        PopulateRoles(user.Role);
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,UserName,Role,RegisteredAt")] User user)
    {
        if (id != user.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            PopulateRoles(user.Role);
            return View(user);
        }

        if (await UserNameExistsAsync(user.UserName, user.Id))
        {
            ModelState.AddModelError(nameof(user.UserName), "Пользователь с таким логином уже существует.");
            PopulateRoles(user.Role);
            return View(user);
        }

        try
        {
            _context.Update(user);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Не удалось обновить пользователя. Проверьте данные и повторите попытку.");
            PopulateRoles(user.Role);
            return View(user);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var user = await _context.Users
            .Include(u => u.Tasks)
            .FirstOrDefaultAsync(m => m.Id == id);

        return user is null ? NotFound() : View(user);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user is not null)
        {
            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Не удалось удалить пользователя. Попробуйте позже.";
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private void PopulateRoles(string? selectedRole = null)
    {
        ViewBag.Role = new SelectList(new[] { AppRoles.User, AppRoles.Manager, AppRoles.Admin }, selectedRole);
    }

    private async Task<bool> UserNameExistsAsync(string userName, int? excludedUserId = null)
    {
        return await _context.Users.AnyAsync(u => u.UserName == userName && (!excludedUserId.HasValue || u.Id != excludedUserId.Value));
    }
}
