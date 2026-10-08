using System.Security.Cryptography;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Web.Identity
{
    // Prepoznaje stari ASP.NET Identity v2 PBKDF2 format i re-hešuje pri prvom uspešnom loginu.
    // Postojeći korisnici tako nastavljaju da rade starim lozinkama bez reseta.
    public class LegacyCompatPasswordHasher : IPasswordHasher<ApplicationUser>
    {
        private readonly PasswordHasher<ApplicationUser> _core = new();

        public string HashPassword(ApplicationUser user, string password)
            => _core.HashPassword(user, password); // novi upisi = novi (Core) format

        public PasswordVerificationResult VerifyHashedPassword(
            ApplicationUser user, string hashedPassword, string providedPassword)
        {
            var result = _core.VerifyHashedPassword(user, hashedPassword, providedPassword);
            if (result != PasswordVerificationResult.Failed)
                return result;

            if (VerifyLegacyV2(hashedPassword, providedPassword))
                return PasswordVerificationResult.SuccessRehashNeeded; // signal da SignInManager re-hešuje

            return PasswordVerificationResult.Failed;
        }

        // Reprodukcija Identity v2 Crypto.VerifyHashedPassword (PBKDF2-HMAC-SHA1, 1000 iter, 16B salt, 32B subkey)
        private static bool VerifyLegacyV2(string hashedPassword, string password)
        {
            byte[] hashedBytes;
            try { hashedBytes = Convert.FromBase64String(hashedPassword); } catch { return false; }
            if (hashedBytes.Length != 49 || hashedBytes[0] != 0x00) return false;

            var salt = new byte[16];
            Buffer.BlockCopy(hashedBytes, 1, salt, 0, 16);
            var storedSubkey = new byte[32];
            Buffer.BlockCopy(hashedBytes, 17, storedSubkey, 0, 32);

            var actualSubkey = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1000, HashAlgorithmName.SHA1, 32);
            return CryptographicOperations.FixedTimeEquals(actualSubkey, storedSubkey);
        }
    }
}
