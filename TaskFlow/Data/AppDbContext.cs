using Microsoft.EntityFrameworkCore;
using TaskFlow.Models;
using TaskFlowApp.Models;  

namespace TaskFlowApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<TaskFlowApp.Models.Task> Tasks { get; set; }  // Используем полное имя класса Task
        public DbSet<Category> Categories { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройка связи: одна задача принадлежит одному пользователю
            modelBuilder.Entity<TaskFlowApp.Models.Task>()  // Используем полное имя
                .HasOne(t => t.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка связи: каждая задача имеет одну категорию
            modelBuilder.Entity<TaskFlowApp.Models.Task>()  // Используем полное имя
                .HasOne(t => t.Category)
                .WithMany(c => c.Tasks)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}