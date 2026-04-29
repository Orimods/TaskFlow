using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Выберите пользователя для входа.")]
    [Display(Name = "Пользователь")]
    public int? UserId { get; set; }

    public string? ReturnUrl { get; set; }
}
