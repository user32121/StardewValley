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
using Object = StardewValley.Object;

namespace AutoMiner
{
    public class AutoMiner : Mod
    {
        private Config config;

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

        Vector2? target;
        List<Vector2> path;
        int pathIndex;

        bool justWarped;

        bool prevPlayerUsingTool;

        HUDMessage prevHUDMessage;

        Texture2D blank;

        private Stopwatch stopwatch = new Stopwatch();

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            Utils.Initialize(this);

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            helper.Events.GameLoop.DayStarted += GameLoop_DayStarted;
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
            GetTargets(e.NewLocation);
            GetLadders(e.NewLocation);
            if (config.outputInfestedFloors && !alreadyDisplayedInfestedFloors && e.NewLocation is MineShaft ms)
            {
                OutputInfestedFloors(ms);
                alreadyDisplayedInfestedFloors = true;
            }
            justWarped = true;
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

        private void GetTargets(GameLocation location = null)
        {
            location ??= Game1.currentLocation;

            targets.Clear();
            targetsSpecial.Clear();

            if (location == null)
                return;

            foreach (Vector2 pos in location.objects.Keys)
            {
                Object obj = location.objects[pos];

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
                foreach (ResourceClump rc in location.resourceClumps)
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

            if (target != null && !targets.Contains(target.Value) && !ladders.Contains(target.Value))
                target = null;
        }
        private void GetLadders(GameLocation location = null)
        {
            location ??= Game1.currentLocation;

            ladders.Clear();

            if (location == null)
                return;

            if (location is MineShaft ms)
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

            if (e.IsOneSecond)
            {
                target = null;
                GetTargets();
                GetLadders();
            }

            if (config.spamAttack.IsDown() && !Game1.player.UsingTool)
            {
                if (Game1.player.CurrentTool is MeleeWeapon)
                    Game1.player.BeginUsingTool();
                else
                    ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
            }

            if (botState == BOT_STATE.ENABLED)
            {
                if (!Game1.player.UsingTool && prevPlayerUsingTool)
                {
                    GetTargets();
                    GetLadders();
                }
                prevPlayerUsingTool = Game1.player.UsingTool;

                if (!Game1.player.UsingTool)
                {
                    //combat
                    Vector2 nearestMonster = Vector2.Zero;
                    double nearestMonsterDistanceSqr = double.MaxValue;
                    foreach (Monster mon in Game1.currentLocation.characters.OfType<Monster>())
                    {
                        double distSqr = (mon.Position - Game1.player.Position).LengthSquared();
                        if (distSqr < nearestMonsterDistanceSqr)
                        {
                            nearestMonsterDistanceSqr = distSqr;
                            nearestMonster = mon.Position;
                        }
                    }
                    if (nearestMonsterDistanceSqr < Game1.tileSize * Game1.tileSize * 2)
                    {
                        Vector2 monsterTile = new Vector2((int)(nearestMonster.X / Game1.tileSize), (int)(nearestMonster.Y / Game1.tileSize));

                        int toolIndex = GetPlayerToolIndex(typeof(MeleeWeapon));
                        if (toolIndex == -1)
                            SetHUDMessage("no weapon");
                        else
                            Game1.player.CurrentToolIndex = toolIndex;


                        if (Game1.currentCursorTile != monsterTile)
                        {
                            //face right direction
                            Game1.player.faceGeneralDirection(monsterTile * Game1.tileSize);
                            int screenX = (int)(((monsterTile.X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                            int screenY = (int)(((monsterTile.Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                            Game1.input.SetMousePosition(screenX, screenY);
                        }
                        else
                        {
                            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
                            Game1.player.BeginUsingTool();
                            goto DONE_TASK_TICK;
                        }
                    }

                    if (Game1.player.Stamina < 10)
                    {
                        Game1.addHUDMessage(new HUDMessage("Low stamina", 3));
                        StopBot();
                        goto DONE_TASK_TICK;
                    }

                    if (target.HasValue && path != null)
                    {
                        //travel to target
                        if ((Game1.player.getTileLocation() - target.Value).LengthSquared() <= 2 || pathIndex >= path.Count)
                        {
                            //reached destination, stop moving
                            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);

                            //switch to tool
                            Type taskTool = typeof(MeleeWeapon);
                            Object targetObj = Game1.currentLocation.getObjectAtTile((int)target.Value.X, (int)target.Value.Y);
                            if (targetObj != null)
                                if (targetObj is BreakableContainer || targetObj.name == "Weeds")
                                    taskTool = typeof(MeleeWeapon);
                                else if (targetObj.name == "Stone")
                                    taskTool = typeof(Pickaxe);
                                else
                                    taskTool = typeof(HandToolPlaceholder);

                            xTile.Layers.Layer layer = (Game1.currentLocation as MineShaft)?.map.GetLayer("Buildings");
                            if (layer != null && (layer.Tiles.Array[(int)target.Value.X, (int)target.Value.Y]?.TileIndex == 173 || layer.Tiles.Array[(int)target.Value.X, (int)target.Value.Y]?.TileIndex == 174))
                                taskTool = typeof(HandToolPlaceholder);

                            int toolIndex = GetPlayerToolIndex(taskTool);
                            if (toolIndex == -1)
                            {
                                Game1.addHUDMessage(new HUDMessage("Unable to switch to " + (taskTool?.Name ?? "null") + " tool", 3));
                                StopBot();
                                goto DONE_TASK_TICK;
                            }
                            else if (toolIndex == -2)
                            {
                                //NO OP
                            }
                            else
                            {
                                Game1.player.CurrentToolIndex = toolIndex;
                            }

                            if (Game1.currentCursorTile != target)
                            {
                                //face right direction
                                if (taskTool == typeof(MeleeWeapon))
                                {
                                    Game1.player.faceGeneralDirection(target.Value * Game1.tileSize);
                                }
                                int screenX = (int)(((target.Value.X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                                int screenY = (int)(((target.Value.Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                                Game1.input.SetMousePosition(screenX, screenY);
                            }
                            else
                            {
                                //use tool
                                if (taskTool == typeof(MeleeWeapon))
                                    Game1.player.BeginUsingTool();
                                else if (taskTool == typeof(HandToolPlaceholder))
                                    ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                                else
                                    ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
                                target = null;
                            }
                        }
                        else
                        {
                            //travel along path
                            if (path[pathIndex] == Game1.player.getTileLocation())
                                pathIndex++;
                            else
                            {
                                Object obj = Game1.currentLocation.getObjectAtTile((int)path[pathIndex].X, (int)path[pathIndex].Y);
                                ResourceClump rc = null;
                                foreach (ResourceClump item in Game1.currentLocation.resourceClumps)
                                    if (item.occupiesTile((int)path[pathIndex].X, (int)path[pathIndex].Y))
                                        rc = item;

                                if (obj != null || rc != null)
                                {
                                    //use tool
                                    Type taskTool = typeof(HandToolPlaceholder);
                                    if (obj != null)
                                    {
                                        if (obj is BreakableContainer)
                                            taskTool = typeof(MeleeWeapon);
                                        else if (obj.name == "Weeds")
                                            taskTool = typeof(MeleeWeapon);
                                        else if (obj.name == "Stone")
                                            taskTool = typeof(Pickaxe);
                                    }
                                    else if (rc != null)
                                    {
                                        if (rc.parentSheetIndex.Value == ResourceClump.mineRock1Index || rc.parentSheetIndex.Value == ResourceClump.mineRock2Index || rc.parentSheetIndex.Value == ResourceClump.mineRock3Index || rc.parentSheetIndex.Value == ResourceClump.mineRock4Index)
                                            taskTool = typeof(Pickaxe);
                                    }

                                    int index = GetPlayerToolIndex(taskTool);
                                    if (index == -1)
                                    {
                                        Game1.addHUDMessage(new HUDMessage("Unable to switch to " + (taskTool?.Name ?? "null") + " tool", 3));
                                        StopBot();
                                        goto DONE_TASK_TICK;
                                    }
                                    else if (index != -2)
                                        Game1.player.CurrentToolIndex = index;

                                    if (Game1.currentCursorTile != path[pathIndex])
                                    {
                                        //face right direction
                                        if (taskTool == typeof(MeleeWeapon))
                                        {
                                            Game1.player.faceGeneralDirection(path[pathIndex] * Game1.tileSize);
                                        }
                                        int screenX = (int)(((path[pathIndex].X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                                        int screenY = (int)(((path[pathIndex].Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                                        Game1.input.SetMousePosition(screenX, screenY);
                                    }
                                    else
                                    {
                                        //use tool
                                        if (taskTool == typeof(MeleeWeapon))
                                            Game1.player.BeginUsingTool();
                                        else if (taskTool == typeof(HandToolPlaceholder))
                                            ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                                        else
                                            ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
                                    }
                                }
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
                    }
                    else
                    {
                        //find next target
                        MineShaft ms = Game1.currentLocation as MineShaft;
                        bool includeEnemies = ms != null ? (/*ms.mustKillAllMonstersToAdvance() &&*/ ladders.Count == 0) : false;
                        bool flyingEnemies = false;
                        if (includeEnemies)
                            foreach (NPC npc in ms.characters)
                                if (npc is Monster mon && mon.isGlider.Value)
                                    flyingEnemies = true;

                        FindNextTarget();
                        if (!target.HasValue)
                        {
                            if (config.autoDescend)
                            {
                                FindNextTarget(true);
                                if (!target.HasValue && includeEnemies)
                                    FindNextTarget(true, includeEnemies: includeEnemies);
                                if (!target.HasValue && (!includeEnemies || !flyingEnemies))
                                {
                                    if (!justWarped)
                                    {
                                        Game1.addHUDMessage(new HUDMessage("Can't reach targets or ladders", 3));
                                        StopBot();
                                    }
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
            justWarped = true;
        }

        private int GetPlayerToolIndex(Type toolType)
        {
            if (toolType == null)
                return -1;  //not found
            if (toolType == typeof(HandToolPlaceholder))
                return -2;  //no tool needed, don't change slots
            for (int i = 0; i < Game1.player.Items.Count; i++)
                if (Game1.player.Items[i]?.GetType() == toolType)
                    return i;
            return -1;
        }

        private void StopBot()
        {
            SetHUDMessage("Bot disabled");
            botState = BOT_STATE.DISABLED;
            target = null;
            path = null;
            pathIndex = 0;
            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
        }

        private Point? Bfs(Point start, out Point[,] prevTileMap, bool includeLadders = false, bool includeEnemies = false)
        {
            xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;

            bool[,] targetGraph = new bool[size.Width, size.Height];

            try
            {
                foreach (Vector2 pos in targets)
                    targetGraph[(int)pos.X, (int)pos.Y] = true;
                if (includeLadders)
                    foreach (Vector2 pos in ladders)
                        targetGraph[(int)pos.X, (int)pos.Y] = true;
                if (includeEnemies)
                    foreach (NPC npc in Game1.currentLocation.characters)
                    {
                        if (npc is Monster)
                            targetGraph[(int)npc.Position.X / 64, (int)npc.position.Y / 64] = true;
                    }
            }
            catch (IndexOutOfRangeException)
            {
                Monitor.Log("location desync", LogLevel.Debug);
            }

            int[,] costGraph = new int[size.Width, size.Height];
            for (int x = 0; x < costGraph.GetLength(0); x++)
                for (int y = 0; y < costGraph.GetLength(1); y++)
                    costGraph[x, y] = (Game1.currentLocation.isObjectAtTile(x, y) || Game1.currentLocation.isTileOccupied(new Vector2(x, y))) ? config.bfsBreakCost : 1;

            return Utils.Bfs(size, Game1.player.getTileLocationPoint(), (int x, int y) => targetGraph[x, y], IsTilePassable, out prevTileMap, (int x, int y) => costGraph[x, y]);
        }

        private bool? IsTilePassable(int x, int y)
        {
            //true: can pass, null: needs to break things before passing, false: cannot pass
            if (Game1.currentLocation.isObjectAtTile(x, y) || Game1.currentLocation.isTileOccupied(new Vector2(x, y)))
                return null;
            else
                return !Game1.currentLocation.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player);
        }

        private void FindNextTarget(bool includeLadders = false, bool includeEnemies = false)
        {
            if (targets.Count == 0 && (!includeLadders || ladders.Count == 0) && (!includeEnemies || Game1.currentLocation is not MineShaft ms || ms.EnemyCount == 0))
                return;

            Vector2 playerPos = Game1.player.getTileLocation();

            target = null;

            //find closest target using bfs (maybe implement dijkstra later but need to implement priorty queue)
            target = Bfs(playerPos.ToPoint(), out Point[,] traversalMap, includeLadders, includeEnemies)?.ToVector2();

            if (target == null)
            {
                //targets unreachable
                if (!justWarped)
                    Game1.addHUDMessage(new HUDMessage("Unable to reach any tasks", 3));
            }
            else
            {
                path = Utils.GeneratePathToTarget(playerPos.ToPoint(), target.Value.ToPoint(), traversalMap);
                pathIndex = 0;
            }
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
                    foreach (NPC npc in Game1.currentLocation.characters)
                        if (npc is Monster mon && mon.hasSpecialItem.Value)
                        {
                            int screenX = (int)(mon.Position.X - Game1.viewport.X);
                            int screenY = (int)(mon.Position.Y - Game1.viewport.Y);
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
        }
    }
}
