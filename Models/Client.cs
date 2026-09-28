public class Client
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public ClientStatus Status { get; set; }

    

    public Client() { }

    public Client(string name, string phone)
    {
        Name = name;
        Phone = phone;
    }
}

public enum ClientStatus
{
    Lead,
    Quoted,
    Bound,
    Lost
}