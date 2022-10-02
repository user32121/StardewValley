using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoNav
{
    public interface IAPI
    {
        public bool TravelToLocation(GameLocation location);
        public bool TravelToClosestLocation(HashSet<GameLocation> locations);
    }
}
