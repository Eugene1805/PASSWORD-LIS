using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PASSWORD_LIS_Client.Utils
{
    public static class ProfanityFilter
    {
        private const string CensoredReplacement = "****";

        private static readonly HashSet<string> ProhibitedWords = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "fuck",
            "shit",
            "bitch",
            "asshole",
            "bastard",
            "damn",
            "crap",
            "dick",
            "pussy",
            "cock",
            "whore",
            "slut",
            "faggot",
            "nigger",
            "retard",

            "puta",
            "puto",
            "mierda",
            "pendejo",
            "pendeja",
            "cabron",
            "cabrón",
            "chingar",
            "chingada",
            "verga",
            "culero",
            "culera",
            "joto",
            "marica",
            "idiota",
            "estupido",
            "estúpido",
            "imbecil",
            "imbécil",
            "zorra",
            "coño",
            "cojones",
            "gilipollas",
            "hijueputa",
            "malparido",
            "gonorrea",
            "huevon",
            "huevón"
        };

        public static string Clean(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            string result = message;

            foreach (var word in ProhibitedWords)
            {
                var pattern = $@"\b{Regex.Escape(word)}\b";
                result = Regex.Replace(result, pattern, CensoredReplacement, RegexOptions.IgnoreCase);
            }

            return result;
        }
    }
}
