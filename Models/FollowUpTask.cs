public class FollowUpTask
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public string Content { get; set; }
    public bool Completed {get ; set;}
    public DateTime DueDate { get; set; }
}