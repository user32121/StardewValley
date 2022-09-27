using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using User32121Lib;
using Object = StardewValley.Object;

namespace AutoMiner
{
    public class AutoMiner : Mod
    {
        private Config config;

        private IAPI user32121API;

        enum BOT_STATE
        {
            DISABLED,
            VISUAL_ONLY,
            ENABLED,
        }
        private BOT_STATE botState = BOT_STATE.DISABLED;
        private int curTargetTypeIndex;

        List<Regex> regexPatterns = new List<Regex>();
        List<Regex> regexPatternsSpecial = new List<Regex>();
        List<Vector2> targets = new List<Vector2>();
        List<Vector2> targetsSpecial = new List<Vector2>();  //only for visual purposes, needs to be in targets for bot to mine it
        List<Vector2> ladders = new List<Vector2>();

        bool alreadyDisplayedInfestedFloors;

        HUDMessage prevHUDMessage;

        Texture2D blank;

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.World.ObjectListChanged += World_ObjectListChanged;
            helper.Events.World.NpcListChanged += World_NpcListChanged;
        }

        public void DisableMod()
        {
            Helper.Events.Input.ButtonsChanged -= Input_ButtonsChanged;
            Helper.Events.Display.RenderedWorld -= Display_RenderedWorld;
            Helper.Events.GameLoop.UpdateTicked -= GameLoop_UpdateTicked;
            Helper.Events.GameLoop.ReturnedToTitle -= GameLoop_ReturnedToTitle;
            Helper.Events.Player.Warped -= Player_Warped;
            Helper.Events.GameLoop.SaveLoaded -= GameLoop_SaveLoaded;
            Helper.Events.GameLoop.DayStarted -= GameLoop_DayStarted;
            Helper.Events.GameLoop.GameLaunched -= GameLoop_GameLaunched;
            Helper.Events.World.ObjectListChanged -= World_ObjectListChanged;
            Helper.Events.World.NpcListChanged -= World_NpcListChanged;
        }

        private void World_NpcListChanged(object sender, StardewModdingAPI.Events.NpcListChangedEventArgs e)
        {
            if (botState == BOT_STATE.ENABLED)
                user32121API.CancelPathfinding();  //force reevaluate
        }

