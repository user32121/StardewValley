using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTasker
{
    internal class Task
    {
        public enum TASK_TYPE
        {
            NONE,
            CLEAR_DEBRIS,
            CLEAR_TREES,
            TILL,
            WATER,
            REFILL_WATER,
			HARVEST_CROPS,
			DIG_ARTIFACTS,
			TRAVEL_TO_TARGET,
            FORAGE,
            EASTER_EVENT,
        }
        public TASK_TYPE type = TASK_TYPE.NONE;
        public List<Vector2> tiles = new List<Vector2>();

        //more friendly names
        public static Dictionary<TASK_TYPE, string> taskToString = new Dictionary<TASK_TYPE, string>()
        {
            { TASK_TYPE.NONE, "None" },
            { TASK_TYPE.CLEAR_DEBRIS, "Clear Debris" },
            { TASK_TYPE.CLEAR_TREES, "Clear Trees" },
            { TASK_TYPE.TILL, "Till" },
            { TASK_TYPE.WATER, "Water" },
            { TASK_TYPE.REFILL_WATER, "Refill Water" },
            { TASK_TYPE.HARVEST_CROPS, "Harvest Crops" },
            { TASK_TYPE.DIG_ARTIFACTS, "Dig Artifacts" },
            { TASK_TYPE.TRAVEL_TO_TARGET, "Travel to Target" },
            { TASK_TYPE.FORAGE, "Forage" },
            { TASK_TYPE.EASTER_EVENT, "Easter Event" },
        };
    }
}
