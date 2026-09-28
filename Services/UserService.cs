using MySqlConnector;
using Dapper;
using BCrypt.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class UserService
{
    private readonly string _connectionString;

    public UserService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task RegisterUser(string username, string password)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        using var connection = new MySqlConnection(_connectionString);
        await connection.ExecuteAsync(
            "INSERT INTO users (username, password_hash) VALUES (@Username, @PasswordHash)",
            new { Username = username, PasswordHash = passwordHash });
    }

    public async Task<User?> ValidateLogin(string username, string password)
    {
        using var connection = new MySqlConnection(_connectionString);
        var user = await connection.QuerySingleOrDefaultAsync<User>(
            "SELECT id AS Id, username AS Username, password_hash AS PasswordHash FROM users WHERE username = @Username",
            new { Username = username });
        if (user == null) return null;
        bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        if (!isValid) return null;
        return user;
    }

    public const string SecretKey = "this-is-a-temporary-secret-key-for-learning-purposes-only";

    public string GenerateToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}