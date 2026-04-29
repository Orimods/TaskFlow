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

    public async Task<IActionResult> Index()
    {
        return View(await _context.Users.Include(u => u.Tasks).OrderBy(u => u.FullName).ToListAsync());
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

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
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

        _context.Update(user);
        await _context.SaveChangesAsync();
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
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private void PopulateRoles(string? selectedRole = null)
    {
        ViewBag.Role = new SelectList(new[] { AppRoles.User, AppRoles.Manager, AppRoles.Admin }, selectedRole);
    }
}
