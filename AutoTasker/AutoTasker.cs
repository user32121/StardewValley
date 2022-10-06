using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoTasker
{
    public class AutoTasker : StardewModdingAPI.Mod
    {
        //graphics assets
        Texture2D blank;
        Dictionary<Task.TASK_TYPE, Color> taskColors;

        //planner
        bool renderPlannerOverlay;
        Task.TASK_TYPE[] validPlannerTasks = new Task.TASK_TYPE[] {
            Task.TASK_TYPE.CLEAR_DEBRIS,
            Task.TASK_TYPE.CLEAR_TREES,
            Task.TASK_TYPE.TILL,
            Task.TASK_TYPE.WATER,
            Task.TASK_TYPE.DIG_ARTIFACTS,
            Task.TASK_TYPE.HARVEST_CROPS,
            Task.TASK_TYPE.FORAGE,
            Task.TASK_TYPE.TRAVEL_TO_TARGET,
            Task.TASK_TYPE.EASTER_EVENT,
        };
        int curPlannerTaskIndex;
        List<Task> tasks;
        Task[,] tileTasks;  //stores backreferences
        bool[,] tileTasksToRemove;
        HUDMessage prevTaskTypeMessage;

        Config config;
        bool modActive;

        bool isLeftMouseDown;
        bool isRightMouseDown;
        Vector2 mouseLeftDownPos;
        Vector2 mouseRightDownPos;
        Vector2 curMousePos;

        bool isRunningTasks;
        Vector2? curTaskTarget;
        Type taskTool;
        List<Vector2> path;
        int pathIndex;

        bool stopUsingTool;

        Stopwatch stopwatch = new Stopwatch();  //for detecting infinite loops, prefer killing bot over game freeze

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            Helper.Events.Input.CursorMoved += Input_CursorMoved;
            Helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            Helper.Events.Player.Warped += Player_Warped;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            Helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
        }

        private void GameLoop_ReturnedToTitle(object sender, StardewModdingAPI.Events.ReturnedToTitleEventArgs e)
        {
            SetIsRunningTasks(false, false);
            renderPlannerOverlay = false;
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (!Game1.player.IsLocalPlayer)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            if (e.IsOneSecond)
            {
                ValidateTasks();
                if (!isRunningTasks)
                    ModPatches.ClearKeys();
            }

            if (isRunningTasks && Game1.player.UsingTool && stopUsingTool)
            {
                stopUsingTool = false;
                Game1.player.EndUsingTool();
                //Game1.player.toolPower
            }

            if (isRunningTasks && tasks != null && !Game1.player.UsingTool)
            {
                ValidateTasks();

                if (Game1.player.Stamina < 10)
                {
                    Game1.addHUDMessage(new HUDMessage("Low stamina", 3));
                    SetIsRunningTasks(false, false);
                    goto DONE_TASK_TICK;
                }

                int toolIndex = GetPlayerToolIndex(taskTool);
                //check water can capacity
                if (taskTool == typeof(WateringCan) && toolIndex >= 0 && Game1.player.Items[toolIndex] is WateringCan wc)
                {
                    if (wc.WaterLeft <= 0)
                    {
                        //goto to nearest pond
                        curTaskTarget = Bfs(Game1.player.getTileLocationPoint(), Game1.currentLocation.CanRefillWateringCanOnTile, out int[,] traversalMap)?.ToVector2();
                        if (curTaskTarget == null)
                        {
                            //water unreachable
                            Game1.addHUDMessage(new HUDMessage("Unable to reach any water", 3));
                        }
                        else
                        {
                            path = generatePathToTarget(Game1.player.getTileLocationPoint(), curTaskTarget.Value.ToPoint(), traversalMap);
                            tasks.Add(new Task() { type = Task.TASK_TYPE.REFILL_WATER, tiles = new List<Vector2>() { curTaskTarget.Value } });
                            ValidateTasks();
                        }
                    }
                }

                if (curTaskTarget == null || tileTasks[(int)curTaskTarget.Value.X, (int)curTaskTarget.Value.Y] == null)
                {
                    curTaskTarget = null;

                    GetNewTaskTarget();
                    //no path found or no targets found
                    if (curTaskTarget == null)
                    {
                        Game1.addHUDMessage(new HUDMessage("Done with tasks", 1));
                        SetIsRunningTasks(false, false);
                        goto DONE_TASK_TICK;
                    }
                    else
                    {
                        pathIndex = 0;

                        //check if has tool
                        Point p = curTaskTarget.Value.ToPoint();
                        if (tileTasks[p.X, p.Y] == null)
                            taskTool = null;
                        else
                            switch (tileTasks[p.X, p.Y].type)
                            {
                                case Task.TASK_TYPE.CLEAR_DEBRIS:
                                    string objName = Game1.currentLocation.getObjectAtTile(p.X, p.Y)?.name;
                                    if (objName != null)
                                    {
                                        if (objName == "Weeds")
                                            taskTool = typeof(MeleeWeapon);
                                        else if (objName == "Stone")
                                            taskTool = typeof(Pickaxe);
                                        else if (objName == "Twig")
                                            taskTool = typeof(Axe);
                                    }
                                    else if (Game1.currentLocation.terrainFeatures.TryGetValue(curTaskTarget.Value, out TerrainFeature feature) && feature is Grass)
                                        taskTool = typeof(MeleeWeapon);
                                    else
                                    {
                                        taskTool = null;
                                        foreach (var item in Game1.currentLocation.resourceClumps)
                                            if (p.X >= item.tile.X && p.Y >= item.tile.Y && p.X < item.tile.X + item.width.Value && p.Y < item.tile.Y + item.height.Value)
                                                if (item.parentSheetIndex.Value == ResourceClump.stumpIndex || item.parentSheetIndex.Value == ResourceClump.hollowLogIndex)
                                                    taskTool = typeof(Axe);
                                                else
                                                    taskTool = typeof(Pickaxe);
                                    }
                                    break;
                                case Task.TASK_TYPE.CLEAR_TREES:
                                    taskTool = typeof(Axe);
                                    break;
                                case Task.TASK_TYPE.TILL:
                                case Task.TASK_TYPE.DIG_ARTIFACTS:
                                    taskTool = typeof(Hoe);
                                    break;
                                case Task.TASK_TYPE.WATER:
                                case Task.TASK_TYPE.REFILL_WATER:
                                    taskTool = typeof(WateringCan);
                                    break;
                                case Task.TASK_TYPE.HARVEST_CROPS:
                                case Task.TASK_TYPE.FORAGE:
                                case Task.TASK_TYPE.TRAVEL_TO_TARGET:
                                case Task.TASK_TYPE.EASTER_EVENT:
                                    taskTool = typeof(HandToolPlaceholder);
                                    break;
                                default:
                                    taskTool = null;
                                    break;
                            }

                        if (GetPlayerToolIndex(taskTool) == -1)
                        {
                            Game1.addHUDMessage(new HUDMessage("Unable to find " + (taskTool?.Name ?? "null") + " tool", 3));
                            SetIsRunningTasks(false, false);
                        }
                    }
                }
                else if ((Game1.player.getTileLocation() - curTaskTarget.Value).LengthSquared() <= (tileTasks[(int)curTaskTarget.Value.X, (int)curTaskTarget.Value.Y].type == Task.TASK_TYPE.TRAVEL_TO_TARGET ? 0 : (taskTool == typeof(MeleeWeapon) ? 1 : 2)) ||
                        pathIndex >= path.Count)
                {
                    //reached destination, stop moving
                    ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);

                    //switch to tool
                    if (toolIndex == -1)
                    {
                        Game1.addHUDMessage(new HUDMessage("Unable to switch to " + (taskTool?.Name ?? "null") + " tool", 3));
                        SetIsRunningTasks(false, false);
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

                    if (Game1.currentCursorTile != curTaskTarget)
                    {
                        //face right direction
                        if (taskTool == typeof(MeleeWeapon))
                        {
                            Game1.player.faceGeneralDirection(curTaskTarget.Value * Game1.tileSize);
                        }
                        int screenX = (int)(((curTaskTarget.Value.X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                        int screenY = (int)(((curTaskTarget.Value.Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                        Game1.input.SetMousePosition(screenX, screenY);
                    }
                    else
                    {
                        //use tool
                        if (taskTool == typeof(MeleeWeapon))
                            Game1.player.BeginUsingTool();
                        else if (taskTool == typeof(WateringCan))
                        {
                            ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
                            stopUsingTool = true;
                        }
                        else if (taskTool == typeof(Hoe))
                        {
                            ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
                            stopUsingTool = true;
                        }
                        else if (taskTool == typeof(HandToolPlaceholder))
                            ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                        else
                            ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
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
        DONE_TASK_TICK:;
        }

        private bool TileHasTask(int x, int y)
        {
            if (x < 0 || y < 0 || x >= tileTasks.GetLength(0) || y >= tileTasks.GetLength(1))
                return false;
            return tileTasks[x, y] != null && (tileTasks[x, y].type != Task.TASK_TYPE.TILL || Game1.player.getTileLocationPoint() != new Point(x, y));
        }

        private Point? Bfs(Point start, Func<int, int, bool> isTileTarget, out int[,] traversalMap)
        {
            xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;
            traversalMap = new int[size.Width, size.Height];
            for (int x = 0; x < traversalMap.GetLength(0); x++)
                for (int y = 0; y < traversalMap.GetLength(1); y++)
                    traversalMap[x, y] = int.MaxValue;
            Queue<Point> toProcess = new Queue<Point>();
            toProcess.Enqueue(start);
            traversalMap[start.X, start.Y] = 0;

            curTaskTarget = null;
            int i = 0;
            stopwatch.Restart();
            while (toProcess.Count > 0)
            {
                i++;

                if (stopwatch.Elapsed.TotalSeconds > 5)
                {
                    Monitor.Log("Bfs took longer than 5 seconds, killing process", LogLevel.Error);
                    break;
                }

                Point p = toProcess.Dequeue();
                for (int x2 = Math.Max(p.X - 1, 0); x2 <= Math.Min(p.X + 1, traversalMap.GetLength(0)); x2++)
                    for (int y2 = Math.Max(p.Y - 1, 0); y2 <= Math.Min(p.Y + 1, traversalMap.GetLength(1)); y2++)
                        if (isTileTarget(x2, y2) && (tileTasks[x2, y2] == null || tileTasks[x2, y2].type != Task.TASK_TYPE.TRAVEL_TO_TARGET || p == new Point(x2, y2)))
                        {
                            //found a target
                            Monitor.VerboseLog("bfs ran " + i + " iterations");
                            return new Point(x2, y2);
                        }

                //down
                if (p.Y < traversalMap.GetLength(1) - 1 && (traversalMap[p.X, p.Y + 1] == int.MaxValue && IsTilePassable(p.X, p.Y + 1)))
                {
                    traversalMap[p.X, p.Y + 1] = traversalMap[p.X, p.Y] + 1;
                    toProcess.Enqueue(new Point(p.X, p.Y + 1));
                }
                //up
                if (p.Y > 0 && (traversalMap[p.X, p.Y - 1] == int.MaxValue && IsTilePassable(p.X, p.Y - 1)))
                {
                    traversalMap[p.X, p.Y - 1] = traversalMap[p.X, p.Y] + 1;
                    toProcess.Enqueue(new Point(p.X, p.Y - 1));
                }
                //right
                if (p.X < traversalMap.GetLength(0) - 1 && (traversalMap[p.X + 1, p.Y] == int.MaxValue && IsTilePassable(p.X + 1, p.Y)))
                {
                    traversalMap[p.X + 1, p.Y] = traversalMap[p.X, p.Y] + 1;
                    toProcess.Enqueue(new Point(p.X + 1, p.Y));
                }
                //left
                if (p.X > 0 && (traversalMap[p.X - 1, p.Y] == int.MaxValue && IsTilePassable(p.X - 1, p.Y)))
                {
                    traversalMap[p.X - 1, p.Y] = traversalMap[p.X, p.Y] + 1;
                    toProcess.Enqueue(new Point(p.X - 1, p.Y));
                }
            }
            Monitor.VerboseLog("bfs ran " + i + " iterations");
            stopwatch.Stop();
            return null;
        }

        private List<Vector2> generatePathToTarget(Point start, Point target, int[,] traversalMap)
        {
            //traverses graph backwards from target to start using depth values generated in Bfs()

            List<Vector2> result = new List<Vector2>();

            Point pos = target;
            stopwatch.Start();

            //find optimal neighbor tile to travel to
            int minTargetDepth = traversalMap[pos.X, pos.Y];
            for (int x2 = Math.Max(target.X - 1, 0); x2 <= Math.Min(target.X + 1, traversalMap.GetLength(0) - 1); x2++)
                for (int y2 = Math.Max(target.Y - 1, 0); y2 <= Math.Min(target.Y + 1, traversalMap.GetLength(1) - 1); y2++)
                    if (traversalMap[x2, y2] < minTargetDepth)
                    {
                        result.Add(pos.ToVector2());
                        pos = new Point(x2, y2);
                        minTargetDepth = traversalMap[x2, y2];
                    }

            while (pos != start)
            {
                if (stopwatch.Elapsed.TotalSeconds > 5)
                {
                    Monitor.Log("Path construction took longer than 5 seconds, killing process", LogLevel.Error);
                    result.Clear();
                    break;
                }

                result.Add(pos.ToVector2());

                int minDepth = traversalMap[(int)pos.X, (int)pos.Y];
                int curDepth = minDepth;
                int dirX = 0, dirY = 0;
                if (pos.X > 0 && traversalMap[pos.X - 1, pos.Y] <= minDepth && traversalMap[pos.X - 1, pos.Y] < curDepth)
                {
                    dirX = -1;
                    if (traversalMap[pos.X - 1, pos.Y] < minDepth)
                        dirY = 0;
                    minDepth = traversalMap[pos.X - 1, pos.Y];
                }
                if (pos.Y > 0 && traversalMap[pos.X, pos.Y - 1] <= minDepth && traversalMap[pos.X, pos.Y - 1] < curDepth)
                {
                    dirY = -1;
                    if (traversalMap[pos.X, pos.Y - 1] < minDepth)
                        dirX = 0;
                    minDepth = traversalMap[pos.X, pos.Y - 1];
                }
                if (pos.X < traversalMap.GetLength(0) - 1 && traversalMap[pos.X + 1, pos.Y] <= minDepth && traversalMap[pos.X + 1, pos.Y] < curDepth)
                {
                    dirX = 1;
                    if (traversalMap[pos.X + 1, pos.Y] < minDepth)
                        dirY = 0;
                    minDepth = traversalMap[pos.X + 1, pos.Y];
                }
                if (pos.Y < traversalMap.GetLength(1) - 1 && traversalMap[pos.X, pos.Y + 1] <= minDepth && traversalMap[pos.X, pos.Y + 1] < curDepth)
                {
                    dirY = 1;
                    if (traversalMap[pos.X, pos.Y + 1] < minDepth)
                        dirX = 0;
                    minDepth = traversalMap[pos.X, pos.Y + 1];
                }
                if (traversalMap[pos.X + dirX, pos.Y + dirY] != int.MaxValue || traversalMap[pos.X, pos.Y] == int.MaxValue)  //ensure it doesn't skip over a diagonal obstacle
                    pos.Y += dirY;
                pos.X += dirX;

                if (dirX == 0 && dirY == 0)
                {
                    //unable to find path, possibly broken traversal map?
                    Console("unable to contruct path to target");
                    break;
                }
            }
            result.Add(pos.ToVector2());
            result.Reverse();
            Monitor.VerboseLog("Path done, length: " + result.Count);

            return result;
        }

        private void GetNewTaskTarget()
        {
            if (tasks.Count == 0)
                return;

            Vector2 playerPos = Game1.player.getTileLocation();

            curTaskTarget = null;

            //find closest target using bfs (maybe implement dijkstra later but need to implement priorty queue)
            curTaskTarget = Bfs(playerPos.ToPoint(), TileHasTask, out int[,] traversalMap)?.ToVector2();

            if (curTaskTarget == null)
            {
                //targets unreachable
                Game1.addHUDMessage(new HUDMessage("Unable to reach any tasks", 3));
            }
            else
            {
                path = generatePathToTarget(playerPos.ToPoint(), curTaskTarget.Value.ToPoint(), traversalMap);
            }
        }

        private bool IsTilePassable(int x, int y)
        {
            return !Game1.currentLocation.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player);
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

        private void ValidateTasks()
        {
            if (tileTasks == null || Game1.currentLocation == null)
                return;

            //clear tileTasks array so that it can be refilled
            for (int x = 0; x < tileTasks.GetLength(0); x++)
                for (int y = 0; y < tileTasks.GetLength(1); y++)
                    tileTasks[x, y] = null;

            for (int i = 0; i < tasks.Count; i++)
            {
                for (int j = 0; j < tasks[i].tiles.Count; j++)
                {
                    Vector2 pos = tasks[i].tiles[j];
                    bool isTileValid = true;

                    xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;
                    if (pos.X < 0 || pos.Y < 0 || pos.X >= size.Width || pos.Y >= size.Height)  //bounds
                        isTileValid = false;
                    else if (tileTasksToRemove[(int)pos.X, (int)pos.Y])  //flagged for removal
                        isTileValid = false;
                    else
                    {
                        //check if tile still needs to be worked on
                        if (tasks[i].type == Task.TASK_TYPE.NONE)
                            break;
                        else if (tasks[i].type == Task.TASK_TYPE.CLEAR_TREES)
                        {
                            if (Game1.currentLocation.terrainFeatures.TryGetValue(pos, out TerrainFeature feature))
                            {
                                if (feature is Tree tree)
                                {
                                    if (tree.growthStage.Value < Tree.treeStage && config.onlyChopGrownTrees)
                                        isTileValid = false;
                                    if (tree.tapped.Value && config.onlyChopUntappedTrees)
                                        isTileValid = false;
                                }
                                else if (feature is FruitTree fruitTree)
                                {
                                    if (!config.alsoClearFruitTrees)
                                        isTileValid = false;
                                    if (fruitTree.growthStage.Value < FruitTree.treeStage && config.onlyChopGrownTrees)
                                        isTileValid = false;
                                }
                                else
                                    isTileValid = false;
                            }
                            else
                            {
                                isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.CLEAR_DEBRIS)
                        {
                            string objName = Game1.currentLocation.getObjectAtTile((int)pos.X, (int)pos.Y)?.name;
                            Game1.currentLocation.terrainFeatures.TryGetValue(pos, out TerrainFeature feature);

                            if (objName != null || feature != null)
                            {
                                if ((objName == null || objName != "Weeds" && objName != "Stone" && objName != "Twig") &&
                                    (feature == null || feature is not Grass))
                                {
                                    isTileValid = false;
                                }
                            }
                            else
                            {
                                isTileValid = false;
                                foreach (var item in Game1.currentLocation.resourceClumps)
                                    if (pos.X >= item.tile.X && pos.Y >= item.tile.Y && pos.X < item.tile.X + item.width.Value && pos.Y < item.tile.Y + item.height.Value)
                                    {
                                        Type toolType;
                                        int minToolLevel;
                                        if (item.parentSheetIndex.Value == ResourceClump.stumpIndex)
                                        { toolType = typeof(Axe); minToolLevel = Tool.copper; }
                                        else if (item.parentSheetIndex.Value == ResourceClump.hollowLogIndex)
                                        { toolType = typeof(Axe); minToolLevel = Tool.steel; }
                                        else if (item.parentSheetIndex.Value == ResourceClump.meteoriteIndex)
                                        { toolType = typeof(Pickaxe); minToolLevel = Tool.steel; }
                                        else if (item.parentSheetIndex.Value == ResourceClump.boulderIndex)
                                        { toolType = typeof(Pickaxe); minToolLevel = Tool.copper; }
                                        else
                                        { toolType = typeof(Pickaxe); minToolLevel = Tool.stone; }
                                        int toolIndex = GetPlayerToolIndex(toolType);
                                        if (toolIndex < 0 || Game1.player.Items[toolIndex] is not Tool tool || tool.UpgradeLevel < minToolLevel)
                                            isTileValid = false;
                                        else
                                            isTileValid = true;
                                    }
                                //
                                //foreach (var item in Game1.currentLocation.resourceClumps)
                                //    if (p.X >= item.tile.X && p.Y >= item.tile.Y && p.X < item.tile.X + item.width.Value && p.Y < item.tile.Y + item.height.Value)
                                //        if (item.parentSheetIndex.Value == ResourceClump.stumpIndex || item.parentSheetIndex.Value == ResourceClump.hollowLogIndex)
                                //            taskTool = typeof(Axe);
                                //        else
                                //            taskTool = typeof(Pickaxe);
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.TILL)
                        {
                            if (!(Game1.currentLocation.doesTileHaveProperty((int)pos.X, (int)pos.Y, "Diggable", "Back") != null && !Game1.currentLocation.isTileOccupied(pos, ignoreAllCharacters: true) && Game1.currentLocation.isTilePassable(new xTile.Dimensions.Location((int)pos.X, (int)pos.Y), Game1.viewport)))
                            {
                                isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.DIG_ARTIFACTS)
                        {
                            if (Game1.currentLocation.getObjectAtTile((int)pos.X, (int)pos.Y)?.name != "Artifact Spot")
                            {
                                isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.TRAVEL_TO_TARGET)
                        {
                            if ((Game1.player.getTileLocation() - pos).LengthSquared() == 0 || !IsTilePassable((int)pos.X, (int)pos.Y))
                                isTileValid = false;
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.HARVEST_CROPS)
                        {
                            if (!Game1.currentLocation.terrainFeatures.TryGetValue(pos, out TerrainFeature tf) || tf is not HoeDirt hd || hd.crop == null ||
                                hd.crop.currentPhase.Value != hd.crop.phaseDays.Count - 1 || (hd.crop.fullyGrown.Value && hd.crop.dayOfCurrentPhase.Value > 0))
                            {
                                isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.FORAGE)
                        {
                            StardewValley.Object obj = Game1.currentLocation.getObjectAtTile((int)pos.X, (int)pos.Y);
                            LargeTerrainFeature feature = Game1.currentLocation.getLargeTerrainFeatureAt((int)pos.X, (int)pos.Y);
                            if ((obj == null || !obj.IsSpawnedObject) &&
                                (feature is not Bush bush || bush.townBush.Value || bush.tileSheetOffset.Value != 1 || !bush.inBloom(Game1.GetSeasonForLocation(Game1.currentLocation), Game1.dayOfMonth)))
                            {
                                isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.WATER)
                        {
                            //TODO optimize for higher tier watering cans

                            if (config.waterPet && Game1.currentLocation is Farm farm && pos.ToPoint() == farm.petBowlPosition.Value)
                            {
                                if (farm.petBowlWatered.Value)
                                    isTileValid = false;
                            }
                            else
                            {
                                if (!(Game1.currentLocation.terrainFeatures.ContainsKey(pos) && Game1.currentLocation.terrainFeatures[pos] is HoeDirt hd))
                                    isTileValid = false;
                                else if (hd.state.Value == HoeDirt.watered)
                                    isTileValid = false;
                                else if (!config.waterEmptyTiles && !Game1.currentLocation.isCropAtTile((int)pos.X, (int)pos.Y))
                                    isTileValid = false;
                            }
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.REFILL_WATER)
                        {
                            if (!Game1.currentLocation.CanRefillWateringCanOnTile((int)pos.X, (int)pos.Y))
                                isTileValid = false;
                            int toolIndex = GetPlayerToolIndex(typeof(WateringCan));
                            if (toolIndex == -1 || Game1.player.Items[toolIndex] is not WateringCan wc || wc.WaterLeft == wc.waterCanMax)
                                isTileValid = false;
                        }
                        else if (tasks[i].type == Task.TASK_TYPE.EASTER_EVENT)
                        {
                            isTileValid = false;
                            if (Game1.CurrentEvent != null)
                            {
                                foreach (var item in Game1.CurrentEvent?.festivalProps)
                                {
                                    IReflectedField<Rectangle> value = Helper.Reflection.GetField<Rectangle>(item, "boundingRect");
                                    if (value.GetValue().Contains((pos + new Vector2(0.5f, 0.5f)) * Game1.tileSize))
                                        isTileValid = true;
                                }
                            }
                        }
                        else
                        {
                            Monitor.Log(tasks[i].type + " task not implemented", LogLevel.Error);
                            isTileValid = false;
                        }
                    }

                    if (isTileValid)
                    {
                        tileTasks[(int)pos.X, (int)pos.Y] = tasks[i];
                    }
                    else
                    {
                        tasks[i].tiles.RemoveAt(j);
                        j--;
                    }
                }

                if (tasks[i].type == Task.TASK_TYPE.NONE)
                {
                    //there shouldn't be any none type tasks
                    Console("None task detected, removing");
                    tasks.RemoveAt(i);
                    i--;
                }
                else if (tasks[i].tiles.Count == 0)
                {
                    //task done, remove
                    tasks.RemoveAt(i);
                    i--;
                }
            }

            for (int x = 0; x < tileTasksToRemove.GetLength(0); x++)
                for (int y = 0; y < tileTasksToRemove.GetLength(1); y++)
                    tileTasksToRemove[x, y] = false;
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            RegenerateTaskData(Game1.currentLocation);
        }

        //player moved to a different location
        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            RegenerateTaskData(e.NewLocation);
        }

        private void RegenerateTaskData(GameLocation newLocation)
        {
            tasks = new List<Task>();
            xTile.Dimensions.Size size = newLocation.Map.Layers[0].LayerSize;
            tileTasks = new Task[size.Width, size.Height];
            tileTasksToRemove = new bool[size.Width, size.Height];
        }

        private void ReloadConfig()
        {
            config = Helper.ReadConfig<Config>();
            Helper.WriteConfig(config);

            taskColors = new Dictionary<Task.TASK_TYPE, Color>()
            {
                { Task.TASK_TYPE.NONE, config.colNone },
                { Task.TASK_TYPE.CLEAR_DEBRIS, config.colClearDebris },
                { Task.TASK_TYPE.CLEAR_TREES, config.colClearTrees},
                { Task.TASK_TYPE.TILL, config.colTill },
                { Task.TASK_TYPE.WATER, config.colWater },
                { Task.TASK_TYPE.REFILL_WATER, config.colWater },
                { Task.TASK_TYPE.DIG_ARTIFACTS, config.colDigArtifacts },
                { Task.TASK_TYPE.TRAVEL_TO_TARGET, config.colTravelToTarget },
                { Task.TASK_TYPE.HARVEST_CROPS, config.colHarvestCrops },
                { Task.TASK_TYPE.FORAGE, config.colForage },
                { Task.TASK_TYPE.EASTER_EVENT, config.colEasterEvent },
            };

            Monitor.Log("Loaded Config", LogLevel.Info);
        }

        private void Input_CursorMoved(object sender, StardewModdingAPI.Events.CursorMovedEventArgs e)
        {
            curMousePos = Game1.currentCursorTile;
        }

        private void Input_ButtonsChanged(object sender, StardewModdingAPI.Events.ButtonsChangedEventArgs e)
        {
            if (config.reloadConfig.JustPressed())
            {
                ReloadConfig();
            }

            if (Game1.activeClickableMenu != null)
                return;

            if (config.togglePlannerOverlay.JustPressed())
            {
                renderPlannerOverlay = !renderPlannerOverlay;
                if (renderPlannerOverlay)
                {
                    SetPlannerTaskType(validPlannerTasks[curPlannerTaskIndex]);
                }
                else
                {
                    //reset selector state
                    isLeftMouseDown = false;
                    isLeftMouseDown = false;
                }
            }

            //"Observe" current tile
            if (e.Pressed.Contains(SButton.O) && Game1.currentLocation != null)
            {
                StardewValley.Object obj = Game1.currentLocation.getObjectAtTile((int)Game1.currentCursorTile.X, (int)Game1.currentCursorTile.Y);
                if (obj != null)
                {
                    Console("obj:");
                    Console(obj?.GetType());
                    Console(obj?.ParentSheetIndex);
                    Console(obj?.IsSpawnedObject);
                    Console(obj?.name);
                    Console(obj?.getDescription());
                }
                Game1.currentLocation.terrainFeatures.TryGetValue(Game1.currentCursorTile, out TerrainFeature tf);
                Crop crop = (tf as HoeDirt)?.crop;
                if (crop != null)
                {
                    Console("crop:");
                    Console(!(crop.currentPhase.Value != crop.phaseDays.Count - 1 || (crop.fullyGrown.Value && crop.dayOfCurrentPhase.Value > 0)));  //ready to harvest
                    Console(crop.currentPhase.Value == crop.phaseDays.Count - 1);
                    Console(crop.fullyGrown.Value);
                }
                //Game1.currentLocation.resourceClumps;

                //foreach (var item in Game1.CurrentEvent.festivalProps)
                //{
                //    IReflectedField<Rectangle> value = Helper.Reflection.GetField<Rectangle>(item, "boundingRect");
                //    if (value.GetValue().Contains(Game1.currentCursorTile * Game1.tileSize))
                //        Console(value.GetValue());
                //}

                //HashSet<string> uniqueNames = new HashSet<string>();
                //foreach (var item in Game1.currentLocation.resourceClumps)
                //{
                //    //uniqueNames.Add(item.width);
                //}
                //Console(Game1.currentLocation.resourceClumps.Count);
                //;
                ////crop
                //if (obj == null)
                //    if (Game1.currentLocation.terrainFeatures.TryGetValue(Game1.currentCursorTile, out TerrainFeature tf) && tf is HoeDirt hd)
                //    {
                //        Console(hd.crop?.fullyGrown?.Value);
                //        Console(hd.crop?.currentPhase?.Value);
                //        Console(hd.crop?.phaseDays?.Count);
                //        Console(hd.crop?.dayOfCurrentPhase?.Value);
                //    }
                //    else
                //        Console("null");
                //LargeTerrainFeature ltf = Game1.currentLocation.getLargeTerrainFeatureAt((int)Game1.currentCursorTile.X, (int)Game1.currentCursorTile.Y);
                //Console(ltf?.GetType());
                //Console((ltf as Bush)?.inBloom(Game1.currentSeason, Game1.dayOfMonth));
                //Console((ltf as Bush)?.tileSheetOffset?.Value);
            }

            if (config.runTasks.JustPressed())
            {
                SetIsRunningTasks(!isRunningTasks, true);
            }

            if (renderPlannerOverlay)
            {
                //[ and ] to cycle task type
                if (config.cyclePlannerTaskRight.JustPressed())
                {
                    curPlannerTaskIndex++;
                    if (curPlannerTaskIndex >= validPlannerTasks.Length)
                        curPlannerTaskIndex -= validPlannerTasks.Length;
                    SetPlannerTaskType(validPlannerTasks[curPlannerTaskIndex]);
                }
                else if (config.cyclePlannerTaskLeft.JustPressed())
                {
                    curPlannerTaskIndex--;
                    if (curPlannerTaskIndex < 0)
                        curPlannerTaskIndex += validPlannerTasks.Length;
                    SetPlannerTaskType(validPlannerTasks[curPlannerTaskIndex]);
                }

                //select regions for marking
                if (config.plannerAddTask.GetState() == SButtonState.Pressed)
                {
                    isLeftMouseDown = true;
                    mouseLeftDownPos = Game1.currentCursorTile;
                }
                if (config.plannerAddTask.GetState() == SButtonState.Released)
                {
                    isLeftMouseDown = false;

                    Task t = new Task();
                    t.type = validPlannerTasks[curPlannerTaskIndex];
                    int minX = (int)Math.Min(mouseLeftDownPos.X, curMousePos.X);
                    int minY = (int)Math.Min(mouseLeftDownPos.Y, curMousePos.Y);
                    int maxX = (int)Math.Max(mouseLeftDownPos.X, curMousePos.X);
                    int maxY = (int)Math.Max(mouseLeftDownPos.Y, curMousePos.Y);
                    for (int x = minX; x <= maxX; x++)
                        for (int y = minY; y <= maxY; y++)
                            t.tiles.Add(new Vector2(x, y));
                    tasks.Add(t);
                    ValidateTasks();
                    if (config.autoStartTasks && tasks.Count > 0)
                        SetIsRunningTasks(true, true);
                }

                //right mouse to remove regions
                if (config.plannerRemoveTask.GetState() == SButtonState.Pressed)
                {
                    isRightMouseDown = true;
                    mouseRightDownPos = Game1.currentCursorTile;
                }
                if (config.plannerRemoveTask.GetState() == SButtonState.Released)
                {
                    isRightMouseDown = false;
                    int minX = (int)Math.Max(Math.Min(mouseRightDownPos.X, curMousePos.X), 0);
                    int minY = (int)Math.Max(Math.Min(mouseRightDownPos.Y, curMousePos.Y), 0);
                    int maxX = (int)Math.Min(Math.Max(mouseRightDownPos.X, curMousePos.X), tileTasksToRemove.GetLength(0) - 1);
                    int maxY = (int)Math.Min(Math.Max(mouseRightDownPos.Y, curMousePos.Y), tileTasksToRemove.GetLength(1) - 1);
                    for (int x = minX; x <= maxX; x++)
                        for (int y = minY; y <= maxY; y++)
                            tileTasksToRemove[x, y] = true;
                    ValidateTasks();
                }

                //middle click to toggle all tile
                if (config.plannerToggleAllTilesTask.GetState() == SButtonState.Pressed)
                {
                    //check if any tiles with current task
                    bool hasTile = false;
                    for (int i = 0; i < tasks.Count; i++)
                        if (tasks[i].type == validPlannerTasks[curPlannerTaskIndex])
                            hasTile = true;

                    if (hasTile)
                    {
                        //disable all
                        for (int i = 0; i < tasks.Count; i++)
                            if (tasks[i].type == validPlannerTasks[curPlannerTaskIndex])
                            {
                                tasks.RemoveAt(i);
                                i--;
                            }
                        ValidateTasks();
                    }
                    else
                    {
                        //enable all
                        Task t = new Task();
                        t.type = validPlannerTasks[curPlannerTaskIndex];
                        for (int x = 0; x < tileTasks.GetLength(0); x++)
                            for (int y = 0; y < tileTasks.GetLength(1); y++)
                                t.tiles.Add(new Vector2(x, y));
                        tasks.Add(t);
                        ValidateTasks();
                        if (config.autoStartTasks && tasks.Count > 0)
                            SetIsRunningTasks(true, true);
                    }
                }
            }
        }

        private void SetPlannerTaskType(Task.TASK_TYPE type)
        {
            if (prevTaskTypeMessage != null)
            {
                prevTaskTypeMessage.transparency = 0;
                prevTaskTypeMessage.timeLeft = 0;
            }
            Game1.addHUDMessage(prevTaskTypeMessage = new HUDMessage("Planner task: " + Task.taskToString[type], 2));
        }

        private void SetIsRunningTasks(bool value, bool alertPlayer)
        {
            if (isRunningTasks != value)
            {
                isRunningTasks = value;
                if (isRunningTasks)
                {
                    if (alertPlayer)
                        Game1.addHUDMessage(new HUDMessage("Running Tasks", 2));
                }
                else
                {
                    if (alertPlayer)
                        Game1.addHUDMessage(new HUDMessage("Stopping Tasks", 2));
                    path = null;
                    curTaskTarget = null;
                    ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                    ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
                }
            }
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            //load textures
            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            //current mouse selection
            Rectangle? leftMouseSelection = null;
            Rectangle? rightMouseSelection = null;
            if (isLeftMouseDown)
            {
                int minX = (int)Math.Min(mouseLeftDownPos.X, curMousePos.X);
                int minY = (int)Math.Min(mouseLeftDownPos.Y, curMousePos.Y);
                int maxX = (int)Math.Max(mouseLeftDownPos.X, curMousePos.X);
                int maxY = (int)Math.Max(mouseLeftDownPos.Y, curMousePos.Y);
                leftMouseSelection = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            if (isRightMouseDown)
            {
                int minX = (int)Math.Min(mouseRightDownPos.X, curMousePos.X);
                int minY = (int)Math.Min(mouseRightDownPos.Y, curMousePos.Y);
                int maxX = (int)Math.Max(mouseRightDownPos.X, curMousePos.X);
                int maxY = (int)Math.Max(mouseRightDownPos.Y, curMousePos.Y);
                rightMouseSelection = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }

            if (renderPlannerOverlay)
            {
                //render planner overlay
                int minTileX = Game1.viewport.X / Game1.tileSize - 1;
                int minTileY = Game1.viewport.Y / Game1.tileSize - 1;
                int maxTileX = minTileX + Game1.viewport.Width / Game1.tileSize + 2;
                int maxTileY = minTileY + Game1.viewport.Height / Game1.tileSize + 2;
                for (int tileX = minTileX; tileX <= maxTileX; tileX++)
                    for (int tileY = minTileY; tileY <= maxTileY; tileY++)
                    {
                        int screenX = tileX * Game1.tileSize - Game1.viewport.X;
                        int screenY = tileY * Game1.tileSize - Game1.viewport.Y;
                        Color col = Color.Black * 0.2f;

                        //tasks
                        if (tileX >= 0 && tileY >= 0 && tileX < tileTasks.GetLength(0) && tileY < tileTasks.GetLength(1) && tileTasks[tileX, tileY] != null)
                            col = taskColors[tileTasks[tileX, tileY].type] * 0.5f;

                        //selection
                        if (leftMouseSelection.HasValue && leftMouseSelection.Value.Contains(tileX, tileY))
                            col = taskColors[validPlannerTasks[curPlannerTaskIndex]] * 0.6f;
                        if (rightMouseSelection.HasValue && rightMouseSelection.Value.Contains(tileX, tileY))
                            col = Color.Red * 0.6f;

                        //cursor hover
                        if (Game1.currentCursorTile.X == tileX && Game1.currentCursorTile.Y == tileY)
                            col = Color.LightGray * 0.5f;

                        //main part of tile
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize - config.tileMargins, Game1.tileSize - config.tileMargins), col);
                        //lines
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.tileMargins, screenY, config.tileMargins, Game1.tileSize - config.tileMargins), Color.LightGray * 0.3f);
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.tileMargins, Game1.tileSize - config.tileMargins, config.tileMargins), Color.LightGray * 0.3f);
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
                            e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, (int)(curPos - prevPos).ToVector2().Length(), config.pathWidth), null, config.colPath, MathF.Atan2(prevPos.Y - curPos.Y, prevPos.X - curPos.X), Vector2.Zero, SpriteEffects.None, 0);
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

        private void Console(object message)
        {
            Monitor.Log(message?.ToString() ?? "null", LogLevel.Debug);
        }
    }
}
