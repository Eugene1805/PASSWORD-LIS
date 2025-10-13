using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PASSWORD_LIS_Client.Utils
{
    public static class ValidationUtils
    {
        private static readonly Regex OnlyLettersRegex = new Regex(@"[^a-zA-Z\sñÑáéíóúÁÉÍÓÚüÜ]");

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
            string passwordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*[0-9])(?=.*[^a-zA-Z0-9]).{8,15}$";
            return Regex.IsMatch(password, passwordRegex);
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            string emailRegex = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
            return Regex.IsMatch(email, emailRegex);
        }

        public static bool PasswordsMatch(string password, string confirmPassword)
        {
            return password == confirmPassword;
        }
    }
}
