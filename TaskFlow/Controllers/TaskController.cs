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

    public async Task<IActionResult> Index(string? search, string? status, int? categoryId, int? userId, string? sort)
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

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(t =>
                t.Title.Contains(normalizedSearch) ||
                (t.Description != null && t.Description.Contains(normalizedSearch)) ||
                (t.Category != null && t.Category.Name.Contains(normalizedSearch)) ||
                t.User.FullName.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId);
        }

        if (userId.HasValue && User.HasElevatedAccess())
        {
            query = query.Where(t => t.UserId == userId);
        }

        query = sort switch
        {
            "title" => query.OrderBy(t => t.Title),
            "title_desc" => query.OrderByDescending(t => t.Title),
            "deadline" => query.OrderBy(t => t.Deadline),
            "deadline_desc" => query.OrderByDescending(t => t.Deadline),
            "status" => query.OrderBy(t => t.Status).ThenBy(t => t.Deadline),
            "status_desc" => query.OrderByDescending(t => t.Status).ThenBy(t => t.Deadline),
            "category" => query.OrderBy(t => t.Category == null ? string.Empty : t.Category.Name).ThenBy(t => t.Deadline),
            "category_desc" => query.OrderByDescending(t => t.Category == null ? string.Empty : t.Category.Name).ThenBy(t => t.Deadline),
            "user" => query.OrderBy(t => t.User.FullName).ThenBy(t => t.Deadline),
            "user_desc" => query.OrderByDescending(t => t.User.FullName).ThenBy(t => t.Deadline),
            _ => query.OrderBy(t => t.Deadline)
        };

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.CategoryId = categoryId;
        ViewBag.UserId = userId;
        ViewBag.Sort = sort;
        await PopulateFilterSelectionsAsync(categoryId, userId);

        return View(await query.ToListAsync());
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

        await ValidateTaskReferencesAsync(task);
        ValidateTaskBusinessRules(task);

        if (ModelState.IsValid)
        {
            try
            {
                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Не удалось сохранить задачу. Проверьте данные и повторите попытку.");
            }
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

        input.UserId = User.HasElevatedAccess() ? input.UserId : User.GetUserId();
        await ValidateTaskReferencesAsync(input);
        ValidateTaskBusinessRules(input);

        if (!ModelState.IsValid)
        {
            await PopulateSelectionsAsync(input);
            return View(input);
        }

        task.Title = input.Title;
        task.Description = input.Description;
        task.Deadline = input.Deadline;
        task.Status = input.Status;
        task.CategoryId = input.CategoryId;
        task.UserId = input.UserId;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Не удалось обновить задачу. Проверьте данные и повторите попытку.");
            await PopulateSelectionsAsync(input);
            return View(input);
        }

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

        try
        {
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Не удалось удалить задачу. Попробуйте позже.";
        }

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

    private async Task PopulateFilterSelectionsAsync(int? selectedCategoryId, int? selectedUserId)
    {
        ViewBag.CanFilterUsers = User.HasElevatedAccess();
        ViewBag.Statuses = new SelectList(new[] { "New", "In Progress", "Done" }, ViewBag.Status);
        ViewBag.Categories = new SelectList(
            await _context.Categories.OrderBy(c => c.Name).ToListAsync(),
            "Id",
            "Name",
            selectedCategoryId);
        ViewBag.Users = new SelectList(
            await _context.Users.OrderBy(u => u.FullName).ToListAsync(),
            "Id",
            "FullName",
            selectedUserId);
    }

    private async Task ValidateTaskReferencesAsync(TaskEntity task)
    {
        if (task.CategoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == task.CategoryId.Value))
        {
            ModelState.AddModelError(nameof(task.CategoryId), "Выбранная категория не найдена.");
        }

        if (!await _context.Users.AnyAsync(u => u.Id == task.UserId))
        {
            ModelState.AddModelError(nameof(task.UserId), "Выбранный пользователь не найден.");
        }
    }

    private void ValidateTaskBusinessRules(TaskEntity task)
    {
        if (task.Status != "Done" && task.Deadline.Date < DateTime.Today)
        {
            ModelState.AddModelError(nameof(task.Deadline), "Срок активной задачи не может быть раньше текущей даты.");
        }
    }
}
