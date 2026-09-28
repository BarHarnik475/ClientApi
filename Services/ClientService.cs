using MySqlConnector;
using Dapper;

public class ClientService
{
    private readonly string _connectionString;

    public ClientService(string connectionString)
    {
        _connectionString = connectionString;
    }


    public static async Task<bool> ClientBelongsToUser(MySqlConnection connection, int clientId, int userId)
    {
        var ownedClientId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM clients WHERE id = @ClientId AND user_id = @UserId",
            new { ClientId = clientId, UserId = userId });
        return ownedClientId != null;
    }

    public async Task<IEnumerable<Client>> GetAllClients(int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QueryAsync<Client>(
            "SELECT id AS Id, name AS Name, phone AS Phone, status AS Status FROM clients WHERE user_id = @UserId",
            new { UserId = userId });
    }

    public async Task<Client?> GetClientById(int id, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Client>(
            "SELECT id AS Id, name AS Name, phone AS Phone, status AS Status FROM clients WHERE id = @Id AND user_id = @UserId",
            new { Id = id, UserId = userId });
    }

    public async Task<int> CreateClient(Client newClient, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "INSERT INTO clients (name, phone, user_id) VALUES (@Name, @Phone, @UserId)",
            new { newClient.Name, newClient.Phone, UserId = userId });
        return await connection.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
    }

    public async Task<int> UpdateClient(int id, Client updatedClient, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.ExecuteAsync(
            "UPDATE clients SET name = @Name, phone = @Phone WHERE id = @Id AND user_id = @UserId",
            new { updatedClient.Name, updatedClient.Phone, Id = id, UserId = userId });
    }

    public async Task<int> UpdateClientStatus(int id, ClientStatus newStatus, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.ExecuteAsync(
            "UPDATE clients SET status = @Status WHERE id = @Id AND user_id = @UserId",
            new { Status = newStatus.ToString(), Id = id, UserId = userId });
    }

    public async Task<DeleteClientResult> DeleteClient(int id, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);

        try
        {
            var rowsAffected = await connection.ExecuteAsync(
                "DELETE FROM clients WHERE id = @Id AND user_id = @UserId", new { Id = id, UserId = userId });

            return rowsAffected == 0 ? DeleteClientResult.NotFound : DeleteClientResult.Success;
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.RowIsReferenced2)
        {
            return DeleteClientResult.HasDependentPolicies;
        }
    }

    public async Task<IEnumerable<Client>> SearchClientsByName(string searchTerm, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QueryAsync<Client>(
            "SELECT id AS Id, name AS Name, phone AS Phone, status AS Status FROM clients WHERE name LIKE @SearchPattern AND user_id = @UserId",
            new { SearchPattern = $"%{searchTerm}%", UserId = userId });
    }

    public async Task<int> GetTotalClientCount(int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM clients WHERE user_id = @UserId",
            new { UserId = userId });
    }
}



public enum DeleteClientResult
{
    Success,
    NotFound,
    HasDependentPolicies
}