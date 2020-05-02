using System;
using System.Runtime.InteropServices;
using System.Linq;

namespace Phantasma.Spook.Utils
{

    public static class SpookUtils
    {
        public static string LocateExec(String filename)
        {
            String path = Environment.GetEnvironmentVariable("PATH");
            string seperator1;
            string seperator2;

            var os = GetOperatingSystem();
            if (os == OSPlatform.OSX || os == OSPlatform.Linux)
            {
                seperator1 = ":";
                seperator2 = "/";
            }
            else
            {
                seperator1 = ";";
                seperator2 = "\\";
            }

            String[] folders = path.Split(seperator1);
            foreach (String folder in folders)
            {
                if (System.IO.File.Exists(folder + filename))
                {
                    return folder + filename;
                }
                else if (System.IO.File.Exists(folder + seperator2 + filename))
                {
                    return folder + seperator2 + filename;
                }
            }

            return String.Empty;
        }

        public static OSPlatform GetOperatingSystem()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return OSPlatform.OSX;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return OSPlatform.Linux;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return OSPlatform.Windows;
            }

            throw new Exception("Cannot determine operating system!");
        }

        // thx to Vince Panuccio
        // https://stackoverflow.com/questions/4580397/json-formatter-in-c/24782322#24782322
        public static string FormatJson(string json, string indent = "    ")
        {
            var indentation = 0;
            var quoteCount = 0;
            var escapeCount = 0;

            var result =
                from ch in json ?? string.Empty
                let escaped = (ch == '\\' ? escapeCount++ : escapeCount > 0 ? escapeCount-- : escapeCount) > 0
                let quotes = ch == '"' && !escaped ? quoteCount++ : quoteCount
                let unquoted = quotes % 2 == 0
                let colon = ch == ':' && unquoted ? ": " : null
                let nospace = char.IsWhiteSpace(ch) && unquoted ? string.Empty : null
                let lineBreak = ch == ',' && unquoted ? ch + Environment.NewLine 
                    + string.Concat(Enumerable.Repeat(indent, indentation)) : null

                let openChar = (ch == '{' || ch == '[') && unquoted ? ch + Environment.NewLine 
                    + string.Concat(Enumerable.Repeat(indent, ++indentation)) : ch.ToString()

                let closeChar = (ch == '}' || ch == ']') && unquoted ? Environment.NewLine 
                    + string.Concat(Enumerable.Repeat(indent, --indentation)) + ch : ch.ToString()

                select colon ?? nospace ?? lineBreak ?? (
                        openChar.Length > 1 ? openChar : closeChar
                        );

            return string.Concat(result);
        }

    }
}
