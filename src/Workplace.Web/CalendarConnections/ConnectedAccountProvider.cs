namespace Workplace.Web.CalendarConnections;

// Must stay in sync with Workplace.ApiService.Data.ConnectedAccountProvider —
// Web and ApiService only share a JSON contract, not an assembly.
public enum ConnectedAccountProvider
{
    MicrosoftGraph,
    GoogleCalendar
}
