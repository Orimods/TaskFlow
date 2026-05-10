using System.ComponentModel.DataAnnotations;

namespace TaskFlowApp.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите название категории.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Название категории должно содержать от 2 до 100 символов.")]
    [Display(Name = "Название категории")]
    public string Name { get; set; } = string.Empty;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
