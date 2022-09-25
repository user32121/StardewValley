using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace User32121Lib
{
    public struct TileData
    {
        public enum ACTION
        {
            NONE,
            IMPASSABLE,
            ACTIONBUTTON,
            USETOOLBUTTON,
            USETOOL,
            CUSTOM,
        }

        public ACTION action;
        public Type tool;
        public int traversalCost;
        public Action customAction;
        public Func<bool> isActionDone;

        public bool IsPassable => action != ACTION.IMPASSABLE;
        public bool IsPassableWithoutAction => action == ACTION.NONE;

        public TileData(ACTION action, Type tool, int traversalCost, Func<bool> isActionDone, Action customAction)
        {
            this.action = action;
            this.tool = tool;
            this.traversalCost = traversalCost;
            this.customAction = customAction ?? (action == ACTION.CUSTOM ? throw new ArgumentNullException(nameof(customAction), "cannot be null when " + nameof(action) + " is " + nameof(ACTION.CUSTOM)) : null);
            this.isActionDone = isActionDone;
        }

        public TileData(ACTION action, Type tool, int traversalCost, Func<bool> isActionDone) : this(action, tool, traversalCost, isActionDone, null) { }


        public readonly static TileData Passable;
        public readonly static TileData Impassable;

        static TileData()
        {
            Passable = new TileData(ACTION.NONE, null, 1, () => true);
            Impassable = new TileData(ACTION.IMPASSABLE, null, 1000000000, () => false);
        }
    }
}
