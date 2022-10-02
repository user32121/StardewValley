using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterTracker
{
    internal class Utils
    {
        public static Dictionary<String, Type> nameToTool = new Dictionary<String, Type>()
        {
            { "Milk Pail", typeof(MilkPail) },
            { "Shears", typeof(Shears) },
        };
    }
}
