using Phantasma.Blockchain;
using System;

namespace Phantasma.Spook.Oracles
{
    public static class OracleUtils
    {
        public static byte[] ReadNEO(string[] input)
        {
            if (input == null || input.Length == 0)
            {
                throw new OracleException("missing oracle input");
            }

            var cmd = input[0].ToLower();
            switch (cmd)
            {
                case "tx":
                    throw new NotImplementedException();

                case "block":
                    throw new NotImplementedException();

                default:
                    throw new OracleException("unknown neo oracle");
            }
        }
    }
}
