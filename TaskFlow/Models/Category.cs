using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaskFlowApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<Task> Tasks { get; set; } = new List<Task>();  // Связь с задачами
    }
}