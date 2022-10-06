using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Object = StardewValley.Object;

namespace User32121Lib
{
    public class User32121Lib : Mod, IAPI
    {
        Config config;
        Options options = new Options();

        Point? target;
        List<Point> path;
        List<TileData> pathActions;
        int pathIndex;

        Action pathfindingCanceled;
        Action pathfindingComplete;
        Func<int, int, bool> isTarget;
        Func<int, int, TileData> isPassable;
        Stopwatch stopwatch = new Stopwatch();

        private static Point negOne = new Point(-1, -1);
        private static List<Point> directions = new List<Point>()
        {
            new Point(1,0),
            new Point(-1,0),
            new Point(0,1),
            new Point(0,-1),
            new Point(1,1),
            new Point(1,-1),
            new Point(-1,1),
            new Point(-1,-1),
        };

        private Texture2D blank;

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.ReturnedToTitle += GameLoop_ReturnedToTitle;
        }

        public void DisableMod()
        {
            Helper.Events.Display.RenderedWorld -= Display_RenderedWorld;
            Helper.Events.GameLoop.UpdateTicked -= GameLoop_UpdateTicked;
            Helper.Events.Player.Warped -= Player_Warped;
            Helper.Events.GameLoop.ReturnedToTitle -= GameLoop_ReturnedToTitle;

            ModPatches.ClearKeys();
        }

