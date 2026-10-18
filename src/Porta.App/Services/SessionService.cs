using Porta.Models;

namespace Porta.Services;

/// <summary>Holds the logged-in user and enforces role-based authorization (FR-02).</summary>
public sealed class SessionService
{
    public User? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;
    public bool CanEdit => CurrentUser?.Role == UserRole.Investor;

    public event EventHandler? SessionChanged;

    public void Start(User user)
    {
        CurrentUser = user;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void End()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Second line of defence behind the disabled UI buttons.</summary>
    public void EnsureCanEdit()
    {
        if (CurrentUser is null)
            throw new UnauthorizedAccessException("Sesi telah berakhir. Silakan login kembali.");
        if (!CanEdit)
            throw new UnauthorizedAccessException("Akses ditolak: peran Reviewer hanya memiliki akses baca (read-only).");
    }
}
