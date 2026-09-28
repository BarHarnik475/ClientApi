public class DashboardSummary
{
    public int TotalClients { get; set; }
    public int TotalPolicies { get; set; }
    public IEnumerable<Policy> UpcomingRenewals { get; set; }
}
