using System.ComponentModel.DataAnnotations;
using TaskFlow.Security;

namespace TaskFlowApp.Models;

public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите ФИО пользователя.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "ФИО должно содержать от 3 до 100 символов.")]
    [Display(Name = "ФИО")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите логин.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Логин должен содержать от 3 до 50 символов.")]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Логин может содержать только латинские буквы, цифры, точку, дефис и подчёркивание.")]
    [Display(Name = "Логин")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите роль.")]
    [StringLength(20, ErrorMessage = "Роль не должна превышать 20 символов.")]
    [Display(Name = "Роль")]
    public string Role { get; set; } = AppRoles.User;

    [Required]
    [Display(Name = "Дата регистрации")]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
