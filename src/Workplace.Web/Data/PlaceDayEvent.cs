namespace Workplace.Web.Data;

public class PlaceDayEvent
{
    public DateOnly Date { get; set; }
    public Guid PlaceId { get; set; }
    public Guid ConnectedAccountId { get; set; }
    public required string GraphEventId { get; set; }
}
