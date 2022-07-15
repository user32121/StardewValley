using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoTasker
{
    internal class Config
    {
        //only used initially
        //to disable mod you need to restart client
        public bool enabled = true;

        public int tileMargins = 3;

        public KeybindList reloadConfig = new KeybindList(SButton.F9);
        public KeybindList togglePlannerOverlay = new KeybindList(SButton.P);
        public KeybindList plannerAddTask = new KeybindList(SButton.MouseLeft);
        public KeybindList plannerRemoveTask = new KeybindList(SButton.MouseRight);
        public KeybindList plannerToggleAllTilesTask = new KeybindList(SButton.MouseMiddle);
        public KeybindList cyclePlannerTaskRight = new KeybindList(SButton.OemCloseBrackets);
        public KeybindList cyclePlannerTaskLeft = new KeybindList(SButton.OemOpenBrackets);
        public KeybindList runTasks = new KeybindList(SButton.OemPipe);

        public bool autoStartTasks = false;

        public Color colNone = Color.Black;
        public Color colClearDebris = Color.Gray;
        public Color colClearTrees = Color.Green;
        public Color colTill = Color.Brown;
        public Color colWater = Color.Blue;
        public Color colDigArtifacts = Color.RosyBrown;
        public Color colHarvestCrops = Color.Green;
        public Color colForage = Color.Green;
        public Color colTravelToTarget = Color.Lime;
        public Color colEasterEvent = Color.Pink;

        public Color colPath = Color.Lime;
        public int pathWidth = 2;

        public bool waterEmptyTiles = false;
        public bool waterPet = false;
        public bool alsoClearFruitTrees = false;
        public bool onlyChopGrownTrees = true;
    }
}
