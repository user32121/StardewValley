using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoMiner
{
    internal class Config
    {
        public bool enabled = true;

        public KeybindList reloadConfig = new KeybindList(SButton.F9);
        public KeybindList startBot = new KeybindList(SButton.OemSemicolon);
        public KeybindList cycleTarget = new KeybindList(SButton.OemQuotes);
        public KeybindList spamAttack = new KeybindList(SButton.Q);

        public int highlightThickness = 2;
        public int pathThickness = 2;
        public Color pathColor = Color.Lime;
        public Color targetColor = Color.Lime;
        public Color ladderColor = Color.Yellow;
        public Color specialColor = Color.Magenta;

        public bool autoDescend = true;
        public bool outputInfestedFloors = true;
        public bool highlightHasSpecialItem = true;

        public int bfsBreakCost = 5;

        public bool searchName = true;
        public bool searchDescription = true;
        public bool searchSheetIndex = true;
        public bool ignoreCase = true;
        public bool useRegex = true;
        //uses regex
        public Dictionary<string, List<string>> mineTargets = new Dictionary<string, List<string>>()
        {
            { "none", new List<string>(){ } },
            { "all", new List<string>(){ ".*" } },
            { "ore", new List<string>(){ ".*Node.*" } },
            { "boulders", new List<string>(){ ".*mineRock.*" } },
            { "main", new List<string>(){ ".*Node.*",".*Quartz.*",".*gem.*",".*tear.*",".*shroom.*",".*Barrel.*",".*Gunther.*" } },
        };
        public Dictionary<string, List<string>> mineSpecialTargets = new Dictionary<string, List<string>>()
        {
            { "none", new List<string>(){ } },
            { "ore", new List<string>(){ ".*Iridium.*", ".*Mystic.*" } },
            { "main", new List<string>(){ ".*Iridium.*", ".*Mystic.*" } },
        };
    }
}
