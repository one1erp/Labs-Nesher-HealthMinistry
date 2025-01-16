using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HealthMinistry.Logic
{
    internal static class Helpers
    {
        internal static DateTime GetDateTimeOrDefault(string input)
        {
            if (DateTime.TryParse(input, out DateTime date))
            {
                return date;
            }
            return DateTime.Now; // Default value - current date and time
        }

        internal static int GetNumberOrDefault(string input)
        {
            if (int.TryParse(input, out int number))
            {
                return number;
            }
            return 0; // ערך ברירת מחדל
        }

        internal static string ReplaceHtmlEntities(string input)
        {
            // Define a dictionary for HTML entity replacements
            var replacements = new Dictionary<string, string>
        {

               { "<", "&lt;" },
            { ">", "&gt;" },
            { "≤", "&le;" },
            { "≥", "&ge;" }
        };


            // Use Regex to replace based on the dictionary
            return Regex.Replace(input, string.Join("|", replacements.Keys), match => replacements[match.Value]);
        }
    }


    
}