using System.ComponentModel.DataAnnotations;

namespace TaskFlowApp.Models;

public class Task : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите название задачи.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название должно содержать от 3 до 200 символов.")]
    [Display(Name = "Название")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов.")]
    [Display(Name = "Описание")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Укажите срок выполнения.")]
    [Display(Name = "Срок выполнения")]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; }

    [Required(ErrorMessage = "Выберите статус задачи.")]
    [StringLength(50, ErrorMessage = "Статус не должен превышать 50 символов.")]
    [Display(Name = "Статус")]
    public string Status { get; set; } = "New";

    [Display(Name = "Категория")]
    public int? CategoryId { get; set; }

    [Display(Name = "Категория")]
    public Category? Category { get; set; }

    [Required(ErrorMessage = "Выберите пользователя.")]
    [Display(Name = "Пользователь")]
    public int UserId { get; set; }

    [Display(Name = "Пользователь")]
    public User User { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var allowedStatuses = new[] { "New", "In Progress", "Done" };
        if (!allowedStatuses.Contains(Status))
        {
            yield return new ValidationResult("Недопустимый статус задачи.", new[] { nameof(Status) });
        }

        if (Status != "Done" && Deadline.Date < DateTime.Today)
        {
            yield return new ValidationResult("Срок активной задачи не может быть раньше текущей даты.", new[] { nameof(Deadline) });
        }
    }
}
