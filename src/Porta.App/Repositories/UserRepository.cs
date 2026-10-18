using Microsoft.Data.SqlClient;
using Porta.Data;
using Porta.Models;

namespace Porta.Repositories;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
}

public sealed class UserRepository(DatabaseHelper db) : IUserRepository
{
    public async Task<User?> GetByUsernameAsync(string username)
    {
        const string sql = """
            SELECT UserId, Username, PasswordHash, FullName, Role, IsActive
            FROM dbo.Users WHERE Username = @username
            """;
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add("@username", System.Data.SqlDbType.NVarChar, 50).Value = username;
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new User
        {
            UserId = r.GetInt32(0),
            Username = r.GetString(1),
            PasswordHash = r.GetString(2),
            FullName = r.GetString(3),
            Role = Enum.Parse<UserRole>(r.GetString(4)),
            IsActive = r.GetBoolean(5)
        };
    }
}
