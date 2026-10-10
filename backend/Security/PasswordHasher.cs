namespace Phonebook.Api.Security;

/// <summary>
/// BCrypt password hashing. Compatible with hashes created by the Java
/// Spring Security BCryptPasswordEncoder and the Python bcrypt library.
/// </summary>
public static class PasswordHasher
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
