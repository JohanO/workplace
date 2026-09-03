namespace Workplace.Web.Data;

public class CalendarColorSetting
{
    public required string CalendarKey { get; set; }
    public required string Color { get; set; }
    public string? DisplayName { get; set; }
}
