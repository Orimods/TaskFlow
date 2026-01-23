using System;
using System.ComponentModel.DataAnnotations;
using TaskFlow.Models;

namespace TaskFlowApp.Models
{
    public class Task
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public DateTime Deadline { get; set; }

        public string Status { get; set; } = "New";

        [Required]
        public int CategoryId { get; set; }
        public Category Category { get; set; } = new Category();  // Связь с категорией

        [Required]
        public int UserId { get; set; }
        public User User { get; set; } = new User(); // Связь с пользователем
    }
}
