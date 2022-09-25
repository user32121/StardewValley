using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace User32121Lib
{
    public interface IAPI
    {
        void CancelPathfinding();
        TileData DefaultIsPassable(int x, int y);
        TileData DefaultIsPassableWithMining(int x, int y);
        void Pathfind(Func<int, int, bool> isTarget, Func<int, int, TileData> isPassable = null, Action pathfindingCanceled = null, Action pathfindingComplete = null, bool quiet = false);
        bool HasPath();

        Options GetOptions();
    }
}
