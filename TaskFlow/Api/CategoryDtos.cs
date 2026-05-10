using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api;

public record ApiCategoryDto(int Id, string Name, int TaskCount);

public class ApiCategoryRequest
{
    [Required(ErrorMessage = "Введите название категории.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Название категории должно содержать от 2 до 100 символов.")]
    public string Name { get; set; } = string.Empty;
}
