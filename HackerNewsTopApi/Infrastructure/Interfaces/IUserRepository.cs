using HackerNewsTopApi.Domain;

namespace HackerNewsTopApi.Infrastructure.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByUsernameAsync(string username);
        Task AddUserAsync(User user);
    }
}
