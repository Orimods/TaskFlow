using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Security;
using TaskFlowApp.Data;
using TaskEntity = TaskFlowApp.Models.Task;

namespace TaskFlow.Controllers;

[Authorize]
public class TaskController : Controller
{
    private readonly AppDbContext _context;

    public TaskController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var query = _context.Tasks
            .Include(t => t.Category)
            .Include(t => t.User)
            .AsQueryable();

        if (!User.HasElevatedAccess())
        {
            var currentUserId = User.GetUserId();
            query = query.Where(t => t.UserId == currentUserId);
        }

        return View(await query.OrderBy(t => t.Deadline).ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await LoadTaskAsync(id.Value);
        if (task is null)
        {
            return NotFound();
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        return View(task);
    }

    public async Task<IActionResult> Create()
    {
        var task = new TaskEntity
        {
            Deadline = DateTime.Today.AddDays(1),
            Status = "New",
            UserId = User.GetUserId()
        };

        await PopulateSelectionsAsync(task);
        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Title,Description,Deadline,Status,CategoryId,UserId")] TaskEntity task)
    {
        if (!User.HasElevatedAccess())
        {
            task.UserId = User.GetUserId();
        }

        if (ModelState.IsValid)
        {
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await PopulateSelectionsAsync(task);
        return View(task);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await LoadTaskAsync(id.Value);
        if (task is null)
        {
            return NotFound();
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        await PopulateSelectionsAsync(task);
        return View(task);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Deadline,Status,CategoryId,UserId")] TaskEntity input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return NotFound();
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            input.UserId = User.HasElevatedAccess() ? input.UserId : User.GetUserId();
            await PopulateSelectionsAsync(input);
            return View(input);
        }

        task.Title = input.Title;
        task.Description = input.Description;
        task.Deadline = input.Deadline;
        task.Status = input.Status;
        task.CategoryId = input.CategoryId;
        task.UserId = User.HasElevatedAccess() ? input.UserId : User.GetUserId();

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var task = await LoadTaskAsync(id.Value);
        if (task is null)
        {
            return NotFound();
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        return View(task);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (task is null)
        {
            return RedirectToAction(nameof(Index));
        }

        if (!CanAccessTask(task))
        {
            return Forbid();
        }

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task<TaskEntity?> LoadTaskAsync(int id)
    {
        return await _context.Tasks
            .Include(t => t.Category)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    private bool CanAccessTask(TaskEntity task)
    {
        return User.HasElevatedAccess() || task.UserId == User.GetUserId();
    }

    private async Task PopulateSelectionsAsync(TaskEntity? task = null)
    {
        ViewBag.CanAssignUsers = User.HasElevatedAccess();
        ViewBag.Statuses = new SelectList(new[] { "New", "In Progress", "Done" }, task?.Status);
        ViewBag.CategoryId = new SelectList(
            await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
            "Id",
            "Name",
            task?.CategoryId);
        ViewBag.UserId = new SelectList(
            await _context.Users.OrderBy(u => u.FullName).ToListAsync(),
            "Id",
            "FullName",
            task?.UserId ?? User.GetUserId());
    }
}
