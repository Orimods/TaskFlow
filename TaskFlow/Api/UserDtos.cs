using System.ComponentModel.DataAnnotations;
using TaskFlow.Security;

namespace TaskFlow.Api;

public record ApiUserDto(int Id, string FullName, string UserName, string Role, DateTime RegisteredAt, int TaskCount);

public class ApiUserCreateRequest
{
    [Required(ErrorMessage = "Введите ФИО пользователя.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "ФИО должно содержать от 3 до 100 символов.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите логин.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Логин должен содержать от 3 до 50 символов.")]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Логин может содержать только латинские буквы, цифры, точку, дефис и подчёркивание.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите роль.")]
    public string Role { get; set; } = AppRoles.User;
}

public class ApiUserUpdateRequest : ApiUserCreateRequest
{
}
