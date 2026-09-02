namespace Workplace.Web.Data;

public class AppUser
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
