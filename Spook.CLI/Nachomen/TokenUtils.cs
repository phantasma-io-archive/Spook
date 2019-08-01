using System;
using System.Collections.Generic;
using System.Text;

using System;
using Phantasma.Numerics;

namespace Phantasma.Spook.Nachomen
{
    public static class TokenUtils
    {
        private const decimal units = 100000000m;

        public static decimal ToDecimal(BigInteger value)
        {
            if (value == null || value == 0)
            {
                return 0;
            }

            var n = (long)value;
            return n / units;
        }

        public static BigInteger ToBigInteger(decimal n)
        {
            var l = (long)(n * units);
            return new BigInteger(l);
        }
    }
}
