using PMS.Domain.Entities;

namespace PMS.Application.Common.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
