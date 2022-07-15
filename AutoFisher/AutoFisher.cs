using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using System;

namespace AutoFisher
{
    public class AutoFisher : StardewModdingAPI.Mod
    {
        Random rng = new Random();

        PIDController fishingController = new PIDController(1, 0, 0);

        BobberBar curBB;
        bool bobberInBar;
        float bobberBarSpeed;
        float bobberBarPos;
        bool goForTreasure;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            Farmer player = Game1.player;
            if (Context.IsWorldReady && player != null && !player.IsLocalPlayer)  //some mods check for null, not sure if needed or not
            {
                Monitor.Log("temp", LogLevel.Debug);
                return;
            }

            if (player.CurrentTool is FishingRod rod)
            {
                //TODO config
                //TODO detect low energy, out of inventory space
                //auto cast
                //if (!rod.inUse() && !rod.isTimingCast && e.IsOneSecond)
                //{
                //    rod.beginUsing(Game1.currentLocation, player.getStandingX(), player.getStandingY(), player);
                //}

                //TODO config
                //instant bite
                if (rod.isFishing && !rod.isNibbling && !rod.hit && !rod.isReeling && !rod.pullingOutOfWater && e.IsOneSecond)
                {
                    rod.timeUntilFishingBite = 0;
                }

                //TODO config
                //auto hit
                if (rod.isFishing && rod.isNibbling && !rod.hit && !rod.isReeling && !rod.pullingOutOfWater)
                {
                    Farmer.useTool(player);
                }

                //TODO config
                //auto reel
                if (Game1.activeClickableMenu is BobberBar bb)
                {
                    if (bb != curBB)
                        NewBobberBar(bb);
                    SimulateFishing();
                }

                //TODO config
                //TODO auto loot treasure

                //TODO config
                //TODO max cast
            }
        }

        private void NewBobberBar(BobberBar bbMenu)
        {
            curBB = bbMenu;
            bobberInBar = Helper.Reflection.GetField<bool>(bbMenu, "bobberInBar").GetValue();
            bobberBarPos = Helper.Reflection.GetField<float>(bbMenu, "bobberBarPos").GetValue();
            bobberBarSpeed = Helper.Reflection.GetField<float>(bbMenu, "bobberBarSpeed").GetValue();

            fishingController.Reset();
        }

        private void SimulateFishing()
        {
            fishingController.P = 1;
            fishingController.I = 0.01f;
            fishingController.D = 20;

            float bobberPosition = Helper.Reflection.GetField<float>(curBB, "bobberPosition").GetValue();
            int bobberBarHeight = Helper.Reflection.GetField<int>(curBB, "bobberBarHeight").GetValue();
            bool hasTreasure = Helper.Reflection.GetField<bool>(curBB, "treasure").GetValue();
            bool treasureCaught = Helper.Reflection.GetField<bool>(curBB, "treasureCaught").GetValue();
            float treasurePos = Helper.Reflection.GetField<float>(curBB, "treasurePosition").GetValue();
            float treasureAppearTimer = Helper.Reflection.GetField<float>(curBB, "treasureAppearTimer").GetValue();
            int whichBobber = Helper.Reflection.GetField<int>(curBB, "whichBobber").GetValue();
            float distanceFromCatching = Helper.Reflection.GetField<float>(curBB, "distanceFromCatching").GetValue();
            bool fadeIn = Helper.Reflection.GetField<bool>(curBB, "fadeIn").GetValue();
            bool fadeOut = Helper.Reflection.GetField<bool>(curBB, "fadeOut").GetValue();
            
            if (hasTreasure && treasureAppearTimer <= 0 && !treasureCaught && distanceFromCatching > 0.8f)
                goForTreasure = true;
            else if (distanceFromCatching < 0.5f)
                goForTreasure = false;
            float targetPos = goForTreasure ? treasurePos : bobberPosition;
            float curPos = bobberBarPos + bobberBarHeight / 2 - 32;
            float pidOutput = fishingController.Update(targetPos - curPos);
            bool moveUp = pidOutput / -200 + 0.5f > rng.NextDouble();


            //code is copied from the BobberBar class, but I don't know how else to force key inputs without patching it

            if (!fadeIn && !fadeOut)
            {
                bobberInBar = bobberPosition + 12f <= bobberBarPos - 32f + (float)bobberBarHeight && bobberPosition - 16f >= bobberBarPos - 32f;
                if (bobberPosition >= (float)(548 - bobberBarHeight) && bobberBarPos >= (float)(568 - bobberBarHeight - 4))
                {
                    bobberInBar = true;
                }

                bool buttonPressed = moveUp || Game1.oldMouseState.LeftButton == ButtonState.Pressed || Game1.isOneOfTheseKeysDown(Game1.oldKBState, Game1.options.useToolButton) || (Game1.options.gamepadControls && (Game1.oldPadState.IsButtonDown(Buttons.X) || Game1.oldPadState.IsButtonDown(Buttons.A)));

                float num6 = (buttonPressed ? (-0.25f) : 0.25f);
                if (buttonPressed && num6 < 0f && (bobberBarPos == 0f || bobberBarPos == (float)(568 - bobberBarHeight)))
                {
                    bobberBarSpeed = 0f;
                }

                if (bobberInBar)
                {
                    num6 *= ((whichBobber == 691) ? 0.3f : 0.6f);
                    if (whichBobber == 691)
                    {
                        if (bobberPosition + 16f < bobberBarPos + (float)(bobberBarHeight / 2))
                        {
                            bobberBarSpeed -= 0.2f;
                        }
                        else
                        {
                            bobberBarSpeed += 0.2f;
                        }
                    }
                }

                float num7 = bobberBarPos;
                bobberBarSpeed += num6;
                bobberBarPos += bobberBarSpeed;
                if (bobberBarPos + (float)bobberBarHeight > 568f)
                {
                    bobberBarPos = 568 - bobberBarHeight;
                    bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f * ((whichBobber == 692) ? 0.1f : 1f);
                }
                else if (bobberBarPos < 0f)
                {
                    bobberBarPos = 0f;
                    bobberBarSpeed = (0f - bobberBarSpeed) * 2f / 3f;
                }

                //force values back in menu
                Helper.Reflection.GetField<bool>(curBB, "bobberInBar").SetValue(bobberInBar);
                Helper.Reflection.GetField<float>(curBB, "bobberBarPos").SetValue(bobberBarPos);
                Helper.Reflection.GetField<float>(curBB, "bobberBarSpeed").SetValue(bobberBarSpeed);
                //TODO config
                //never fail
                if(distanceFromCatching < 0.1)
                    Helper.Reflection.GetField<float>(curBB, "distanceFromCatching").SetValue(0.1f);
            }
        }

        void Console(string message)
        {
            Monitor.Log(message, LogLevel.Debug);
        }
    }
}
