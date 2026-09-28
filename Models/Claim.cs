public class InsuranceClaim
{
    public int Id { get; set; }
    public int PolicyId { get; set; }
    public ClaimStatus Status { get; set; }
    public DateTime ClaimDate { get; set; }
    public string Description { get; set; }
    public decimal Amount { get; set; }
}

public enum ClaimStatus
{
    Filed,
    UnderReview,
    Approved,
    Denied,
    Paid
}