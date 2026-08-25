namespace Workplace.ApiService.Auth;

public class CurrentUser : ICurrentUser
{
    public string UserId { get; set; } = string.Empty;
}
