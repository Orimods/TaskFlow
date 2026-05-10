using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Api;

public record ApiLoginRequest(
    [Required(ErrorMessage = "Укажите пользователя.")]
    int UserId);

public record ApiAuthUserDto(int Id, string FullName, string UserName, string Role);

public record ApiLoginResponse(string Token, DateTime ExpiresAt, ApiAuthUserDto User);
