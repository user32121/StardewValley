using Microsoft.Xna.Framework;
using StardewModdingAPI.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterTracker
{
    internal class Config
    {
        public bool enabled = true;

        public KeybindList tendAnimals = new KeybindList(StardewModdingAPI.SButton.L);
        //public KeybindList talkToNPCs = new KeybindList(StardewModdingAPI.SButton.K);
        public KeybindList nextNPC = new KeybindList(StardewModdingAPI.SButton.OemComma);
        public KeybindList followNPC = new KeybindList(StardewModdingAPI.SButton.OemPeriod);

        public int highlightThickness = 2;
        public Color highlightColor = Color.Magenta;
        public Color highlight2Color = Color.Yellow;
        public int pathThickness = 2;
        public Color pathColor = Color.Lime;

        public bool petAutoPettedAnimals = true;

        //public bool petPet = true;

        //public bool talkToNPCsWithFullFriendship = false;
    }
}
