using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Security;
using TaskFlowApp.Data;
using TaskFlowApp.Models;

namespace TaskFlow.Controllers;

[Authorize]
public class CategoryController : Controller
{
    private readonly AppDbContext _context;

    public CategoryController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Categories.Include(c => c.Tasks).OrderBy(c => c.Name).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var category = await _context.Categories
            .Include(c => c.Tasks)
            .ThenInclude(t => t.User)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (category is null)
        {
            return NotFound();
        }

        return View(category);
    }

    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name")] Category category)
    {
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var category = await _context.Categories.FindAsync(id);
        return category is null ? NotFound() : View(category);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] Category category)
    {
        if (id != category.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(category);
        }

        _context.Update(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var category = await _context.Categories
            .Include(c => c.Tasks)
            .FirstOrDefaultAsync(m => m.Id == id);

        return category is null ? NotFound() : View(category);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Roles = AppRoles.ManagerOrAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category is not null)
        {
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}
