using MySqlConnector;
using Dapper;

public class PolicyService
{
    private readonly string _connectionString;

    public PolicyService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<Policy>?> GetPoliciesByClientId(int clientId, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);

        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return null;

        return await connection.QueryAsync<Policy>(
            "SELECT p.id AS Id, p.client_id AS ClientId, c.name AS Provider, p.policy_type AS PolicyType, p.premium AS Premium, p.start_date AS StartDate, p.renewal_date AS RenewalDate " +
            "FROM policies p JOIN carriers c ON p.carrier_id = c.id " +
            "WHERE p.client_id = @ClientId",
            new { ClientId = clientId });
    }

    public async Task<IEnumerable<Policy>> GetUpcomingRenewals(int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QueryAsync<Policy>(
            "SELECT p.id AS Id, p.client_id AS ClientId, car.name AS Provider, p.policy_type AS PolicyType, p.premium AS Premium, p.start_date AS StartDate, p.renewal_date AS RenewalDate " +
            "FROM policies p JOIN clients c ON p.client_id = c.id JOIN carriers car ON p.carrier_id = car.id " +
            "WHERE p.renewal_date BETWEEN CURDATE() AND DATE_ADD(CURDATE(), INTERVAL 30 DAY) AND c.user_id = @UserId",
            new { UserId = userId });
    }

    public async Task<int> GetTotalPolicyCount(int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM policies p JOIN clients c ON p.client_id = c.id WHERE c.user_id = @UserId",
            new { UserId = userId });
    }

    public async Task<(CreatePolicyResult Result, int? Id)> CreatePolicy(int clientId, Policy newPolicy, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);

        if (!await ClientService.ClientBelongsToUser(connection, clientId, userId)) return (CreatePolicyResult.ClientNotFound, null);

        var carrierId = await GetCarrierIdByName(connection, newPolicy.Provider);
        if (carrierId == null) return (CreatePolicyResult.CarrierNotFound, null);

        await connection.ExecuteAsync(
            "INSERT INTO policies (carrier_id, policy_type, premium, start_date, renewal_date, client_id) VALUES (@CarrierId, @PolicyType, @Premium, @StartDate, @RenewalDate, @ClientId)",
            new { CarrierId = carrierId, newPolicy.PolicyType, newPolicy.Premium, newPolicy.StartDate, newPolicy.RenewalDate, ClientId = clientId });
        var newId = await connection.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
        return (CreatePolicyResult.Success, newId);
    }

    public async Task<EditPolicyResult> EditPolicy(int id, Policy updatedPolicy, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);

        var carrierId = await GetCarrierIdByName(connection, updatedPolicy.Provider);
        if (carrierId == null) return EditPolicyResult.CarrierNotFound;

        var rowsAffected = await connection.ExecuteAsync(
            "UPDATE policies SET carrier_id = @CarrierId, policy_type = @PolicyType, premium = @Premium, start_date = @StartDate, renewal_date = @RenewalDate " +
            "WHERE id = @Id AND client_id IN (SELECT id FROM clients WHERE user_id = @UserId)",
            new { CarrierId = carrierId, updatedPolicy.PolicyType, updatedPolicy.Premium, updatedPolicy.StartDate, updatedPolicy.RenewalDate, Id = id, UserId = userId });

        return rowsAffected == 0 ? EditPolicyResult.NotFound : EditPolicyResult.Success;
    }

    public async Task<int> DeletePolicy(int id, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.ExecuteAsync(
            "DELETE FROM policies WHERE id = @Id AND client_id IN (SELECT id FROM clients WHERE user_id = @UserId)",
            new { Id = id, UserId = userId });
    }

    

    private static async Task<int?> GetCarrierIdByName(MySqlConnection connection, string carrierName)
    {
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM carriers WHERE name = @Name",
            new { Name = carrierName });
    }

    public async Task<IEnumerable<Carrier>> GetAllCarriers()
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QueryAsync<Carrier>(
            "SELECT id AS Id, name AS Name, contact_phone AS ContactPhone, contact_email AS ContactEmail FROM carriers");
    }
}

public enum CreatePolicyResult
{
    Success,
    ClientNotFound,
    CarrierNotFound
}

public enum EditPolicyResult
{
    Success,
    NotFound,
    CarrierNotFound
}