        private void GameLoop_ReturnedToTitle(object sender, StardewModdingAPI.Events.ReturnedToTitleEventArgs e)
        {
            ResetValues();
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            ModPatches.UpdateSuppressed();

            if (config.reloadConfig.JustPressed())
                ReloadConfig();

            if (config.abortPathFinding.JustPressed())
                CancelPathfinding();

            if (e.IsMultipleOf((uint)(config.recalculationFrequency * 60)) && target != null)
                CalculatePath();

            if (Game1.currentLocation == null || Game1.player.UsingTool || Game1.player.isEating)
                return;

            //spam
            if (config.spamTool.IsDown())
            {
                if (Game1.player.CurrentTool is MeleeWeapon)
                    Game1.player.BeginUsingTool();
                else
                    ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
            }

            //eat
            if (config.autoeatOverride ?? options.autoEat)
            {
                if (Game1.player.Stamina < Game1.player.MaxStamina * config.eatStaminaThreshold)
                {
                    int indexToEat = -1;
                    int staminaRecovered = -1;
                    int foodLeft = 0;
                    for (int i = 0; i < Game1.player.Items.Count; i++)
                    {
                        if (Game1.player.Items[i] != null &&
                            Game1.player.Items[i].staminaRecoveredOnConsumption() > 0)  //good food
                        {
                            foodLeft += Game1.player.Items[i].Stack;
                            if ((Game1.player.Items[i].staminaRecoveredOnConsumption() + Game1.player.Stamina <= Game1.player.MaxStamina &&  //prevent overeat
                                Game1.player.Items[i].healthRecoveredOnConsumption() + Game1.player.health <= Game1.player.maxHealth ||  //prevent overeat
                                Game1.player.Stamina < Game1.player.MaxStamina * config.overEatStaminaThreshold) &&   //allow overeat
                                (Game1.player.Items[i].staminaRecoveredOnConsumption() < staminaRecovered ^ config.eatBestFoodFirst || indexToEat == -1))  //find best or worst food
                            {
                                staminaRecovered = Game1.player.Items[i].staminaRecoveredOnConsumption();
                                indexToEat = i;
                            }
                        }
                    }
                    if (indexToEat >= 0)
                    {
                        Game1.player.CurrentToolIndex = indexToEat;
                        Game1.player.eatHeldObject();
                        if (foodLeft <= 1)
                        {
                            Game1.addHUDMessage(new HUDMessage("Out of food", HUDMessage.error_type));
                        }
                    }
                }
                if (Game1.player.health < Game1.player.maxHealth * config.eatHealthThreshold)
                {
                    int indexToEat = -1;
                    int healthRecovered = -1;
                    int foodLeft = 0;
                    for (int i = 0; i < Game1.player.Items.Count; i++)
                    {
                        if (Game1.player.Items[i] != null &&
                            Game1.player.Items[i].healthRecoveredOnConsumption() > 0)  //good food
                        {
                            foodLeft += Game1.player.Items[i].Stack;
                            if ((Game1.player.Items[i].staminaRecoveredOnConsumption() + Game1.player.stamina <= Game1.player.MaxStamina &&  //prevent overeat
                                Game1.player.Items[i].healthRecoveredOnConsumption() + Game1.player.health <= Game1.player.maxHealth ||  //prevent overeat
                                Game1.player.health < Game1.player.maxHealth * config.overEatHealthThreshold) &&   //allow overeat
                                (Game1.player.Items[i].healthRecoveredOnConsumption() < healthRecovered ^ config.eatBestFoodFirst || indexToEat == -1))  //find best or worst food
                            {
                                healthRecovered = Game1.player.Items[i].healthRecoveredOnConsumption();
                                indexToEat = i;
                            }
                        }
                    }
                    if (indexToEat >= 0)
                    {
                        Game1.player.CurrentToolIndex = indexToEat;
                        Game1.player.eatHeldObject();
                        if (foodLeft <= 1)
                        {
                            Game1.addHUDMessage(new HUDMessage("Out of food", HUDMessage.error_type));
                        }
                    }
                }
            }


            //combat
            if (config.autoCombat && !config.overrideCombat.IsDown())
            {
                Monster nearestMonster = null;
                double nearestMonsterDistanceSqr = double.MaxValue;

                IReflectedMethod isMonsterDamageApplicable = Helper.Reflection.GetMethod(Game1.currentLocation, "isMonsterDamageApplicable");
                foreach (Monster mon in Game1.currentLocation.characters.OfType<Monster>())
                {
                    double distSqr = (mon.Position - Game1.player.Position).LengthSquared();
                    if (distSqr < nearestMonsterDistanceSqr &&
                        (mon is not RockCrab rc || rc.Sprite.currentFrame % 4 != 0 || Helper.Reflection.GetField<Netcode.NetBool>(rc, "shellGone").GetValue()) &&
                        (mon is not LavaCrab lc || lc.Sprite.currentFrame % 4 != 0) &&
                        !mon.IsInvisible && !mon.isInvincible() && (isMonsterDamageApplicable.Invoke<bool>(Game1.player, mon, true) || isMonsterDamageApplicable.Invoke<bool>(Game1.player, mon, false)))
                    {
                        nearestMonsterDistanceSqr = distSqr;
                        nearestMonster = mon;
                    }
                }

                //find if monster will be in weapon's aoe
                if (nearestMonster != null && nearestMonsterDistanceSqr < 25 * Game1.tileSize * Game1.tileSize)
                {
                    if (SwitchToTool(typeof(MeleeWeapon)))
                    {
                        int prevDirection = Game1.player.FacingDirection;
                        Vector2 prevLastClick = Game1.player.lastClick;

                        int prefferedDirection = Game1.player.getGeneralDirectionTowards(nearestMonster.Position);
                        int swingDirection = -1;
                        for (int direction = 0; direction < 4; direction++)
                        {
                            for (int animationIndex = 0; animationIndex < 6; animationIndex++)
                            {
                                Game1.player.FacingDirection = direction;
                                Game1.player.lastClick = Vector2.Zero;
                                Vector2 toolLocation = Game1.player.GetToolLocation(ignoreClick: true);
                                Vector2 tileLocation1 = Vector2.Zero;
                                Vector2 tileLocation2 = Vector2.Zero;
                                Rectangle aoe = (Game1.player.CurrentTool as MeleeWeapon).getAreaOfEffect((int)toolLocation.X, (int)toolLocation.Y, direction, ref tileLocation1, ref tileLocation2, Game1.player.GetBoundingBox(), animationIndex);
                                if (nearestMonster.TakesDamageFromHitbox(aoe) && (swingDirection == -1 || direction == prefferedDirection))
                                    swingDirection = direction;
                            }
                        }

                        Game1.player.FacingDirection = prevDirection;
                        Game1.player.lastClick = prevLastClick;

                        if (swingDirection != -1)
                        {
                            Game1.player.faceDirection(swingDirection);

                            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
                            Game1.player.BeginUsingTool();
                        }
                    }
                }
            }

            //travel along path
            if (target != null && path != null && pathIndex < path.Count)
            {
                ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
                ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
                ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
                ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);

                if (!pathActions[pathIndex].IsPassableWithoutAction)
                {
                    if (pathActions[pathIndex].isActionDone())
                    {
                        pathActions[pathIndex] = TileData.Passable;
                        goto DONE_ACTION_CODE;
                    }

                    //face right direction
                    switch (pathActions[pathIndex].action)
                    {
                        case TileData.ACTION.IMPASSABLE:
                            Monitor.Log("Path contains impassable tile", LogLevel.Debug);
                            ResetValues();
                            goto DONE_ACTION_CODE;
                        case TileData.ACTION.ACTIONBUTTON:
                        case TileData.ACTION.USETOOLBUTTON:
                            if (Game1.currentCursorTile != path[pathIndex].ToVector2())
                            {
                                int screenX = (int)(((path[pathIndex].X + 0.5f) * Game1.tileSize - Game1.viewport.X) * Game1.options.zoomLevel);
                                int screenY = (int)(((path[pathIndex].Y + 0.5f) * Game1.tileSize - Game1.viewport.Y) * Game1.options.zoomLevel);
                                Game1.input.SetMousePosition(screenX, screenY);
                                goto DONE_ACTION_CODE;  //wait 1 frame for player to turn to target
                            }
                            if (Game1.player.UsingTool)
                                goto DONE_ACTION_CODE;
                            break;
                        case TileData.ACTION.USETOOL:
                            if (Game1.player.UsingTool)
                                goto DONE_ACTION_CODE;
                            break;
                        case TileData.ACTION.CUSTOM:
                            //NO OP
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    switch (pathActions[pathIndex].action)
                    {
                        case TileData.ACTION.IMPASSABLE:
                            Monitor.Log("Path contains impassable tile", LogLevel.Debug);
                            ResetValues();
                            goto DONE_ACTION_CODE;
                        case TileData.ACTION.ACTIONBUTTON:
                            if (SwitchToTool(pathActions[pathIndex].tool))
                                ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                            else
                                CancelPathfinding();
                            break;
                        case TileData.ACTION.USETOOLBUTTON:
                            if (Game1.player.stamina < config.minimumStamina)
                            {
                                Game1.addHUDMessage(new HUDMessage("Low stamina", 3));
                                ResetValues();
                                goto DONE_ACTION_CODE;
                            }
                            if (SwitchToTool(pathActions[pathIndex].tool))
                                ModPatches.QuickPressKey(Game1.options.useToolButton[0].key);
                            else
                                CancelPathfinding();
                            break;
                        case TileData.ACTION.USETOOL:
                            if (Game1.player.stamina < config.minimumStamina)
                            {
                                Game1.addHUDMessage(new HUDMessage("Low stamina", 3));
                                ResetValues();
                                goto DONE_ACTION_CODE;
                            }
                            //face right direction
                            Game1.player.faceGeneralDirection(path[pathIndex].ToVector2() * Game1.tileSize, 0, false, false);
                            //use tool
                            if (SwitchToTool(pathActions[pathIndex].tool))
                                Game1.player.BeginUsingTool();
                            else
                                CancelPathfinding();
                            break;
                        case TileData.ACTION.CUSTOM:
                            pathActions[pathIndex].customAction();
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }
            DONE_ACTION_CODE:
                if (path != null)
                    if (path[pathIndex] == Game1.player.getTileLocationPoint() && pathActions[pathIndex].IsPassableWithoutAction)
                    {
                        pathIndex++;
                        if (pathIndex >= path.Count)
                        {
                            ResetValues(canceled: false);
                            pathfindingComplete();
                        }
                    }
                    else
                    {
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

        public bool SwitchToTool(Type toolType)
        {
            //return true if succeeded
            if (toolType == null)
                return true;  //no change needed
            for (int i = 0; i < Game1.player.Items.Count; i++)
                if (Game1.player.Items[i]?.GetType() == toolType)
                {
                    Game1.player.CurrentToolIndex = i;
                    return true;
                }
            Monitor.Log("Unable to swith to " + toolType.Name, LogLevel.Debug);
            return false;
        }

        private bool HasTool(Type toolType, int minToolLevel = -1, bool warnIfFalse = false)
        {
            //return true if succeeded
            if (toolType == null)
                return true;
            for (int i = 0; i < Game1.player.Items.Count; i++)
                if (Game1.player.Items[i]?.GetType() == toolType)
                {
                    if ((Game1.player.Items[i] as Tool).UpgradeLevel >= minToolLevel)
                        return true;
                    else
                    {
                        if (warnIfFalse)
                            Monitor.Log(toolType.Name + " is not high enough level (" + minToolLevel + ")", LogLevel.Debug);
                        return false;
                    }
                }
            if (warnIfFalse)
                Monitor.Log("Unable to swith to " + toolType.Name, LogLevel.Debug);
            return false;
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;

            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            //path
            if (path != null)
            {
                Point curPos, prevPos = Point.Zero;
                for (int i = 0; i < path.Count; i++)
                {
                    int screenX = path[i].X * Game1.tileSize - Game1.viewport.X + Game1.tileSize / 2;
                    int screenY = path[i].Y * Game1.tileSize - Game1.viewport.Y + Game1.tileSize / 2;
                    curPos = new Point(screenX, screenY);
                    if (i > 0)
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, (int)(curPos - prevPos).ToVector2().Length(), config.pathThickness), null, config.pathColor, MathF.Atan2(prevPos.Y - curPos.Y, prevPos.X - curPos.X), Vector2.Zero, SpriteEffects.None, 0);
                    //intermediate
                    TileData tileData = isPassable(path[i].X, path[i].Y);
                    if (!tileData.IsPassableWithoutAction)
                    {
                        screenX -= Game1.tileSize / 2;
                        screenY -= Game1.tileSize / 2;
                        //top
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.intermediateColor);
                        //bottom
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.intermediateColor);
                        //left
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.intermediateColor);
                        //right
                        e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.intermediateColor);
                    }
                    prevPos = curPos;
                }
            }

            //target
            if (target.HasValue)
            {
                int screenX = (int)(target.Value.X * Game1.tileSize - Game1.viewport.X);
                int screenY = (int)(target.Value.Y * Game1.tileSize - Game1.viewport.Y);
                //top
                e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, Game1.tileSize, config.highlightThickness), config.targetColor);
                //bottom
                e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY + Game1.tileSize - config.highlightThickness, Game1.tileSize, config.highlightThickness), config.targetColor);
                //left
                e.SpriteBatch.Draw(blank, new Rectangle(screenX, screenY, config.highlightThickness, Game1.tileSize), config.targetColor);
                //right
                e.SpriteBatch.Draw(blank, new Rectangle(screenX + Game1.tileSize - config.highlightThickness, screenY, config.highlightThickness, Game1.tileSize), config.targetColor);
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

        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            ResetValues();
        }

        public override object GetApi()
        {
            return this;
        }

        private void ReloadConfig()
        {
            config = Helper.ReadConfig<Config>();
            Helper.WriteConfig(config);
            Monitor.Log("Loaded Config", LogLevel.Info);

            if (!config.enabled)
                DisableMod();
        }

        public void Pathfind(Func<int, int, bool> isTarget, Func<int, int, TileData> isPassable = null, Action pathfindingCanceled = null, Action pathfindingComplete = null, bool suppressNoPathNotification = false)
        {
            if (target != null)
                this.pathfindingCanceled();

            this.isTarget = isTarget ?? throw new ArgumentNullException(nameof(isTarget));
            this.isPassable = isPassable ?? DefaultIsPassable;
            this.pathfindingCanceled = pathfindingCanceled ?? NOOPFunction;
            this.pathfindingComplete = pathfindingComplete ?? NOOPFunction;

            CalculatePath(suppressNoPathNotification);
            if (!HasPath())
                CancelPathfinding();
        }

        public TileData DefaultIsPassable(int x, int y)
        {
            return DefaultIsPassable(x, y, Game1.currentLocation);
        }

        public TileData DefaultIsPassable(int x, int y, GameLocation location)
        {
            return !location.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player) ? TileData.Passable : TileData.Impassable;
        }

        public TileData DefaultIsPassableWithMining(int x, int y)
        {
            if (Game1.currentLocation.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player))
            {
                Object obj = Game1.currentLocation.getObjectAtTile(x, y);
                if (obj != null)
                {
                    if (obj is BreakableContainer)
                        return new TileData(TileData.ACTION.USETOOL, typeof(MeleeWeapon), config.defaultBreakWeedCost, () => Game1.currentLocation.getObjectAtTile(x, y) == null);
                    else if (obj.name == "Weeds")
                        return new TileData(TileData.ACTION.USETOOL, typeof(MeleeWeapon), config.defaultBreakWeedCost, () => Game1.currentLocation.getObjectAtTile(x, y) == null);
                    else if (obj.name == "Stone")
                        return new TileData(TileData.ACTION.USETOOLBUTTON, typeof(Pickaxe), config.defaultBreakStoneCost, () => Game1.currentLocation.getObjectAtTile(x, y) == null);
                    else if (obj.name == "Twig")
                        return new TileData(TileData.ACTION.USETOOLBUTTON, typeof(Axe), config.defaultBreakStoneCost, () => Game1.currentLocation.getObjectAtTile(x, y) == null);
                    else if (obj.CanBeGrabbed)
                        return new TileData(TileData.ACTION.ACTIONBUTTON, null, config.defaultBreakWeedCost, () => Game1.currentLocation.getObjectAtTile(x, y) == null);
                    else
                        return TileData.Impassable;
                }
                else
                {
                    ResourceClump rc = null;
                    foreach (ResourceClump item in Game1.currentLocation.resourceClumps)
                        if (item.occupiesTile(x, y))
                            rc = item;
                    if (rc != null)
                    {
                        TileData MakeTileDateForResourceClump(Type tool, int minToolLevel)
                        {
                            if (HasTool(tool, minToolLevel))
                                return new TileData(TileData.ACTION.USETOOLBUTTON, tool, config.defaultBreakStoneCost, () => rc.health.Value <= 0);
                            else
                                return TileData.Impassable;
                        }

                        if (rc.parentSheetIndex.Value == ResourceClump.mineRock1Index ||
                            rc.parentSheetIndex.Value == ResourceClump.mineRock2Index ||
                            rc.parentSheetIndex.Value == ResourceClump.mineRock3Index ||
                            rc.parentSheetIndex.Value == ResourceClump.mineRock4Index)
                            return new TileData(TileData.ACTION.USETOOLBUTTON, typeof(Pickaxe), config.defaultBreakStoneCost, () => rc.health.Value <= 0);
                        else if (rc.parentSheetIndex.Value == ResourceClump.stumpIndex)
                            return MakeTileDateForResourceClump(typeof(Axe), 1);
                        else if (rc.parentSheetIndex.Value == ResourceClump.hollowLogIndex)
                            return MakeTileDateForResourceClump(typeof(Axe), 2);
                        else if (rc.parentSheetIndex.Value == ResourceClump.boulderIndex)
                            return MakeTileDateForResourceClump(typeof(Pickaxe), 2);
                        else if (rc.parentSheetIndex.Value == ResourceClump.meteoriteIndex)
                            return MakeTileDateForResourceClump(typeof(Pickaxe), 3);
                        else
                            return TileData.Impassable;
                    }
                    else
                    {
                        return TileData.Impassable;
                    }
                }
            }
            else
            {
                return TileData.Passable;
            }
        }

        private void CalculatePath(bool suppressNoPathNotification = false)
        {
            ResetValues(canceled: false);

            xTile.Dimensions.Size size = Game1.currentLocation.Map.Layers[0].LayerSize;
            Point start = Game1.player.getTileLocationPoint();

            bool[,] targetMap = new bool[size.Width, size.Height];
            TileData[,] costMap = new TileData[size.Width, size.Height];
            int[,] scoreMap = new int[size.Width, size.Height];
            Point[,] prevTileMap = new Point[size.Width, size.Height];
            for (int x = 0; x < scoreMap.GetLength(0); x++)
                for (int y = 0; y < scoreMap.GetLength(1); y++)
                {
                    targetMap[x, y] = isTarget(x, y);
                    costMap[x, y] = isPassable(x, y);
                    scoreMap[x, y] = int.MaxValue;
                    prevTileMap[x, y] = negOne;
                }
            Queue<Point> toProcess = new Queue<Point>();
            toProcess.Enqueue(start);
            scoreMap[start.X, start.Y] = 0;

            const int cardinalTravelCost = 10;
            const int diagonalTravelCost = 14;

            int i = 0;
            stopwatch.Restart();
            bool cancel = false;
            while (toProcess.Count > 0)
            {
                i++;

                if (stopwatch.Elapsed.TotalSeconds > config.maxBFSTime)
                {
                    Monitor.Log("Bfs took longer than " + config.maxBFSTime + " seconds, killing process", LogLevel.Error);
                    cancel = true;
                    break;
                }

                Point p = toProcess.Dequeue();
                foreach (Point dir in directions)
                {
                    Point p2 = p + dir;
                    if (p2.X >= 0 && p2.Y >= 0 && p2.X < scoreMap.GetLength(0) && p2.Y < scoreMap.GetLength(1))
                    {
                        if (dir.X == 0 || dir.Y == 0)
                        {
                            //cardinal
                            int totalTileCost = cardinalTravelCost * costMap[p2.X, p2.Y].traversalCost;
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + totalTileCost && costMap[p2.X, p2.Y].IsPassable)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + totalTileCost;
                                prevTileMap[p2.X, p2.Y] = p;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                        else
                        {
                            //diagonal
                            int totalTileCost = diagonalTravelCost * costMap[p2.X, p2.Y].traversalCost;
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + diagonalTravelCost && costMap[p.X, p2.Y].IsPassableWithoutAction && costMap[p2.X, p.Y].IsPassableWithoutAction && costMap[p2.X, p2.Y].IsPassableWithoutAction)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + diagonalTravelCost;
                                prevTileMap[p2.X, p2.Y] = p;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                    }
                }
            }
            Monitor.VerboseLog("bfs ran " + i + " iterations");
            stopwatch.Stop();
            if (cancel)
                return;

            //find closest target
            Point closestTarget = negOne;
            for (int x = 0; x < scoreMap.GetLength(0); x++)
                for (int y = 0; y < scoreMap.GetLength(1); y++)
                    if (targetMap[x, y] && scoreMap[x, y] != int.MaxValue && (closestTarget == negOne || scoreMap[x, y] < scoreMap[closestTarget.X, closestTarget.Y]))
                        closestTarget = new Point(x, y);

            if (closestTarget == negOne)
            {
                if (!suppressNoPathNotification)
                    Monitor.Log("unable to reach any targets", LogLevel.Debug);
                return;
            }

            path = new List<Point>();
            pathActions = new List<TileData>();
            Point pos = closestTarget;
            target = closestTarget;
            stopwatch.Start();

            while (pos != start)
            {
                if (stopwatch.Elapsed.TotalSeconds > config.maxPathConstructionTime)
                {
                    Monitor.Log("Path construction took longer than " + config.maxPathConstructionTime + " seconds, killing process", LogLevel.Error);
                    cancel = true;
                    break;
                }

                if (prevTileMap[pos.X, pos.Y] == negOne)
                {
                    //unable to find path, possibly broken prevTileMap?
                    Monitor.Log("unable to construct path to target", LogLevel.Debug);
                    cancel = true;
                    break;
                }

                path.Add(pos);
                pathActions.Add(costMap[pos.X, pos.Y]);
                pos = prevTileMap[pos.X, pos.Y];
            }
            if (cancel)
            {
                ResetValues();
            }
            else
            {
                path.Add(pos);
                pathActions.Add(costMap[pos.X, pos.Y]);
                path.Reverse();
                pathActions.Reverse();
                Monitor.VerboseLog("Path done, length: " + path.Count);
            }
        }

        public List<(Point, int)> FindAllAccessibleTargets(Point start, Func<int, int, bool> isTarget, Func<int, int, TileData> isPassable = null, GameLocation location = null)
        {
            location ??= Game1.currentLocation;

            xTile.Dimensions.Size size = location.Map.Layers[0].LayerSize;

            Func<int, int, GameLocation, TileData> tileIsPassable;
            if (isPassable == null)
                tileIsPassable = DefaultIsPassable;
            else
                tileIsPassable = (x, y, _) => isPassable(x, y);

            bool[,] targetMap = new bool[size.Width, size.Height];
            TileData[,] costMap = new TileData[size.Width, size.Height];
            int[,] scoreMap = new int[size.Width, size.Height];
            for (int x = 0; x < scoreMap.GetLength(0); x++)
                for (int y = 0; y < scoreMap.GetLength(1); y++)
                {
                    targetMap[x, y] = isTarget(x, y);
                    costMap[x, y] = tileIsPassable(x, y, location);
                    scoreMap[x, y] = int.MaxValue;
                }
            Queue<Point> toProcess = new Queue<Point>();
            toProcess.Enqueue(start);
            scoreMap[start.X, start.Y] = 0;

            const int cardinalTravelCost = 10;
            const int diagonalTravelCost = 14;

            int i = 0;
            stopwatch.Restart();
            while (toProcess.Count > 0)
            {
                i++;

                if (stopwatch.Elapsed.TotalSeconds > config.maxBFSTime)
                {
                    Monitor.Log("Bfs took longer than " + config.maxBFSTime + " seconds, killing process", LogLevel.Error);
                    return new();
                }

                Point p = toProcess.Dequeue();
                foreach (Point dir in directions)
                {
                    Point p2 = p + dir;
                    if (p2.X >= 0 && p2.Y >= 0 && p2.X < scoreMap.GetLength(0) && p2.Y < scoreMap.GetLength(1))
                    {
                        if (dir.X == 0 || dir.Y == 0)
                        {
                            //cardinal
                            int totalTileCost = cardinalTravelCost * costMap[p2.X, p2.Y].traversalCost;
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + totalTileCost && costMap[p2.X, p2.Y].IsPassable)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + totalTileCost;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                        else
                        {
                            //diagonal
                            int totalTileCost = diagonalTravelCost * costMap[p2.X, p2.Y].traversalCost;
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + diagonalTravelCost && costMap[p.X, p2.Y].IsPassableWithoutAction && costMap[p2.X, p.Y].IsPassableWithoutAction && costMap[p2.X, p2.Y].IsPassableWithoutAction)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + diagonalTravelCost;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                    }
                }
            }
            Monitor.VerboseLog("bfs ran " + i + " iterations");
            stopwatch.Stop();

            //find all targets
            List<(Point, int)> accessibleTargets = new();
            for (int x = 0; x < scoreMap.GetLength(0); x++)
                for (int y = 0; y < scoreMap.GetLength(1); y++)
                    if (targetMap[x, y] && (scoreMap[x, y] != int.MaxValue))
                        accessibleTargets.Add((new(x, y), scoreMap[x, y]));
            return accessibleTargets;
        }

        private void ResetValues(bool canceled = true)
        {
            if (target != null && canceled)
                pathfindingCanceled();
            target = null;
            path = null;
            pathIndex = 0;

            ModPatches.ClearKeys();
        }

        private void NOOPFunction()
        {
            //NO OP
        }

        public void CancelPathfinding()
        {
            ResetValues();
        }

        public Options GetOptions()
        {
            return options;
        }

        public bool HasPath()
        {
            return target.HasValue;
        }
    }
}
