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
using U32121IAPI = User32121Lib.IAPI;
using ANIAPI = AutoNav.IAPI;
using AutoNav;
using StardewValley.Buildings;
using StardewValley.Characters;

namespace CharacterTracker
{
    public class CharacterTracker : Mod
    {
        private Config config;

        private U32121IAPI user32121API;
        private ANIAPI navAPI;

        private bool tendingAnimals;
        private bool talkingToNPCs;
        private List<NPC> npcs = new List<NPC>();
        private int curTrackedNPC;
        private bool trackingNPC;
        private HashSet<(GameLocation, NPC)> unreachableNPCs = new();

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
            user32121API = Helper.ModRegistry.GetApi<U32121IAPI>("user32121.User32121Lib");
            if (user32121API == null)
            {
                Monitor.Log("Unable to load user32121.User32121Lib", LogLevel.Error);
                DisableMod();
            }
            navAPI = Helper.ModRegistry.GetApi<ANIAPI>("user32121.AutoNav");
            if (navAPI == null)
            {
                Monitor.Log("Unable to load user32121.AutoNav", LogLevel.Error);
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
            unreachableNPCs.Clear();
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
            if (tendingAnimals)
                foreach (FarmAnimal animal in Game1.getFarm().getAllFarmAnimals())
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

            if (talkingToNPCs)
                foreach (NPC npc in npcs)
                    if (ShouldTalkToNPC(npc) && Game1.currentLocation == npc.currentLocation)
                    {
                        int screenX = npc.getStandingX() - Game1.tileSize / 2 - Game1.viewport.X;
                        int screenY = npc.getStandingY() - Game1.tileSize / 2 - Game1.viewport.Y;
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

            if (e.IsOneSecond && (Game1.currentLocation is Farm || Game1.currentLocation is AnimalHouse))
            {
                if (tendingAnimals && !user32121API.HasPath())
                    FindNextAnimal();
            }
            if (e.IsOneSecond && talkingToNPCs && !user32121API.HasPath())
                FindNextNPC();
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
                    if (Game1.currentLocation is Farm || Game1.currentLocation.isFarmBuildingInterior())
                        FindNextAnimal();
                    else if (!navAPI.TravelToLocation(Game1.getFarm()))
                    {
                        Game1.addHUDMessage(new HUDMessage("Unable to travel to farm", HUDMessage.error_type));
                        StopTendingAnimals();
                    }
                }
                else
                {
                    SetHUDMessage("stopped tending to animals");
                    StopTendingAnimals();
                }
            }

            if (config.talkToNPCs.JustPressed())
            {
                talkingToNPCs = !talkingToNPCs;
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
            if (Game1.currentLocation is not Farm farm && Game1.currentLocation is not AnimalHouse)
            {
                StopTendingAnimals();
                Game1.addHUDMessage(new HUDMessage("Not on farm", HUDMessage.error_type));
                return;
            }

            bool hasAnimalsHere = false;
            foreach (FarmAnimal animal in Game1.getFarm().getAllFarmAnimals())
                if (animal.currentLocation == Game1.currentLocation && (ShouldPetAnimal(animal) || ShouldHarvestAnimal(animal)))
                    hasAnimalsHere = true;
            if (Game1.currentLocation is Farm f)
            {
                Pet pet = Game1.player.getPet();
                if (ShouldPetAnimal(pet))
                    hasAnimalsHere = true;
            }
            Monitor.Log("finding animal", LogLevel.Debug);

            if (hasAnimalsHere)
            {
                user32121API.Pathfind((int x, int y) =>
                {
                    TileData tileData = IsPassableIncludeAnimals(x, y);
                    return tileData.action != TileData.ACTION.IMPASSABLE && tileData.action != TileData.ACTION.NONE;
                }, IsPassableIncludeAnimals, pathfindingComplete: FindNextAnimal);
            }

            if (!user32121API.HasPath())
            {
                //check if more animals
                HashSet<Point> animalBuildingDoors = new();
                bool moreAnimals = false;
                foreach (FarmAnimal animal in Game1.getFarm().getAllFarmAnimals())
                    if (ShouldPetAnimal(animal) || ShouldHarvestAnimal(animal))
                    {
                        if (animal.currentLocation is AnimalHouse ah)
                            animalBuildingDoors.Add(ah.getBuilding().getPointForHumanDoor());
                        moreAnimals = true;
                    }

                if (!moreAnimals)
                {
                    StopTendingAnimals();
                    SetHUDMessage("No more animals here to tend to");
                }
                if (Game1.currentLocation is not Farm)
                {
                    navAPI.TravelToLocation(Game1.getFarm());
                }
                else if (animalBuildingDoors.Count > 0)
                {
                    user32121API.Pathfind((x, y) => animalBuildingDoors.Contains(new Point(x, y)), (x, y) => IsPassableIncludeAnimalsAndBuildingDoors(x, y, animalBuildingDoors));

                    if (user32121API.HasPath())
                    {
                        SetHUDMessage("going inside animal house");
                    }
                    else
                    {
                        SetHUDMessage("unable to reach animal house doors: " + string.Join(' ', animalBuildingDoors));
                        StopTendingAnimals();
                    }
                }
            }
        }

