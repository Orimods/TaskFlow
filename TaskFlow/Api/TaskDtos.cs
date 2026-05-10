using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api;

public record ApiTaskDto(
    int Id,
    string Title,
    string? Description,
    DateTime Deadline,
    string Status,
    int? CategoryId,
    string? CategoryName,
    int UserId,
    string UserFullName);

public class ApiTaskCreateRequest
{
    [Required(ErrorMessage = "Введите название задачи.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Название должно содержать от 3 до 200 символов.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Описание не должно превышать 1000 символов.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Укажите срок выполнения.")]
    public DateTime Deadline { get; set; }

    [Required(ErrorMessage = "Выберите статус задачи.")]
    public string Status { get; set; } = "New";

    public int? CategoryId { get; set; }

    public int? UserId { get; set; }
}

public class ApiTaskUpdateRequest : ApiTaskCreateRequest
{
}
