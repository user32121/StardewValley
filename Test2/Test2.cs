using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Linq;
using User32121Lib;

namespace Test2
{
    internal class Test2 : Mod
    {
        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
        }

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (e.Pressed.Contains(SButton.O))
            {
                IAPI lib = Helper.ModRegistry.GetApi<IAPI>("user32121.User32121Lib");
                if (lib != null)
                {
                    Monitor.Log("pathfinding...", LogLevel.Debug);
                    Vector2 target = Game1.currentCursorTile;
                    lib.Pathfind((int x, int y) => x == target.X && y == target.Y, lib.DefaultIsPassableWithMining);
                }
            }
        }
    }
}
