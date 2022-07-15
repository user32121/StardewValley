using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WildTimeLapse
{
    public class WildTimeLapse : Mod
    {
        IReflectedMethod onFadeToBlackComplete;

        Config config;

        bool isWarping = false;
        int counter;

        private Dictionary<string, int> seasonToInt = new Dictionary<string, int>()
        {
            { "spring", 0 },
            { "summer", 1 },
            { "fall", 2 },
            { "winter", 3 },
        };

        public override void Entry(IModHelper helper)
        {
            config = Helper.ReadConfig<Config>();
            helper.WriteConfig(config);
            if (!config.enabled)
                return;

            Monitor.Log("Timelapse mod enabled, the only way to stop it is the exit the game", LogLevel.Info);

            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            onFadeToBlackComplete = helper.Reflection.GetMethod(Game1.game1, "onFadeToBlackComplete");
        }

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (e.Pressed.Contains(SButton.K))
            {

            }
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (isWarping && !Game1.isWarping)
            {
                //warp done
                if (Game1.currentLocation is Farm)
                {
                    //take screenshot
                    //code from Game1.takeMapScreenshot
                    string path = "Screenshots";
                    string playerFolder = SaveGame.FilterFileName(Game1.player.Name);
                    int folder = ((Environment.OSVersion.Platform != PlatformID.Unix) ? 26 : 28);
                    string path2 = Path.Combine(Environment.GetFolderPath((Environment.SpecialFolder)folder), "StardewValley", path, "timelapse", playerFolder);
                    if (!Directory.Exists(path2))
                        Directory.CreateDirectory(path2);
                    string filename = Game1.game1.takeMapScreenshot(1, Path.Combine("timelapse", playerFolder, String.Format("{0:D3}_{1}_{2:D2}", Game1.year, seasonToInt[Game1.currentSeason], Game1.dayOfMonth)), null);
                    
                    counter++;
                    Monitor.Log("Took " + counter + " screenshots", LogLevel.Debug);

                    //warp to home
                    Game1.warpHome();
                    onFadeToBlackComplete.Invoke();
                    //new day
                    Game1.NewDay(0);
                    onFadeToBlackComplete.Invoke();
                }
            }
            isWarping = Game1.isWarping;
        }

        private void GameLoop_DayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            //warp to farm
            isWarping = true;
            Game1.warpFarmer("Farm", 64, 15, Game1.down);
            onFadeToBlackComplete.Invoke();
        }
    }
}
