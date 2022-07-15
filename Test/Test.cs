using StardewModdingAPI;
using System;

namespace Test
{
    public class Test : StardewModdingAPI.Mod
    {
        public override void Entry(IModHelper helper)
        {
            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
        }

        private void Input_ButtonPressed(object sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            Monitor.Log(e.Button.ToString(), LogLevel.Debug);
        }
    }
}
