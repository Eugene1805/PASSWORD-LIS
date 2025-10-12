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
    }
}
