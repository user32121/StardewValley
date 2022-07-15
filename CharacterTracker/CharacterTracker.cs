using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CharacterTracker
{
    public class CharacterTracker : Mod
    {
        private Config config;

        private bool tendingAnimals;
        private List<NPC> npcs = new List<NPC>();
        private int curTrackedNPC;
        private bool trackingNPC;
        private bool noPathfinding;

        private Point? target;
        private List<Vector2> path;
        private int pathIndex;
        private FarmAnimal animalTarget;

        private HUDMessage prevHUDMessage;

        Texture2D blank;

        Stopwatch stopwatch = new Stopwatch();


        public override void Entry(IModHelper helper)
        {
            config = helper.ReadConfig<Config>();
            helper.WriteConfig(config);
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            Utils.Initialize(this);

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
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
                    int screenX = (int)(npcs[curTrackedNPC].getStandingX() - Game1.tileSize / 2 - Game1.viewport.X);
                    int screenY = (int)(npcs[curTrackedNPC].getStandingY() - Game1.tileSize / 2 - Game1.viewport.Y);
                    //top
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.highlightColor);
                    //bottom
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.highlightColor);
                    //left
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                    //right
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                    //line to player
                    Vector2 playerScreenPos = (Game1.player.Position - new Vector2(Game1.viewport.X, Game1.viewport.Y));
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize / 2, screenY + Game1.tileSize / 2, (int)(playerScreenPos - new Vector2(screenX, screenY)).Length(), config.highlightThickness), null, config.highlightColor, MathF.Atan2(playerScreenPos.Y - screenY, playerScreenPos.X - screenX), Vector2.Zero, SpriteEffects.None, 0);
                }
            }

            //animals
            if (tendingAnimals && Game1.currentLocation is Farm farm)
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                {
                    if (animal.currentLocation == Game1.currentLocation &&
                        (!animal.wasPet.Value && !animal.wasAutoPet.Value || (animal.currentProduce.Value > 0 && animal.age.Value >= animal.ageWhenMature.Value)))
                    {
                        int screenX = (int)(animal.getStandingX() - Game1.tileSize / 2 - Game1.viewport.X);
                        int screenY = (int)(animal.getStandingY() - Game1.tileSize / 2 - Game1.viewport.Y);
                        //top
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.highlightColor);
                        //bottom
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.highlightColor);
                        //left
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                        //right
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.highlightColor);
                    }
                }

            //path
            if (path != null)
            {
                Point curPos, prevPos = Point.Zero;
                for (int i = 0; i < path.Count; i++)
                {
                    int screenX = (int)(path[i].X * Game1.tileSize - Game1.viewport.X + Game1.tileSize / 2);
                    int screenY = (int)(path[i].Y * Game1.tileSize - Game1.viewport.Y + Game1.tileSize / 2);
                    curPos = new Point(screenX, screenY);
                    if (i > 0)
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, (int)(curPos - prevPos).ToVector2().Length(), config.pathThickness), null, config.pathColor, MathF.Atan2(prevPos.Y - curPos.Y, prevPos.X - curPos.X), Vector2.Zero, SpriteEffects.None, 0);
                    prevPos = curPos;
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

            if (trackingNPC && npcs.Count != 0)
            {
                if (e.IsOneSecond)
                    CalculatePathToNPC();
            }

            if (tendingAnimals)
            {
                if (Game1.currentLocation is not Farm farm)
                {
                    Game1.addHUDMessage(new HUDMessage("Not on farm", HUDMessage.error_type));
                    StopTendingAnimals();
                    return;
                }

                if (e.IsOneSecond)
                    FindNextAnimal();
            }

            //travel to target
            if (path != null && target.HasValue)
                if ((Game1.player.getTileLocation() - target.Value.ToVector2()).LengthSquared() <= 1 || pathIndex >= path.Count)
                {
                    //reached destination, stop moving
                    ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);

                    if (animalTarget != null)
                    {
                        if (!animalTarget.wasPet.Value && (!animalTarget.wasAutoPet.Value || config.petAutoPettedAnimals))
                        {
                            if (Game1.currentCursorTile != target.Value.ToVector2())
                            {
                                //face right direction
                                int screenX = (int)(((target.Value.X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                                int screenY = (int)(((target.Value.Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                                Game1.input.SetMousePosition(screenX, screenY);
                            }
                            else
                            {
                                ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                                target = null;
                            }
                        }
                    }
                }
                else
                {
                    //travel along path
                    if (path[pathIndex] == Game1.player.getTileLocation())
                        pathIndex++;
                    else
                    {
                        ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                        ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                        ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                        ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
                        Rectangle bb = Game1.player.GetBoundingBox();
                        if (bb.Left < path[pathIndex].X * Game1.tileSize)
                            ModPatches.SetKeyDown(Game1.options.moveRightButton[0].key);
                        else if (bb.Right > (path[pathIndex].X + 1) * Game1.tileSize)
                            ModPatches.SetKeyDown(Game1.options.moveLeftButton[0].key);
                        if (bb.Top < path[pathIndex].Y * Game1.tileSize)
                            ModPatches.SetKeyDown(Game1.options.moveDownButton[0].key);
                        else if (bb.Bottom > (path[pathIndex].Y + 1) * Game1.tileSize)
                            ModPatches.SetKeyDown(Game1.options.moveUpButton[0].key);
                    }
                }
        }

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

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
                    SetHUDMessage(String.Format("Currently tracking {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName));
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
                        noPathfinding = false;
                        if (npcs[curTrackedNPC].currentLocation != Game1.currentLocation)
                        {
                            StopTrackingNPC();
                            Game1.addHUDMessage(new HUDMessage(String.Format("Not in same location as {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName), HUDMessage.error_type));
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
            if (noPathfinding)
                return;

            if (npcs[curTrackedNPC].currentLocation != Game1.currentLocation)
            {
                StopTrackingNPC();
                Game1.addHUDMessage(new HUDMessage(String.Format("Not in same location as {0} ({1})", npcs[curTrackedNPC].displayName, npcs[curTrackedNPC].currentLocation.NameOrUniqueName), HUDMessage.error_type));
                return;
            }

            path = null;
            target = Utils.Bfs(Game1.currentLocation.Map.Layers[0].LayerSize, Game1.player.getTileLocationPoint(), isTrackedNPC, IsTilePassable, out Point[,] traversalMap);
            if (target.HasValue)
            {
                path = Utils.GeneratePathToTarget(Game1.player.getTileLocationPoint(), target.Value, traversalMap);
                pathIndex = 0;
                if (path.Count == 0)
                {
                    Game1.addHUDMessage(new HUDMessage("Unable to construct path to NPC", HUDMessage.error_type));
                    noPathfinding = true;
                }
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage("Unable to find path to NPC", HUDMessage.error_type));
                noPathfinding = true;
            }
        }

        private void FindNextAnimal()
        {
            if (noPathfinding)
                return;

            if (Game1.currentLocation is not Farm farm)
            {
                StopTendingAnimals();
                Game1.addHUDMessage(new HUDMessage("Not on farm", HUDMessage.error_type));
                return;
            }

            xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;
            FarmAnimal[,] validTiles = new FarmAnimal[size.Width, size.Height];
            bool anyValidAnimals = false;
            foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                if (animal.currentLocation == Game1.currentLocation &&
                    (!animal.wasPet.Value && !animal.wasAutoPet.Value || (animal.currentProduce.Value > 0 && animal.age.Value >= animal.ageWhenMature.Value)))
                {
                    validTiles[animal.getTileX(), animal.getTileY()] = animal;
                    anyValidAnimals = true;
                }


            if (anyValidAnimals)
            {
                path = null;
                target = Utils.Bfs(size, Game1.player.getTileLocationPoint(), (int x, int y) =>
                {
                    if (x < 0 || y < 0 || x >= validTiles.GetLength(0) || y >= validTiles.GetLength(1))
                        return false;
                    return validTiles[x, y] != null;
                }, IsTilePassable, out Point[,] traversalMap);
                if (target.HasValue)
                {
                    animalTarget = validTiles[target.Value.X, target.Value.Y];
                    path = Utils.GeneratePathToTarget(Game1.player.getTileLocationPoint(), target.Value, traversalMap);
                    pathIndex = 0;
                    if (path.Count == 0)
                    {
                        Game1.addHUDMessage(new HUDMessage("Unable to construct path to animal", HUDMessage.error_type));
                        noPathfinding = true;
                    }
                }
                else
                {
                    Game1.addHUDMessage(new HUDMessage("Unable to reach animals", HUDMessage.error_type));
                    noPathfinding = true;
                }
            }
            else
            {
                SetHUDMessage("No more animals here to tend to");
                StopTendingAnimals();
            }

            if (path == null || path.Count == 0)
            {
                //check if more animals
                HashSet<string> locs = new HashSet<string>();
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                    if ((!animal.wasPet.Value && !animal.wasAutoPet.Value || (animal.currentProduce.Value > 0 && animal.age.Value >= animal.ageWhenMature.Value)))
                        locs.Add(animal.currentLocation.NameOrUniqueName);
                if (locs.Count > 0)
                    SetHUDMessage("locations with unattended animals: " + string.Join(", ", locs));
            }
        }

        private bool isTrackedNPC(int x, int y)
        {
            return new Point(x, y) == npcs[curTrackedNPC].getTileLocationPoint();
        }

        private void SetHUDMessage(string message)
        {
            if (!Game1.doesHUDMessageExist(message))
            {
                if (prevHUDMessage != null)
                {
                    prevHUDMessage.timeLeft = 0;
                    prevHUDMessage.transparency = 0;
                }
                Game1.addHUDMessage(prevHUDMessage = new HUDMessage(message, HUDMessage.newQuest_type));
            }
        }

        private bool IsTilePassable(int x, int y)
        {
            return !Game1.currentLocation.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player);
        }

        private void StopTrackingNPC()
        {
            trackingNPC = false;
            path = null;
            ModPatches.ClearKeys();
        }

        private void StopTendingAnimals()
        {
            tendingAnimals = false;
            path = null;
            ModPatches.ClearKeys();
        }
    }
}
