using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using User32121Lib;

namespace CharacterTracker
{
    public class CharacterTracker : Mod
    {
        private Config config;

        private IAPI user32121API;

        private bool tendingAnimals;
        private List<NPC> npcs = new List<NPC>();
        private int curTrackedNPC;
        private bool trackingNPC;

        private HUDMessage prevHUDMessage;

        Texture2D blank;


        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<Config>();
            helper.WriteConfig(config);
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
        }

        private void DisableMod()
        {
            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            Helper.Events.Display.RenderedWorld += Display_RenderedWorld;

            ModPatches.ClearKeys();
        }

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            user32121API = Helper.ModRegistry.GetApi<IAPI>("user32121.User32121Lib");
            if (user32121API == null)
            {
                Monitor.Log("Unable to load user32121.User32121Lib", LogLevel.Error);
                DisableMod();
            }
        }
        private void GameLoop_DayStarted(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            npcs.Clear();
            foreach (GameLocation loc in Game1.locations)
                foreach (NPC npc in loc.characters)
                    npcs.Add(npc);
            npcs.Sort((NPC npc1, NPC npc2) => npc1.Name.CompareTo(npc2.Name));
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            //load textures
            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            //npcs
            if (npcs.Count != 0)
            {
                if (npcs[curTrackedNPC].currentLocation == Game1.currentLocation && trackingNPC)
                {
                    int screenX = npcs[curTrackedNPC].getStandingX() - Game1.tileSize / 2 - Game1.viewport.X;
                    int screenY = npcs[curTrackedNPC].getStandingY() - Game1.tileSize / 2 - Game1.viewport.Y;
                    //top
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.highlightColor);
                    //bottom
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.highlightColor);
                    //left
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                    //right
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                    //line to player
                    Vector2 playerScreenPos = Game1.player.Position - new Vector2(Game1.viewport.X, Game1.viewport.Y);
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize / 2, screenY + Game1.tileSize / 2, (int)(playerScreenPos - new Vector2(screenX, screenY)).Length(), config.highlightThickness), null, config.highlightColor, MathF.Atan2(playerScreenPos.Y - screenY, playerScreenPos.X - screenX), Vector2.Zero, SpriteEffects.None, 0);
                }
            }

            //animals
            if (tendingAnimals && Game1.currentLocation is Farm farm)
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                {
                    if (animal.currentLocation == Game1.currentLocation)
                    {
                        Color? col = null;
                        if (ShouldPetAnimal(animal))
                            col = config.highlightColor;
                        else if (ShouldHarvestAnimal(animal))
                            col = config.highlight2Color;
                        if (col.HasValue)
                        {
                            int screenX = animal.getStandingX() - Game1.tileSize / 2 - Game1.viewport.X;
                            int screenY = animal.getStandingY() - Game1.tileSize / 2 - Game1.viewport.Y;
                            //top
                            e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), col.Value);
                            //bottom
                            e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), col.Value);
                            //left
                            e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), col.Value);
                            //right
                            e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), col.Value);
                        }
                    }
                }
        }

        private void EnsureTexturesLoaded(GraphicsDevice gd)
        {
            if (blank == null)
            {
                blank = new Texture2D(gd, 1, 1);
                blank.SetData(new Color[] { Color.White });
            }
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (!Game1.player.IsLocalPlayer)
                return;
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            if (tendingAnimals)
            {
                if (Game1.currentLocation is not Farm farm)
                {
                    Game1.addHUDMessage(new HUDMessage("Not on farm", HUDMessage.error_type));
                    StopTendingAnimals();
                    return;
                }
            }
        }

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            //observe
            //if (e.Pressed.Contains(SButton.O))
            //{
            //    if (Game1.currentLocation is Farm f)
            //        foreach (var item in f.animals.Values)
            //        {
            //            Monitor.Log(string.Format("{0}: {1}", item.Name, item.currentProduce.Value), LogLevel.Debug);
            //        }
            //}

            if (config.tendAnimals.JustPressed())
            {
                tendingAnimals = !tendingAnimals;
                if (tendingAnimals)
                {
                    SetHUDMessage("tending to animals");
                    FindNextAnimal();
                }
                else
                {
                    SetHUDMessage("stopped tending to animals");
                    StopTendingAnimals();
                }
            }

            if (config.nextNPC.JustPressed())
            {
                if (npcs.Count == 0)
                    Game1.addHUDMessage(new HUDMessage("No NPCs", HUDMessage.error_type));
                else
                {
                    curTrackedNPC++;
                    curTrackedNPC %= npcs.Count;
                    SetHUDMessage(string.Format("Currently tracking {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName));
                }
            }

            if (config.followNPC.JustPressed())
            {
                if (npcs.Count == 0)
                    Game1.addHUDMessage(new HUDMessage("No NPCs", HUDMessage.error_type));
                else
                {
                    trackingNPC = !trackingNPC;
                    if (trackingNPC)
                    {
                        if (npcs[curTrackedNPC].currentLocation != Game1.currentLocation)
                        {
                            StopTrackingNPC();
                            Game1.addHUDMessage(new HUDMessage(string.Format("Not in same location as {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName), HUDMessage.error_type));
                        }
                        else
                        {
                            SetHUDMessage("tracking " + npcs[curTrackedNPC].displayName);
                            CalculatePathToNPC();
                        }
                    }
                    else
                    {
                        SetHUDMessage("Stopped tracking " + npcs[curTrackedNPC].displayName);
                        StopTrackingNPC();
                    }
                }
            }
        }

        private void CalculatePathToNPC()
        {
            if (npcs[curTrackedNPC].currentLocation != Game1.currentLocation)
            {
                StopTrackingNPC();
                Game1.addHUDMessage(new HUDMessage(String.Format("Not in same location as {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName), HUDMessage.error_type));
                return;
            }

            user32121API.Pathfind(IsTrackedNPC);
        }

        private void FindNextAnimal()
        {
            if (Game1.currentLocation is not Farm farm)
            {
                StopTendingAnimals();
                Game1.addHUDMessage(new HUDMessage("Not on farm", HUDMessage.error_type));
                return;
            }

            xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;
            Dictionary<Point, FarmAnimal> validTiles = new Dictionary<Point, FarmAnimal>();
            int animalCount = 0;
            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                if (animal.currentLocation == Game1.currentLocation && (ShouldPetAnimal(animal) || ShouldHarvestAnimal(animal)))
                {
                    validTiles[animal.GetBoundingBox().Center / new Point(Game1.tileSize, Game1.tileSize)] = animal;
                    animalCount++;
                }
            Monitor.Log("finding animal", LogLevel.Debug);

            if (validTiles.Count > 0)
            {
                user32121API.Pathfind((int x, int y) =>
                {
                    TileData tileData = IsPassableIncludeAnimals(x, y);
                    return tileData.action != TileData.ACTION.IMPASSABLE && tileData.action != TileData.ACTION.NONE;
                }, IsPassableIncludeAnimals, pathfindingComplete: FindNextAnimal);

                if (!user32121API.HasPath())
                    StopTendingAnimals();
            }
            else
            {
                SetHUDMessage("No more animals here to tend to");
                StopTendingAnimals();
            }

            if (!user32121API.HasPath())
            {
                //check if more animals
                HashSet<string> locs = new HashSet<string>();
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                    if (ShouldPetAnimal(animal) || ShouldHarvestAnimal(animal))
                        locs.Add(animal.currentLocation.NameOrUniqueName);
                if (locs.Count > 0)
                    SetHUDMessage("locations with unattended animals: " + string.Join(", ", locs));
            }
        }

        private bool ShouldPetAnimal(FarmAnimal animal)
        {
            return !animal.wasPet.Value && (!animal.wasAutoPet.Value || config.petAutoPettedAnimals);
        }
        private bool ShouldHarvestAnimal(FarmAnimal animal)
        {
            return animal.currentProduce.Value > 0 && animal.age.Value >= animal.ageWhenMature.Value;
        }

        private bool IsTrackedNPC(int x, int y)
        {
            return new Point(x, y) == npcs[curTrackedNPC].getTileLocationPoint();
        }

        private void SetHUDMessage(string message)
        {
            if (prevHUDMessage != null)
            {
                prevHUDMessage.timeLeft = 0;
                prevHUDMessage.transparency = 0;
            }
            Game1.addHUDMessage(prevHUDMessage = new HUDMessage(message, HUDMessage.newQuest_type));
        }

        private TileData IsPassableIncludeAnimals(int x, int y)
        {
            Rectangle tileBB = new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
            bool isAnimal = false;
            foreach (Character npc in Game1.currentLocation.characters.Cast<Character>().Concat((Game1.currentLocation as Farm)?.Animals.Values.AsEnumerable()))
                if ((npc.getTileLocationPoint() == new Point(x, y) || tileBB.Intersects(npc.GetBoundingBox())) && npc is FarmAnimal fa)
                {
                    isAnimal = true;
                    if (ShouldPetAnimal(fa))
                        return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => fa.wasPet.Value);
                    else if (ShouldHarvestAnimal(fa) && fa.getTileLocationPoint() != Game1.player.getTileLocationPoint() && tileBB.Intersects(fa.GetHarvestBoundingBox()))
                            return new TileData(TileData.ACTION.USETOOL, Utils.nameToTool[fa.toolUsedForHarvest.Value], 1, () => fa.currentProduce.Value <= 0);
                }
            if (isAnimal)
                return TileData.Passable;

            return user32121API.DefaultIsPassable(x, y);
        }

        private void StopTrackingNPC()
        {
            trackingNPC = false;
            user32121API.CancelPathfinding();
            ModPatches.ClearKeys();
        }

        private void StopTendingAnimals()
        {
            tendingAnimals = false;
            user32121API.CancelPathfinding();
            ModPatches.ClearKeys();
        }
    }
}
