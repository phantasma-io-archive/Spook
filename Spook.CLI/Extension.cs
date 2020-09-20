using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phantasma.Spook
{
    internal static class SpookExtensions
    {
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


    }
}

