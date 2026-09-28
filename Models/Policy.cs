public class Policy
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Provider { get; set; }
    public string PolicyType { get; set; }
    public decimal Premium { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime RenewalDate { get; set; }
}