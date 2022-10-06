using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using User32121Lib;
using static AutoNav.Config;
using U32121IAPI = User32121Lib.IAPI;

namespace AutoNav
{
    public class AutoNav : Mod, IAPI
    {
        Config config;

        private U32121IAPI user32121API;

        bool overlayEnabled = false;

        Texture2D blank;

        HashSet<Point> targetWarps, targetTiles;
        Dictionary<Point, Point> targetWarpToActualWarp;
        List<DIRECTION> movingDir = new();
        GameLocation prevLocation = null;

        DIRECTION pendingDir = DIRECTION.NONE, prevPendingDir;
        GameLocation processingLocation;
        int keyPressCounter = 0;

        List<(GameLocation, Point)> apiWarpQueue = new();
        private bool apiMode;

        readonly float sqrtHalf = MathF.Sqrt(0.5f);

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
            helper.Events.GameLoop.UpdateTicking += GameLoop_UpdateTicking;
            helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
        }

        private void DisableMod()
        {
            Helper.Events.Input.ButtonsChanged -= Input_ButtonsChanged;
            Helper.Events.Display.RenderedWorld -= Display_RenderedWorld;
            Helper.Events.Player.Warped -= Player_Warped;
            Helper.Events.GameLoop.GameLaunched -= GameLoop_GameLaunched;
            Helper.Events.GameLoop.UpdateTicking -= GameLoop_UpdateTicking;
            Helper.Events.GameLoop.SaveLoaded -= GameLoop_SaveLoaded;
        }

        private void ReloadConfig()
        {
            config = Helper.ReadConfig<Config>();
            Helper.WriteConfig(config);

            if (!config.enabled)
                DisableMod();
        }

