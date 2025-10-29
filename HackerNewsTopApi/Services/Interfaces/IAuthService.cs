namespace HackerNewsTopApi.Services.Interfaces
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(string username, string password);
        Task<string?> LoginAsync(string username, string password);
    }
}
