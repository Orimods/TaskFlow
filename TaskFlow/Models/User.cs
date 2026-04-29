using System.ComponentModel.DataAnnotations;
using TaskFlow.Security;

namespace TaskFlowApp.Models;

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Display(Name = "ФИО")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Display(Name = "Логин")]
    public string UserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Display(Name = "Роль")]
    public string Role { get; set; } = AppRoles.User;

    [Required]
    [Display(Name = "Дата регистрации")]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
