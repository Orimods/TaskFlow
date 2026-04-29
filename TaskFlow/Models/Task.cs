using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaskFlowApp.Models;

public class Task
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Название")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [Required]
    [Display(Name = "Срок выполнения")]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; }

    [Required]
    [MaxLength(50)]
    [Display(Name = "Статус")]
    public string Status { get; set; } = "New";

    [Display(Name = "Категория")]
    public int? CategoryId { get; set; }

    [Display(Name = "Категория")]
    public Category? Category { get; set; }

    [Required]
    [Display(Name = "Пользователь")]
    public int UserId { get; set; }

    [Display(Name = "Пользователь")]
    public User User { get; set; } = null!;
}
