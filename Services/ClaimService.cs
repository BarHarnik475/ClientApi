using MySqlConnector;
using Dapper;
using System.ComponentModel.Design;
public class ClaimService
{
    private readonly string _connectionString;

    public ClaimService(string connectionString)
    {
        _connectionString = connectionString;
    }

    private static async Task<bool> PolicyBelongsToUser(MySqlConnection connection, int policyId, int userId)
    {
        var ownedPolicyId = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT p.id FROM policies p JOIN clients c ON p.client_id = c.id WHERE p.id = @PolicyId AND c.user_id = @UserId",
            new { PolicyId = policyId, UserId = userId });
        return ownedPolicyId != null;
    }
    public async Task<IEnumerable<InsuranceClaim>> GetClaimsByPolicyId(int id, int userId)
    {
        using var connection = new MySqlConnection(_connectionString);
        return await connection.QueryAsync<InsuranceClaim>(
            "SELECT id AS Id, policy_id AS PolicyId, claim_date AS ClaimDate, description AS Description, status AS Status, amount AS Amount FROM claims " +
            "WHERE policy_id In(SELECT p.id FROM policies p JOIN clients c ON p.client_id = c.id WHERE c.user_id = @UserId) AND policy_id = @Id",
        new { UserId = userId, Id = id });
    }

    public async Task<(CreateClaimResult result,InsuranceClaim? id)> CreateClaim(int userId,InsuranceClaim claim,int policyId)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        if (!await PolicyBelongsToUser(connection, policyId, userId)) return (CreateClaimResult.PolicyNotFound, null);
        await connection.ExecuteAsync("INSERT INTO claims(policy_id,claim_date,description,amount) VALUES(@PolicyId,@ClaimDate,@Description,@Amount)",
        new{PolicyId = policyId,claim.ClaimDate,claim.Description,claim.Amount});
        var newId = await connection.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
        var createdClaim = await connection.QuerySingleAsync<InsuranceClaim>("SELECT id AS Id,policy_id AS PolicyId,claim_date AS ClaimDate,description AS Description, " +
        "status AS Status,amount AS Amount FROM claims WHERE id = @Id",new{Id = newId});
        return (CreateClaimResult.Success,createdClaim);
    }



}


public enum CreateClaimResult
{
    Success,
    PolicyNotFound
}

