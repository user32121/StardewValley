using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xTile.Dimensions;

namespace AutoMiner
{
    internal static class Utils
    {
        public static Dictionary<int, string> objIndexToName = new Dictionary<int, string>()
        {
            { 8, "Topaz Node" },
            { 10, "Amethyst Node" },
            { 14, "Aquamarine Node" },
            { 6, "Jade Node" },
            { 12, "Emerald Node" },
            { 4, "Ruby Node" },
            { 2, "Diamond Node" },
            { 44, "Gem Node" },
            { 26, "Mystic Node" },
            { 75, "Geode Node" },
            { 76, "Frozen Geode Node" },
            { 77, "Magma Geode Node" },
            { 819, "Omni Geode Node" },
            { 843, "Cinder Shard Node" },
            { 844, "Cinder Shard Node" },
            { 751, "Copper Node" },
            { 849, "Copper Node" },
            { 290, "Iron Node" },
            { 850, "Iron Node" },
            { 764, "Gold Node" },
            { 765, "Iridium Node" },
            { 95, "Radioactive Node" },
        };
    }
}
