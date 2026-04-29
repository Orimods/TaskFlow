using System.ComponentModel.DataAnnotations;

namespace TaskFlowApp.Models;

public class Category
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "Название категории")]
    public string Name { get; set; } = string.Empty;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
