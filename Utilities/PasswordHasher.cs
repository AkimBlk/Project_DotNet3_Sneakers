using System;
using System.Security.Cryptography;

namespace MyProjectBase.Utilities;

public static class PasswordHasher
{
    // Taille du sel aleatoire stocke avec chaque mot de passe.
    private const int SaltSize = 16;

    // Taille de la cle derivee par PBKDF2.
    private const int KeySize = 32;

    // Nombre d'iterations : augmente le cout d'une attaque par brute force.
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        // Chaque mot de passe recoit un sel different pour eviter deux hashes identiques.
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        // Format lisible : algorithme, iterations, sel, cle derivee.
        return $"PBKDF2-SHA256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        try
        {
            // Reconstruit le hash avec le sel stocke puis compare le resultat.
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
                return false;

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

            // FixedTimeEquals evite de donner des indices via le temps de comparaison.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
