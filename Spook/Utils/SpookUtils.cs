using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Reflection;

using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net;
using System.Text.Json;

namespace Phantasma.Spook.Utils
{

    public static class SpookUtils
    {
        public static string FixPath(string path)
        {
            path = path.Replace("\\", "/");

            if (!path.EndsWith('/'))
            {
                path += '/';
            }

            return path;
        }

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
        public static string GetVersion(this Assembly assembly)
        {
            CustomAttributeData attribute = assembly.CustomAttributes.FirstOrDefault(p => p.AttributeType == typeof(AssemblyInformationalVersionAttribute));
            if (attribute == null) return assembly.GetName().Version.ToString(3);
            return (string)attribute.ConstructorArguments[0].Value;
        }

        public static bool IsValidJson(this string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return false;
            }

            var value = stringValue.Trim();

            if ((value.StartsWith("{") && value.EndsWith("}")) || //For object
                (value.StartsWith("[") && value.EndsWith("]"))) //For array
            {
                try
                {
                    var obj = JToken.Parse(value);
                    return true;
                }
                catch (JsonReaderException)
                {
                    return false;
                }
            }

            return false;
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

        public static decimal GetNormalizedFee(FeeUrl[] fees)
        {
            var taskList = new List<Task<decimal>>();

            foreach (var fee in fees)
            {
                taskList.Add(
                        new Task<decimal>(() => 
                        {
                            return GetFee(fee);
                        })
                );
            }

            Parallel.ForEach(taskList, (task) =>
            {
                task.Start();
            });

            Task.WaitAll(taskList.ToArray());

            var results = new List<decimal>();
            foreach (var task in taskList)
            {
                results.Add(task.Result);
            }

            var median = GetMedian<decimal>(results.ToArray());

            return median;
        }

        public static decimal GetFee(FeeUrl feeObj)
        {
            decimal fee = 0;

            if (string.IsNullOrEmpty(feeObj.url))
            {
                return feeObj.defaultFee;
            }

            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(feeObj.url);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty(feeObj.feeHeight, out var prop))
                    {
                        fee = decimal.Parse(prop.ToString().Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);
                        fee += feeObj.feeIncrease;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Getting fee failed: " + e);
            }

            return fee;
        }

        public static T GetMedian<T>(T[] sourceArray) where T : IComparable<T>
        {
            if (sourceArray == null || sourceArray.Length == 0)
                throw new ArgumentException("Median of empty array not defined.");

            T[] sortedArray = sourceArray;
            Array.Sort(sortedArray);

            //get the median
            int size = sortedArray.Length;
            int mid = size / 2;
            if (size % 2 != 0)
            {
                return sortedArray[mid];
            }

            dynamic value1 = sortedArray[mid];
            dynamic value2 = sortedArray[mid - 1];

            return (sortedArray[mid] + value2) * 0.5;
        }
    }
}
