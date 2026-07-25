using PMS.Application.Common.Interfaces;

namespace PMS.Infrastructure.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 11;   // ~100ms/hash trên phần cứng hiện tại

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch (BCrypt.Net.SaltParseException) { return false; }   // hash hỏng trong DB
    }
}