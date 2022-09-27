using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace User32121Lib
{
    internal class Config
    {
        public bool enabled = true;
        public KeybindList reloadConfig = new KeybindList(SButton.F9);

        public KeybindList spamTool = new KeybindList(SButton.Q);

        public KeybindList abortPathFinding = new KeybindList(SButton.CapsLock);

        public int defaultBreakWeedCost = 2;
        public int defaultBreakStoneCost = 10;

        public int highlightThickness = 2;
        public int pathThickness = 2;
        public Color pathColor = Color.Lime;
        public Color intermediateColor = Color.Yellow;
        public Color targetColor = Color.Lime;

        public double maxBFSTime = 1;
        public double maxPathConstructionTime = 1;
        public double recalculationFrequency = 1;

        public bool? autoeatOverride = null;
        public double eatStaminaThreshold = 0.1;  //if below, will eat if it doesn't overflow
        public double eatHealthThreshold = 0.5;
        public double overEatStaminaThreshold = 0.1;  //if below, will eat even if it overflows
        public double overEatHealthThreshold = 0.1;
        public bool eatBestFoodFirst = false;

        public double minimumStamina = 10;

        public bool autoCombat = true;
        public KeybindList overrideCombat = new KeybindList(new Keybind(SButton.W), new Keybind(SButton.A), new Keybind(SButton.S), new Keybind(SButton.D));
    }
}