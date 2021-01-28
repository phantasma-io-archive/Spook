using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Phantasma.Cryptography;
using Phantasma.Spook.Command;
using Phantasma.Spook.Utils;

namespace Phantasma.Spook.Shell
{
    class SpookShell
    {
        private Spook _node;

        public SpookShell(string[] args, SpookSettings conf, Spook node)
        {
            _node = node;
        }


    }
}
