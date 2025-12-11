using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Utils
{
    public static class ValidationUtils
    {
        private const int RegexTimeoutMilliseconds = 100;
        private static readonly Regex OnlyLettersRegex = new Regex(@"[^a-zA-Z\sñÑáéíóúÁÉÍÓÚüÜ]",
            RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeoutMilliseconds));
        private static readonly Regex PasswordRequirementsRegex = 
            new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{8,15}$",
                RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeoutMilliseconds));
        private static readonly Regex EmailRegex = new Regex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$",
            RegexOptions.None, TimeSpan.FromMilliseconds(RegexTimeoutMilliseconds));
        public static bool ContainsOnlyLetters(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            return !OnlyLettersRegex.IsMatch(text);
        }
        public static bool ArePasswordRequirementsMet(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) 
            {
                return false; 
            }
            return PasswordRequirementsRegex.IsMatch(password);
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }
            return EmailRegex.IsMatch(email);
        }

        public static bool PasswordsMatch(string password, string confirmPassword)
        {
            return password == confirmPassword;
        }

        public static async Task<bool> IsNicknameInUseAsync(string nickname)
        {
            return await App.AccountManagerService.IsNicknameInUseAsync(nickname);
        }
    }
}
