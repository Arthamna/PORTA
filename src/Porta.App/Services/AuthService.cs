using Porta.Repositories;

namespace Porta.Services;

/// <summary>Login/logout with BCrypt password verification (FR-01, NFR-04).</summary>
public sealed class AuthService(IUserRepository users, SessionService session)
{
    public async Task<(bool Success, string? Error)> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return (false, "Username dan password wajib diisi.");

        var user = await users.GetByUsernameAsync(username.Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "Username atau password salah.");
        if (!user.IsActive)
            return (false, "Akun tidak aktif. Hubungi administrator.");

        session.Start(user);
        return (true, null);
    }

    public void Logout() => session.End();

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, 11);
}
