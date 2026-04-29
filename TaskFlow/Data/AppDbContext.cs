using Microsoft.EntityFrameworkCore;
using TaskFlow.Security;
using TaskFlowApp.Models;
using TaskEntity = TaskFlowApp.Models.Task;

namespace TaskFlowApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.UserName)
            .IsUnique();

        modelBuilder.Entity<TaskEntity>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tasks)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TaskEntity>()
            .HasOne(t => t.Category)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        var registeredAt = new DateTime(2026, 1, 24, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FullName = "Администратор системы", UserName = "admin", Role = AppRoles.Admin, RegisteredAt = registeredAt },
            new User { Id = 2, FullName = "Менеджер проектов", UserName = "manager", Role = AppRoles.Manager, RegisteredAt = registeredAt },
            new User { Id = 3, FullName = "Кофтоногов Вадим", UserName = "ivan", Role = AppRoles.User, RegisteredAt = registeredAt },
            new User { Id = 4, FullName = "Виктор Пупкин", UserName = "maria", Role = AppRoles.User, RegisteredAt = registeredAt }
        );

        modelBuilder.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Учёба" },
            new Category { Id = 2, Name = "Работа" },
            new Category { Id = 3, Name = "Личное" }
        );

        modelBuilder.Entity<TaskEntity>().HasData(
            new TaskEntity
            {
                Id = 1,
                Title = "Подготовить план проекта",
                Description = "Собрать требования, определить этапы работы и назначить ответственных.",
                Deadline = new DateTime(2026, 4, 25),
                Status = "In Progress",
                CategoryId = 1,
                UserId = 3
            },
            new TaskEntity
            {
                Id = 2,
                Title = "Проверить задачи команды",
                Description = "Провести ревью задач и уточнить сроки выполнения.",
                Deadline = new DateTime(2026, 4, 26),
                Status = "New",
                CategoryId = 2,
                UserId = 2
            },
            new TaskEntity
            {
                Id = 3,
                Title = "Настроить резервное копирование",
                Description = "Подготовить базовый регламент резервного копирования базы данных.",
                Deadline = new DateTime(2026, 4, 30),
                Status = "Done",
                CategoryId = 2,
                UserId = 1
            }
        );
    }
}