        private void GameLoop_GameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            user32121API = Helper.ModRegistry.GetApi<U32121IAPI>("user32121.User32121Lib");
            if (user32121API == null)
            {
                Monitor.Log("Unable to load user32121.User32121Lib", LogLevel.Error);
                DisableMod();
            }
        }

        private void GameLoop_SaveLoaded(object sender, StardewModdingAPI.Events.SaveLoadedEventArgs e)
        {
            processingLocation = Game1.currentLocation;
        }

        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            if (!config.allowChainDirections)
                StopBot();
            if (movingDir.Count == 0)
                processingLocation = e.NewLocation;
        }

        private void GameLoop_UpdateTicking(object sender, StardewModdingAPI.Events.UpdateTickingEventArgs e)
        {
            if (keyPressCounter == 0)
                prevPendingDir = pendingDir;
            if (prevPendingDir != pendingDir)
                keyPressCounter = 0;
            if (pendingDir != DIRECTION.NONE)
            {
                KeybindList kbl = pendingDir switch
                {
                    DIRECTION.LEFT => config.left,
                    DIRECTION.RIGHT => config.right,
                    DIRECTION.UP => config.up,
                    DIRECTION.DOWN => config.down,
                    DIRECTION.UPLEFT => config.upLeft,
                    DIRECTION.UPRIGHT => config.upRight,
                    DIRECTION.DOWNLEFT => config.downLeft,
                    DIRECTION.DOWNRIGHT => config.downRight,
                    DIRECTION.CENTER => config.center,
                    _ => throw new NotImplementedException()
                };
                if (kbl.IsDown())
                {
                    keyPressCounter++;
                    if (keyPressCounter >= 60 * config.chainDirectionConfirmTime)
                    {
                        movingDir.Add(pendingDir);
                        prevLocation = null;
                        GetLocationInDirection(processingLocation, pendingDir, out string locationName);
                        locationName = locationName.Split('|').Last();

                        processingLocation = Game1.getLocationFromName(locationName);
                        pendingDir = DIRECTION.NONE;
                        keyPressCounter = 0;
                    }
                }
                else
                    keyPressCounter = 0;
            }

            if (!user32121API.HasPath() && Game1.currentLocation != prevLocation)
            {
                if (apiMode)
                {
                    if (apiWarpQueue.Count > 0)
                    {
                        if (prevLocation != null)
                            apiWarpQueue.RemoveAt(0);
                        if (apiWarpQueue.Count > 0)
                        {
                            string nextLocName = "unknown";
                            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;
                            (GameLocation, Point) nextWarp = apiWarpQueue.First();
                            foreach (Warp warp in nextWarp.Item1.warps)
                                if (Math.Max(0, Math.Min(size.Width - 1, warp.X)) == nextWarp.Item2.X && Math.Max(0, Math.Min(size.Height - 1, warp.Y)) == nextWarp.Item2.Y)
                                    nextLocName = warp.TargetName;

                            Monitor.Log("next location: " + nextLocName, LogLevel.Debug);
                            APITravelToLocation(apiWarpQueue.First());
                        }
                        else
                            StopBot();
                    }
                }
                else
                {
                    if (movingDir.Count > 0)
                    {
                        if (prevLocation != null)
                            movingDir.RemoveAt(0);
                        if (movingDir.Count > 0)
                        {
                            Monitor.Log("next direction: " + movingDir.First(), LogLevel.Debug);
                            TravelInDir(movingDir.First());
                        }
                        else
                            processingLocation = Game1.currentLocation;
                    }
                }
            }
            prevLocation = Game1.currentLocation;
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;

            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            if (overlayEnabled || apiMode)
            {
                e.SpriteBatch.Draw(blank, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.5f);

                if (apiMode)
                {
                    string drawText = "API mode";
                    Vector2 drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(drawText).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2);
                    e.SpriteBatch.DrawString(Game1.dialogueFont, drawText, drawPos, Color.White);
                }
                else if (processingLocation != null && config.warpLists.TryGetValue(processingLocation?.Name, out Dictionary<DIRECTION, string> warps))
                {
                    DIRECTION highlightMovingDir = config.allowChainDirections ? DIRECTION.NONE : movingDir.FirstOrDefault();
                    Vector2 drawPos;

                    Vector2 progressBarPos = Vector2.Zero;

                    if (warps.TryGetValue(DIRECTION.CENTER, out string warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.CENTER ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.CENTER) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.UP, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 5 / 2 - config.displayDistanceFromCenter);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.UP ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.UP) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.DOWN, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing * 3 / 2 + config.displayDistanceFromCenter);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.DOWN ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.DOWN) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.LEFT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayDistanceFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.LEFT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.LEFT) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.RIGHT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 + config.displayDistanceFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.RIGHT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.RIGHT) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.UPRIGHT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 + config.displayDistanceFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayDistanceFromCenter * sqrtHalf);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.UPRIGHT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.UPRIGHT) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.UPLEFT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayDistanceFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayDistanceFromCenter * sqrtHalf);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.UPLEFT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.UPLEFT) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.DOWNRIGHT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 + config.displayDistanceFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayDistanceFromCenter * sqrtHalf);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.DOWNRIGHT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.DOWNRIGHT) progressBarPos = drawPos;
                    }
                    if (warps.TryGetValue(DIRECTION.DOWNLEFT, out warpName))
                    {
                        warpName = warpName.Split('|').Last();
                        drawPos = new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayDistanceFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayDistanceFromCenter * sqrtHalf);
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, drawPos, highlightMovingDir == DIRECTION.DOWNLEFT ? Color.Lime : Color.White);
                        if (pendingDir == DIRECTION.DOWNLEFT) progressBarPos = drawPos;
                    }

                    if (config.allowChainDirections && keyPressCounter > 0)
                    {
                        float chainProgBarValue = keyPressCounter / (60 * config.chainDirectionConfirmTime);
                        e.SpriteBatch.Draw(blank, new Rectangle((int)progressBarPos.X, (int)progressBarPos.Y + Game1.dialogueFont.LineSpacing, 200, 3), Color.White * 0.5f);
                        e.SpriteBatch.Draw(blank, new Rectangle((int)progressBarPos.X, (int)progressBarPos.Y + Game1.dialogueFont.LineSpacing, (int)(200 * chainProgBarValue), 3), Color.Lime);
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
            if (Game1.currentLocation == null)
                return;
            if (Game1.activeClickableMenu != null)
                return;

            if (config.toggleNavOverlay.JustPressed())
            {
                overlayEnabled = !overlayEnabled;
                if (!overlayEnabled || apiMode)
                    StopBot();
            }

            //observe
            if (e.Pressed.Contains(SButton.O))
            {
                Monitor.Log("Loc: " + Game1.currentLocation.Name, LogLevel.Debug);
                foreach (Warp warp in Game1.currentLocation.warps)
                    Monitor.Log(warp.TargetName, LogLevel.Debug);
                Monitor.Log(Game1.currentCursorTile.ToString(), LogLevel.Debug);
            }

            if (overlayEnabled)
            {
                DIRECTION targetDir = DIRECTION.NONE;
                if (config.center.JustPressed())
                {
                    targetDir = DIRECTION.CENTER;
                    SuppressKeyBindList(config.center);
                }
                else if (config.up.JustPressed())
                {
                    targetDir = DIRECTION.UP;
                    SuppressKeyBindList(config.up);
                }
                else if (config.down.JustPressed())
                {
                    targetDir = DIRECTION.DOWN;
                    SuppressKeyBindList(config.down);
                }
                else if (config.right.JustPressed())
                {
                    targetDir = DIRECTION.RIGHT;
                    SuppressKeyBindList(config.right);
                }
                else if (config.left.JustPressed())
                {
                    targetDir = DIRECTION.LEFT;
                    SuppressKeyBindList(config.left);
                }
                else if (config.upRight.JustPressed())
                {
                    targetDir = DIRECTION.UPRIGHT;
                    SuppressKeyBindList(config.upRight);
                }
                else if (config.upLeft.JustPressed())
                {
                    targetDir = DIRECTION.UPLEFT;
                    SuppressKeyBindList(config.upLeft);
                }
                else if (config.downRight.JustPressed())
                {
                    targetDir = DIRECTION.DOWNRIGHT;
                    SuppressKeyBindList(config.downRight);
                }
                else if (config.downLeft.JustPressed())
                {
                    targetDir = DIRECTION.DOWNLEFT;
                    SuppressKeyBindList(config.downLeft);
                }

                if (targetDir != DIRECTION.NONE)
                {
                    if (!config.allowChainDirections)
                    {
                        StopBot();
                        processingLocation = Game1.currentLocation;
                    }

                    if (processingLocation != null)
                        if (config.warpLists.TryGetValue(processingLocation.Name, out Dictionary<DIRECTION, string> warps))
                        {
                            if (warps.ContainsKey(targetDir))
                            {
                                if (config.allowChainDirections)
                                {
                                    pendingDir = targetDir;
                                }
                                else
                                {
                                    TravelInDir(targetDir);
                                    movingDir.Clear();
                                    movingDir.Add(targetDir);
                                }
                            }
                        }
                        else
                            Monitor.LogOnce(processingLocation.Name + " has no warps in config", LogLevel.Debug);
                }
            }
        }

        private void SuppressKeyBindList(KeybindList kbl)
        {
            foreach (Keybind keybind in kbl.Keybinds)
                foreach (SButton buttons in keybind.Buttons)
                    if (SButtonExtensions.TryGetKeyboard(buttons, out Keys k))
                        ModPatches.SuppressKey(k);
        }

        private bool GetLocationInDirection(GameLocation from, DIRECTION dir, out string output, bool isSilent = false)
        {
            output = "";
            if (from == null)
                return false;
            if (!config.warpLists.TryGetValue(from.Name, out Dictionary<DIRECTION, string> warps) || !warps.TryGetValue(dir, out output))
            {
                StopBot();
                Monitor.LogOnce(string.Format("error in finding direction {0} in {1}", dir, from), LogLevel.Error);
                return false;
            }
            return true;
        }

        private void TravelInDir(DIRECTION targetDir)
        {
            if (!GetLocationInDirection(Game1.currentLocation, targetDir, out string warpData))
                return;
            string warpName = warpData.Split("|")[0];

            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;
            targetWarps = new HashSet<Point>();
            targetWarpToActualWarp = new Dictionary<Point, Point>();
            foreach (Warp warp in Game1.currentLocation.warps)
                if (warp.TargetName == warpData)
                {
                    Point actual = new Point(warp.X, warp.Y);
                    Point target = new Point(
                       Math.Max(0, Math.Min(size.Width - 1, warp.X)),
                       Math.Max(0, Math.Min(size.Height - 1, warp.Y)));
                    targetWarps.Add(target);
                    targetWarpToActualWarp[target] = actual;
                }

            targetTiles = new HashSet<Point>();
            if (warpName.Contains(','))
            {
                string[] points = warpName.Split(';');
                foreach (string point in points)
                {
                    string[] pointXY = point.Split(',');
                    if (pointXY.Length == 2 && int.TryParse(pointXY[0], out int x) && int.TryParse(pointXY[1], out int y))
                        targetTiles.Add(new Point(x, y));
                    else
                        Monitor.Log(string.Format("unable to parse {0}: {1}: {2}", Game1.currentLocation.Name, targetDir.ToString(), point), LogLevel.Debug);
                }
            }

            GameLocation curLoc = Game1.currentLocation;
            Point startPos = Game1.player.getTileLocationPoint();
            user32121API.Pathfind((x, y) => targetWarps.Contains(new Point(x, y)) || targetTiles.Contains(new Point(x, y)), (x, y) => IsPassable(x, y, startPos, curLoc));

            if (!user32121API.HasPath())
            {
                Game1.addHUDMessage(new HUDMessage("Unable to reach " + warpData, HUDMessage.error_type));
                StopBot();
            }
        }

        private void APITravelToLocation((GameLocation, Point) targetData)
        {
            GameLocation curLoc = Game1.currentLocation;
            (GameLocation targetLocation, Point targetPoint) = targetData;

            targetWarps = new HashSet<Point>();
            targetWarps.Add(targetData.Item2);

            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;
            targetTiles = new();
            targetWarps = new();
            targetWarpToActualWarp = new Dictionary<Point, Point>();
            foreach (Warp warp in Game1.currentLocation.warps)
            {
                Point actual = new Point(warp.X, warp.Y);
                Point target = new Point(
                   Math.Max(0, Math.Min(size.Width - 1, warp.X)),
                   Math.Max(0, Math.Min(size.Height - 1, warp.Y)));
                targetWarps.Add(target);
                targetWarpToActualWarp[target] = actual;
            }

            Point startPos = Game1.player.getTileLocationPoint();
            user32121API.Pathfind((x, y) => targetPoint == new Point(x, y), (x, y) => IsPassable(x, y, startPos, curLoc));

            if (!user32121API.HasPath())
            {
                Game1.addHUDMessage(new HUDMessage("Unable to reach " + targetLocation.Name, HUDMessage.error_type));
                StopBot();
            }
        }

        private void StopBot()
        {
            user32121API.CancelPathfinding();
            targetTiles = null;
            movingDir.Clear();
            apiMode = false;
            apiWarpQueue.Clear();
            processingLocation = Game1.currentLocation;
        }

        private TileData IsPassable(int x, int y, Point startPos, GameLocation curLoc)
        {
            if (targetTiles.Contains(new Point(x, y)))
                return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, () => IsPathFindingComplete(curLoc));
            else if (targetWarps.Contains(new Point(x, y)) && new Point(x, y) != startPos)
                return new TileData(TileData.ACTION.CUSTOM, null, 1, () => IsPathFindingComplete(curLoc), () => ContinueMovingToEndOfTile(x, y));
            else
                return user32121API.DefaultIsPassable(x, y);
        }

        private bool IsPathFindingComplete(GameLocation from)
        {
            return Game1.activeClickableMenu != null || Game1.currentLocation != from;
        }

        private void ContinueMovingToEndOfTile(int x, int y)
        {
            Point targetTile = targetWarpToActualWarp[new Point(x, y)];

            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
            Rectangle bb = Game1.player.GetBoundingBox();
            if (bb.Left < targetTile.X * Game1.tileSize)
                ModPatches.SetKeyDown(Game1.options.moveRightButton[0].key);
            else if (bb.Right > (targetTile.X + 1) * Game1.tileSize)
                ModPatches.SetKeyDown(Game1.options.moveLeftButton[0].key);
            if (bb.Top < targetTile.Y * Game1.tileSize)
                ModPatches.SetKeyDown(Game1.options.moveDownButton[0].key);
            else if (bb.Bottom > (targetTile.Y + 1) * Game1.tileSize)
                ModPatches.SetKeyDown(Game1.options.moveUpButton[0].key);
        }

        public override object GetApi()
        {
            return this;
        }

        public bool TravelToLocation(GameLocation targetLocation)
        {
            StopBot();
            apiMode = true;

            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;

            Dictionary<(GameLocation, Point), int> pathCostFromStart = new();
            Dictionary<(GameLocation, Point), (GameLocation, Point)> visitedFrom = new();
            Queue<(GameLocation, Point)> toProcess = new();
            {
                (GameLocation currentLocation, Point) currentPos = (Game1.currentLocation, Game1.player.getTileLocationPoint());
                toProcess.Enqueue(currentPos);
                visitedFrom[currentPos] = currentPos;
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            //bfs
            while (toProcess.Count > 0)
            {
                if (stopwatch.Elapsed.TotalSeconds > config.apiMaxBFSTime)
                {
                    Monitor.Log("Bfs took longer than " + config.apiMaxBFSTime + " seconds, killing process", LogLevel.Error);
                    goto CANCELED;
                }

                (GameLocation curLoc, Point curPos) = toProcess.Dequeue();

                HashSet<Point> warps = new();
                Dictionary<Point, (GameLocation, Point)> warpToWarpTarget = new();
                foreach (Warp warp in curLoc.warps)
                {
                    Point actual = new(warp.X, warp.Y);
                    Point target = new(
                       Math.Max(0, Math.Min(size.Width - 1, warp.X)),
                       Math.Max(0, Math.Min(size.Height - 1, warp.Y)));
                    warps.Add(target);
                    warpToWarpTarget[target] = (Game1.getLocationFromName(warp.TargetName), new Point(warp.TargetX, warp.TargetY));
                }

                List<(Point, int)> points = user32121API.FindAllAccessibleTargets(curPos, (x, y) => warps.Contains(new Point(x, y)), location: curLoc);
                int curPathCost = pathCostFromStart.GetValueOrDefault((curLoc, curPos), int.MaxValue);
                foreach ((Point warpPos, int newSegmentCost) in points)
                {
                    (GameLocation newLoc, Point newPos) = warpToWarpTarget[warpPos];
                    if (!pathCostFromStart.TryGetValue((newLoc, newPos), out int oldPathCost) ||
                        newSegmentCost + curPathCost < oldPathCost)
                    {
                        pathCostFromStart[(newLoc, newPos)] = newSegmentCost + curPathCost;
                        visitedFrom[(curLoc, warpPos)] = (curLoc, curPos);
                        visitedFrom[(newLoc, newPos)] = (curLoc, warpPos);

                        toProcess.Enqueue((newLoc, newPos));
                    }
                }
            }

            //construct path
            (GameLocation, Point) bestWarp = (null, Point.Zero);
            int bestScore = int.MaxValue;
            foreach (KeyValuePair<(GameLocation, Point), int> item in pathCostFromStart)
            {
                if (item.Key.Item1 == targetLocation && item.Value < bestScore)
                {
                    bestWarp = item.Key;
                    bestScore = item.Value;
                }
            }
            if (bestScore == int.MaxValue)
            {
                //could not reach targetLocation
                Monitor.Log("unable to reach " + targetLocation.Name, LogLevel.Debug);
                goto CANCELED;
            }
            (GameLocation, Point) curWarp = bestWarp;
            if (!visitedFrom.TryGetValue(curWarp, out curWarp))
            {
                Monitor.Log("pathing error: invalid initial path", LogLevel.Debug);
                goto CANCELED;
            }
            apiWarpQueue.Add(curWarp);

            stopwatch.Restart();
            while (curWarp.Item1 != Game1.currentLocation)
            {
                if (stopwatch.Elapsed.TotalSeconds > config.apiMaxBFSTime)
                {
                    Monitor.Log("Pathing took longer than " + config.apiMaxBFSTime + " seconds, killing process", LogLevel.Error);
                    goto CANCELED;
                }

                if (!visitedFrom.TryGetValue(curWarp, out curWarp) || !visitedFrom.TryGetValue(curWarp, out curWarp))
                {
                    Monitor.Log("pathing error: broken path", LogLevel.Debug);
                    goto CANCELED;
                }
                apiWarpQueue.Add(curWarp);
            }
            apiWarpQueue.Reverse();
            prevLocation = null;
            return true;

        CANCELED:
            StopBot();
            return false;
        }

        public bool TravelToClosestLocation(HashSet<GameLocation> targetLocations)
        {
            StopBot();
            apiMode = true;

            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;

            Dictionary<(GameLocation, Point), int> pathCostFromStart = new();
            Dictionary<(GameLocation, Point), (GameLocation, Point)> visitedFrom = new();
            Queue<(GameLocation, Point)> toProcess = new();
            {
                (GameLocation currentLocation, Point) currentPos = (Game1.currentLocation, Game1.player.getTileLocationPoint());
                toProcess.Enqueue(currentPos);
                visitedFrom[currentPos] = currentPos;
            }

            Stopwatch stopwatch = new();
            stopwatch.Start();
            //bfs
            while (toProcess.Count > 0)
            {
                if (stopwatch.Elapsed.TotalSeconds > config.apiMaxBFSTime)
                {
                    Monitor.Log("Bfs took longer than " + config.apiMaxBFSTime + " seconds, killing process", LogLevel.Error);
                    goto CANCELED;
                }

                (GameLocation curLoc, Point curPos) = toProcess.Dequeue();

                HashSet<Point> warps = new();
                Dictionary<Point, (GameLocation, Point)> warpToWarpTarget = new();
                foreach (Warp warp in curLoc.warps)
                {
                    Point actual = new(warp.X, warp.Y);
                    Point target = new(
                       Math.Max(0, Math.Min(size.Width - 1, warp.X)),
                       Math.Max(0, Math.Min(size.Height - 1, warp.Y)));
                    warps.Add(target);
                    warpToWarpTarget[target] = (Game1.getLocationFromName(warp.TargetName), new Point(warp.TargetX, warp.TargetY));
                }

                List<(Point, int)> points = user32121API.FindAllAccessibleTargets(curPos, (x, y) => warps.Contains(new Point(x, y)), location: curLoc);
                int curPathCost = pathCostFromStart.GetValueOrDefault((curLoc, curPos), int.MaxValue);
                foreach ((Point warpPos, int newSegmentCost) in points)
                {
                    (GameLocation newLoc, Point newPos) = warpToWarpTarget[warpPos];
                    if (!pathCostFromStart.TryGetValue((newLoc, newPos), out int oldPathCost) ||
                        newSegmentCost + curPathCost < oldPathCost)
                    {
                        pathCostFromStart[(newLoc, newPos)] = newSegmentCost + curPathCost;
                        if (warpPos != curPos)
                            visitedFrom[(curLoc, warpPos)] = (curLoc, curPos);
                        visitedFrom[(newLoc, newPos)] = (curLoc, warpPos);

                        toProcess.Enqueue((newLoc, newPos));
                    }
                }
            }

            //construct path
            (GameLocation, Point) bestWarp = (null, Point.Zero);
            int bestScore = int.MaxValue;
            foreach (KeyValuePair<(GameLocation, Point), int> item in pathCostFromStart)
            {
                if (targetLocations.Contains(item.Key.Item1) && item.Value < bestScore)
                {
                    bestWarp = item.Key;
                    bestScore = item.Value;
                }
            }
            if (bestScore == int.MaxValue)
            {
                //could not reach targetLocation
                Monitor.Log("unable to reach any locations", LogLevel.Debug);
                goto CANCELED;
            }
            (GameLocation, Point) curWarp = bestWarp;
            if (!visitedFrom.TryGetValue(curWarp, out curWarp))
            {
                Monitor.Log("pathing error: invalid initial path", LogLevel.Debug);
                goto CANCELED;
            }
            apiWarpQueue.Add(curWarp);

            stopwatch.Restart();
            while (curWarp.Item1 != Game1.currentLocation)
            {
                if (stopwatch.Elapsed.TotalSeconds > config.apiMaxBFSTime)
                {
                    Monitor.Log("Pathing took longer than " + config.apiMaxBFSTime + " seconds, killing process", LogLevel.Error);
                    goto CANCELED;
                }

                if (!visitedFrom.TryGetValue(curWarp, out curWarp) || !visitedFrom.TryGetValue(curWarp, out curWarp))
                {
                    Monitor.Log("pathing error: broken path", LogLevel.Debug);
                    goto CANCELED;
                }
                apiWarpQueue.Add(curWarp);
            }
            apiWarpQueue.Reverse();
            prevLocation = null;
            return true;

        CANCELED:
            StopBot();
            return false;
        }
    }
}