        private void World_ObjectListChanged(object sender, StardewModdingAPI.Events.ObjectListChangedEventArgs e)
        {
            if (botState == BOT_STATE.ENABLED)
                user32121API.CancelPathfinding();  //force reevaluate
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
            alreadyDisplayedInfestedFloors = false;
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            RecompileRegexPatterns();
        }

        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            if (config.outputInfestedFloors && !alreadyDisplayedInfestedFloors && e.NewLocation is MineShaft ms)
            {
                OutputInfestedFloors(ms);
                alreadyDisplayedInfestedFloors = true;
            }
        }

        private void OutputInfestedFloors(MineShaft ms)
        {
            //code from MineShaft.loadLevel
            //doesn't work for levels > 120 due to relying on MineShaft.mineRandom, which has side effects and changes as other values are loaded
            List<int> slimeAreas = new List<int>();
            List<int> monsterAreas = new List<int>();
            List<int> quarryAreas = new List<int>();

            for (int level = 0; level <= 120; level++)
            {
                bool isSlimeArea, isMonsterArea, isQuarryArea;
                isSlimeArea = isMonsterArea = isQuarryArea = false;

                int num = ((level % 40 % 20 == 0 && level % 40 != 0) ? 20 : ((level % 10 == 0) ? 10 : level));
                num %= 40;
                if (level == 120)
                {
                    num = 120;
                }

                Random random = new Random((int)Game1.stats.DaysPlayed + level * 100 + (int)Game1.uniqueIDForThisGame / 2);
                if (!ms.AnyOnlineFarmerHasBuff(23) && random.NextDouble() < 0.044 && num % 5 != 0 && num % 40 > 5 && num % 40 < 30 && num % 40 != 19)
                {
                    if (random.NextDouble() < 0.5)
                    {
                        isMonsterArea = true;
                    }
                    else
                    {
                        isSlimeArea = true;
                    }
                }
                else if (random.NextDouble() < 0.044 && Utility.doesMasterPlayerHaveMailReceivedButNotMailForTomorrow("ccCraftsRoom") && Game1.MasterPlayer.hasOrWillReceiveMail("VisitedQuarryMine") && num % 40 > 1 && num % 5 != 0)
                {
                    isQuarryArea = true;
                    if (random.NextDouble() < 0.25)
                    {
                        isMonsterArea = true;
                    }
                }

                if (isQuarryArea)
                {
                    isQuarryArea = true;
                    isSlimeArea = false;
                    isMonsterArea = false;
                }

                if (isSlimeArea)
                    slimeAreas.Add(level);
                if (isMonsterArea)
                    monsterAreas.Add(level);
                if (isQuarryArea)
                    quarryAreas.Add(level);
            }
            Monitor.Log("Slime areas: " + string.Join(", ", slimeAreas), LogLevel.Debug);
            Monitor.Log("Monster areas: " + string.Join(", ", monsterAreas), LogLevel.Debug);
            Monitor.Log("Quarry areas: " + string.Join(", ", quarryAreas), LogLevel.Debug);
        }

        private void GetTargets()
        {
            targets.Clear();
            targetsSpecial.Clear();

            if (Game1.currentLocation == null)
                return;

            foreach (Vector2 pos in Game1.currentLocation.objects.Keys)
            {
                Object obj = Game1.currentLocation.objects[pos];

                bool match = false;
                bool matchS = false;
                string str = "";
                if (config.searchName)
                    str += obj.name;
                if (config.searchDescription)
                    str += ";" + obj.getDescription();
                if (config.searchSheetIndex)
                    str += ";" + Utils.objIndexToName.GetValueOrDefault(obj.ParentSheetIndex, "");
                if (config.useRegex)
                {
                    foreach (Regex pattern in regexPatterns)
                        if (pattern.IsMatch(str))
                            match = true;
                    foreach (Regex pattern in regexPatternsSpecial)
                        if (pattern.IsMatch(str))
                            matchS = true;
                }
                else
                {
                    string curTarget = config.mineTargets.ElementAt(curTargetTypeIndex).Key;
                    foreach (string pattern in config.mineTargets[curTarget])
                        if (str.Contains(pattern))
                            match = true;
                    foreach (string pattern in config.mineTargets.GetValueOrDefault(curTarget) ?? new List<string>())
                        if (str.Contains(pattern))
                            matchS = true;
                }
                if (match)
                    targets.Add(pos);
                if (matchS)
                    targetsSpecial.Add(pos);
            }
            bool matchMineRocks = false;
            bool matchMineRocksS = false;
            foreach (Regex pattern in regexPatterns)
                if (pattern.IsMatch("mineRock"))
                    matchMineRocks = true;
            foreach (Regex pattern in regexPatternsSpecial)
                if (pattern.IsMatch("mineRock"))
                    matchMineRocksS = true;
            if (matchMineRocks || matchMineRocksS)
                foreach (ResourceClump rc in Game1.currentLocation.resourceClumps)
                {
                    if (rc.parentSheetIndex.Value == ResourceClump.mineRock1Index ||
                        rc.parentSheetIndex.Value == ResourceClump.mineRock2Index ||
                        rc.parentSheetIndex.Value == ResourceClump.mineRock3Index ||
                        rc.parentSheetIndex.Value == ResourceClump.mineRock4Index)
                    {
                        for (int x2 = 0; x2 < rc.width.Value; x2++)
                            for (int y2 = 0; y2 < rc.height.Value; y2++)
                            {
                                if (matchMineRocks)
                                    targets.Add(new Vector2(rc.tile.X + x2, rc.tile.Y + y2));
                                if (matchMineRocksS)
                                    targetsSpecial.Add(new Vector2(rc.tile.X + x2, rc.tile.Y + y2));
                            }
                    }
                }
        }
        private void GetLadders()
        {
            ladders.Clear();

            if (Game1.currentLocation == null)
                return;

            if (Game1.currentLocation is MineShaft ms)
            {
                bool ladderHasSpawned = Helper.Reflection.GetField<bool>(ms, "ladderHasSpawned").GetValue();
                int stonesLeftOnThisLevel = Helper.Reflection.GetProperty<int>(ms, "stonesLeftOnThisLevel").GetValue();
                double num3 = 0.02 + 1.0 / (double)Math.Max(1, stonesLeftOnThisLevel) + (double)Game1.player.LuckLevel / 100.0 + Game1.player.DailyLuck / 5.0;
                if (ms.EnemyCount == 0)
                    num3 += 0.04;
                Vector2 ladderPos = Helper.Reflection.GetProperty<Vector2>(ms, "tileBeneathLadder").GetValue();

                //already spawned ladders
                xTile.Layers.Layer layer = ms.map.GetLayer("Buildings");
                for (int i = 0; i < layer.Tiles.Array.GetLength(0); i++)
                    for (int j = 0; j < layer.Tiles.Array.GetLength(1); j++)
                    {
                        if (layer.Tiles.Array[i, j] == null)
                            continue;

                        //173: ladder, 174: shaft
                        if (layer.Tiles.Array[i, j].TileIndex == 173 || layer.Tiles.Array[i, j].TileIndex == 174)
                            ladders.Add(new Vector2(i, j));
                    }

                //potential ladders
                foreach (Vector2 pos in ms.Objects.Keys)
                {
                    if (ms.Objects[pos].Name == "Stone")
                    {
                        //code from MineShaft.checkStoneForItems
                        Random random = new Random((int)pos.X * 1000 + (int)pos.Y + ms.mineLevel + (int)Game1.uniqueIDForThisGame / 2);
                        random.NextDouble();
                        if (!ladderHasSpawned && !ms.mustKillAllMonstersToAdvance() && (stonesLeftOnThisLevel == 0 || random.NextDouble() < num3) && ms.shouldCreateLadderOnThisLevel())
                        {
                            ladders.Add(pos);
                        }
                    }
                }
            }
        }

        private void GameLoop_ReturnedToTitle(object sender, StardewModdingAPI.Events.ReturnedToTitleEventArgs e)
        {
            botState = BOT_STATE.DISABLED;
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (!Game1.player.IsLocalPlayer)
                return;
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            if (botState == BOT_STATE.ENABLED)
            {
                if (!Game1.player.UsingTool && !Game1.player.isEating)
                {
                    if (Game1.player.Stamina < 10)
                    {
                        Game1.addHUDMessage(new HUDMessage("Low stamina", 3));
                        StopBot();
                        goto DONE_TASK_TICK;
                    }

                    if (!user32121API.HasPath())
                    {
                        //find next target
                        GetTargets();
                        GetLadders();
                        MineShaft ms = Game1.currentLocation as MineShaft;
                        bool includeEnemies = ms != null && ladders.Count == 0;
                        bool flyingEnemies = false;
                        if (includeEnemies)
                            foreach (NPC npc in ms.characters)
                                if (npc is Monster mon && mon.isGlider.Value)
                                    flyingEnemies = true;

                        user32121API.Pathfind((int x, int y) => targets.Contains(new Vector2(x, y)), isPassable: user32121API.DefaultIsPassableWithMining, quiet: config.autoDescend);
                        if (!user32121API.HasPath())
                        {
                            if (config.autoDescend)
                            {
                                IEnumerable<Monster> monsters = Game1.currentLocation.characters.OfType<Monster>();
                                user32121API.Pathfind((int x, int y) => ladders.Contains(new Vector2(x, y)), isPassable: (x, y) => IsPassableIncludeLaddersAndMonsters(x, y, monsters), quiet: includeEnemies);

                                if (!user32121API.HasPath() && includeEnemies)
                                {
                                    IEnumerable<Vector2> monPositions = monsters.Select((mon) => mon.getTileLocation());
                                    user32121API.Pathfind((int x, int y) => monPositions.Contains(new Vector2(x, y)), isPassable: (x, y) => IsPassableIncludeMonsters(x, y, monsters));
                                }
                                if (!user32121API.HasPath() && (!includeEnemies || !flyingEnemies))
                                {
                                    Game1.addHUDMessage(new HUDMessage("Can't reach targets or ladders", 3));
                                    StopBot();
                                }
                            }
                            else
                            {
                                if (targets.Count == 0)
                                {
                                    Game1.addHUDMessage(new HUDMessage("No more targets", 3));
                                    StopBot();
                                }
                                else
                                {
                                    Game1.addHUDMessage(new HUDMessage("Can't reach targets", 3));
                                    StopBot();
                                }
                            }
                        }
                    }
                }
            }
        DONE_TASK_TICK:;
        }

        private int GetPlayerToolIndex(Type toolType)
        {
            if (toolType == null)
                return -1;  //not found
            for (int i = 0; i < Game1.player.Items.Count; i++)
                if (Game1.player.Items[i]?.GetType() == toolType)
                    return i;
            return -1;
        }

        private void StopBot()
        {
            SetHUDMessage("Bot disabled");
            botState = BOT_STATE.DISABLED;
            user32121API.CancelPathfinding();
        }

        public TileData IsPassableIncludeMonsters(int x, int y, IEnumerable<Monster> monsters)
        {
            TileData res = user32121API.DefaultIsPassableWithMining(x, y);

            if (!res.IsPassable)
            {
                Rectangle tileBB = new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
                foreach (Monster mon in monsters)
                    if (tileBB.Intersects(mon.GetBoundingBox()))
                    {
                        if (mon is RockCrab rc && Helper.Reflection.GetField<bool>(rc, "waiter").GetValue())
                            return new TileData(TileData.ACTION.USETOOLBUTTON, typeof(Pickaxe), 1, () => mon.Health <= 0);
                        else
                            return new TileData(TileData.ACTION.USETOOL, typeof(MeleeWeapon), 1, () => mon.Health <= 0);
                    }
            }

            return res;
        }

        public TileData IsPassableIncludeLaddersAndMonsters(int x, int y, IEnumerable<Monster> monsters)
        {
            TileData res = IsPassableIncludeMonsters(x, y, monsters);
            if (!res.IsPassable && ladders.Contains(new Vector2(x, y)))
                return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => false);
            return res;
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            if (botState != BOT_STATE.DISABLED)
            {
                //highlight targets
                foreach (Vector2 pos in targets)
                {
                    int screenX = (int)(pos.X * Game1.tileSize - Game1.viewport.X);
                    int screenY = (int)(pos.Y * Game1.tileSize - Game1.viewport.Y);
                    //top
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.targetColor);
                    //bottom
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.targetColor);
                    //left
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.targetColor);
                    //right
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.targetColor);
                }

                //highlight special targets
                foreach (Vector2 pos in targetsSpecial)
                {
                    int screenX = (int)(pos.X * Game1.tileSize - Game1.viewport.X);
                    int screenY = (int)(pos.Y * Game1.tileSize - Game1.viewport.Y);
                    //top
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.specialColor);
                    //bottom
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.specialColor);
                    //left
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.specialColor);
                    //right
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.specialColor);
                    //line to player
                    Vector2 playerScreenPos = (Game1.player.Position - new Vector2(Game1.viewport.X, Game1.viewport.Y));
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize / 2, screenY + Game1.tileSize / 2, (int)(playerScreenPos - new Vector2(screenX, screenY)).Length(), config.highlightThickness), null, config.specialColor, MathF.Atan2(playerScreenPos.Y - screenY, playerScreenPos.X - screenX), Vector2.Zero, SpriteEffects.None, 0);
                }

                //highlight ladders
                foreach (Vector2 pos in ladders)
                {
                    int screenX = (int)(pos.X * Game1.tileSize - Game1.viewport.X);
                    int screenY = (int)(pos.Y * Game1.tileSize - Game1.viewport.Y);
                    //top
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.ladderColor);
                    //bottom
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.ladderColor);
                    //left
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.ladderColor);
                    //right
                    e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.ladderColor);
                }

                //Monster.HasSpecialItem
                if (config.highlightHasSpecialItem)
                    foreach (Monster mon in Game1.currentLocation.characters.OfType<Monster>())
                    {
                        Color? col = null;

                        if (mon.hasSpecialItem.Value)
                            col = config.specialColor;
                        else if (config.highlightCrab && (mon is RockCrab || mon is LavaCrab) || config.highlightDuggy && mon is Duggy || config.highlightStoneGolem && mon is RockGolem)
                            col = config.enemyColor;
                        if (col.HasValue)
                        {
                            int screenX = (int)(mon.Position.X - Game1.viewport.X);
                            int screenY = (int)(mon.Position.Y - Game1.viewport.Y);
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

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (config.reloadConfig.JustPressed())
                ReloadConfig();

            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            //"observe"
            if (e.Pressed.Contains(SButton.O))
            {
                Monitor.Log(Game1.currentLocation.ToString(), LogLevel.Debug);
            }

            if (config.startBot.JustPressed())
            {
                if (botState == BOT_STATE.DISABLED)
                {
                    botState = BOT_STATE.VISUAL_ONLY;
                    SetHUDMessage("Visuals only");
                }
                else if (botState == BOT_STATE.VISUAL_ONLY)
                {
                    botState = BOT_STATE.ENABLED;
                    SetHUDMessage("Bot enabled");
                }
                else
                {
                    botState = BOT_STATE.DISABLED;
                    StopBot();
                }
            }
            if (config.cycleTarget.JustPressed())
            {
                curTargetTypeIndex = (curTargetTypeIndex + 1) % config.mineTargets.Count;
                RecompileRegexPatterns();
                SetHUDMessage("Now targeting " + config.mineTargets.ElementAt(curTargetTypeIndex).Key);
            }
        }

        private void RecompileRegexPatterns()
        {
            if (!config.useRegex)
                return;

            regexPatterns.Clear();
            regexPatternsSpecial.Clear();
            RegexOptions ro = RegexOptions.Compiled;
            if (config.ignoreCase)
                ro |= RegexOptions.IgnoreCase;

            string curTarget = config.mineTargets.ElementAt(curTargetTypeIndex).Key;

            foreach (string searchPattern in config.mineTargets[curTarget])
                try
                {
                    regexPatterns.Add(new Regex(searchPattern, ro));
                }
                catch (RegexParseException rpe)
                {
                    Monitor.Log(rpe.Message, LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage(rpe.Message, 3));
                }
            foreach (string searchPattern in config.mineSpecialTargets.GetValueOrDefault(curTarget) ?? new List<string>())
                try
                {
                    regexPatternsSpecial.Add(new Regex(searchPattern, ro));
                }
                catch (RegexParseException rpe)
                {
                    Monitor.Log(rpe.Message, LogLevel.Warn);
                    Game1.addHUDMessage(new HUDMessage(rpe.Message, 3));
                }

            GetTargets();
        }

        private void SetHUDMessage(string message)
        {
            if (prevHUDMessage != null)
            {
                prevHUDMessage.transparency = 0;
                prevHUDMessage.timeLeft = 0;
            }
            Game1.addHUDMessage(prevHUDMessage = new HUDMessage(message, 2));
        }

        private void ReloadConfig()
        {
            config = Helper.ReadConfig<Config>();
            Helper.WriteConfig(config);
            Monitor.Log("Loaded Config", LogLevel.Info);
            RecompileRegexPatterns();
            if (!config.enabled)
                DisableMod();
        }
    }
}
