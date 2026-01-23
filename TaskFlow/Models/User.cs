using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskFlowApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        public ICollection<Task> Tasks { get; set; } = new List<Task>();  // Связь с задачами
    }
}