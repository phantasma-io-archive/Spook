using System;
using System.Runtime.InteropServices;

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

    }
}