        private void FindNextNPC()
        {
            Monitor.Log("finding npc", LogLevel.Debug);
            HashSet<NPC> NPCsHere = new();
            foreach (NPC npc in npcs)
                if (npc.currentLocation == Game1.currentLocation && ShouldTalkToNPC(npc))
                    NPCsHere.Add(npc);

            if (NPCsHere.Count > 0)
            {
                user32121API.Pathfind((int x, int y) =>
                {
                    TileData tileData = IsPassableIncludeNPCs(x, y);
                    return tileData.action != TileData.ACTION.IMPASSABLE && tileData.action != TileData.ACTION.NONE;
                }, IsPassableIncludeNPCs, pathfindingComplete: FindNextNPC);
                if (!user32121API.HasPath())
                {
                    foreach (NPC npc in NPCsHere)
                        unreachableNPCs.Add((npc.currentLocation, npc));
                }
            }

            if (!user32121API.HasPath())
            {
                //check if more npcs
                HashSet<GameLocation> locsWithNPCs = new();
                foreach (NPC npc in npcs)
                    if (ShouldTalkToNPC(npc))
                        locsWithNPCs.Add(npc.currentLocation);

                if (locsWithNPCs.Count == 0)
                {
                    StopTalkingToNPCs();
                    SetHUDMessage("No more NPCs to talk to");
                }
                if (!navAPI.TravelToClosestLocation(locsWithNPCs))
                {
                    StopTalkingToNPCs();
                    SetHUDMessage("Unable to reach any more NPCs");
                }
            }
        }

        private bool ShouldPetAnimal(FarmAnimal animal)
        {
            return !animal.wasPet.Value && (!animal.wasAutoPet.Value || config.petAutoPettedAnimals);
        }
        private bool ShouldPetAnimal(Pet pet)
        {
            if (!config.petPet)
                return false;
            return pet.lastPetDay[Game1.player.UniqueMultiplayerID] != Game1.Date.TotalDays;
        }
        private bool ShouldHarvestAnimal(FarmAnimal animal)
        {
            return animal.currentProduce.Value > 0 && animal.age.Value >= animal.ageWhenMature.Value && animal.defaultProduceIndex.Value != 430;
        }

        private bool ShouldTalkToNPC(NPC npc)
        {
            return npc.canTalk() && !unreachableNPCs.Contains((npc.currentLocation, npc)) &&
                Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship fs) && !fs.TalkedToToday &&
                (config.talkToNPCsWithFullFriendship || fs.Points < (npc.datable.Value ? 2000 : 2500) || fs.IsDating() || fs.IsMarried());
        }

        private void HarvestAnimal(FarmAnimal animal)
        {
            Rectangle hbb = animal.GetHarvestBoundingBox();
            Point mousePos = Game1.getMousePosition();
            mousePos.X += Game1.viewport.X;
            mousePos.Y += Game1.viewport.Y;
            if (!hbb.Contains(mousePos))
            {
                int screenX = (int)((hbb.Center.X - Game1.viewport.X) * Game1.options.zoomLevel);
                int screenY = (int)((hbb.Center.Y - Game1.viewport.Y) * Game1.options.zoomLevel);
                Game1.input.SetMousePosition(screenX, screenY);
                return;  //wait 1 frame for player to turn to target
            }
            if (Game1.player.UsingTool)
                return;

            if (user32121API.SwitchToTool(Utils.nameToTool[animal.toolUsedForHarvest.Value]))
                ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
            else
                user32121API.CancelPathfinding();
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
            IEnumerable<Character> chars = Game1.currentLocation.characters.Cast<Character>();
            if (Game1.currentLocation is Farm f)
                chars = chars.Concat(f.Animals.Values.AsEnumerable());
            else if (Game1.currentLocation is AnimalHouse ah)
                chars = chars.Concat(ah.Animals.Values.AsEnumerable());
            foreach (Character npc in chars)
                if (npc.getTileLocationPoint() == new Point(x, y) || tileBB.Intersects(npc.GetBoundingBox()))
                    if (npc is FarmAnimal fa)
                    {
                        isAnimal = true;
                        if (ShouldPetAnimal(fa))
                            return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => fa.wasPet.Value);
                        else if (ShouldHarvestAnimal(fa) && fa.getTileLocationPoint() != Game1.player.getTileLocationPoint() && tileBB.Intersects(fa.GetHarvestBoundingBox()) && fa.toolUsedForHarvest.Value != "null")
                            return new TileData(TileData.ACTION.CUSTOM, null, 1, () => fa.currentProduce.Value <= 0, () => HarvestAnimal(fa));
                    }
                    else if (npc is Pet p)
                    {
                        isAnimal = true;
                        if (ShouldPetAnimal(p))
                            return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => !ShouldPetAnimal(p));
                    }
            if (isAnimal)
                return TileData.Passable;

            return user32121API.DefaultIsPassable(x, y);
        }

        private TileData IsPassableIncludeAnimalsAndBuildingDoors(int x, int y, HashSet<Point> buildingDoors)
        {
            if (buildingDoors.Contains(new Point(x, y)))
                return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => false);

            return IsPassableIncludeAnimals(x, y);
        }

        private TileData IsPassableIncludeNPCs(int x, int y)
        {
            Rectangle tileBB = new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
            bool isNPC = false;
            foreach (NPC npc in npcs)
                if (npc.currentLocation == Game1.currentLocation && (npc.getTileLocationPoint() == new Point(x, y) || tileBB.Intersects(npc.GetBoundingBox())))
                {
                    isNPC = true;
                    if (ShouldTalkToNPC(npc))
                        return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => !ShouldTalkToNPC(npc));
                }
            if (isNPC)
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

        private void StopTalkingToNPCs()
        {
            talkingToNPCs = false;
            user32121API.CancelPathfinding();
            ModPatches.ClearKeys();
        }
    }
}
