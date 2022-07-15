using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using static AutoNav.Config;

namespace AutoNav
{
    public class AutoNav : Mod
    {
        Config config;

        bool overlayEnabled = false;

        Texture2D blank;

        Point? target;
        List<Vector2> path;
        int pathIndex;
        DIRECTION movingDir;

        GameLocation prevLocation;

        readonly float sqrtHalf = MathF.Sqrt(0.5f);


        public override void Entry(IModHelper helper)
        {
            config = Helper.ReadConfig<Config>();
            helper.WriteConfig(config);

            ModPatches.PatchInput(this);

            Utils.Initialize(this);

            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
        }

        private void GameLoop_UpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (target.HasValue && path != null)
            {
                //minecart
                if ((target.Value - Game1.player.getTileLocationPoint()).ToVector2().LengthSquared() <= 2 &&
                    (config.warpLists[Game1.currentLocation.Name][movingDir] == "Minecart" || config.warpLists[Game1.currentLocation.Name][movingDir] == "FarmHouse"))
                {
                    ModPatches.QuickPressKey(Game1.options.actionButton[0].key);
                }

                //travel along path
                if (pathIndex < path.Count)
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
            prevLocation = Game1.currentLocation;
        }

        private void Player_Warped(object sender, StardewModdingAPI.Events.WarpedEventArgs e)
        {
            StopBot();
        }

        private void Display_RenderedWorld(object sender, StardewModdingAPI.Events.RenderedWorldEventArgs e)
        {
            if (Game1.currentLocation == null)
                return;

            EnsureTexturesLoaded(e.SpriteBatch.GraphicsDevice);

            if (overlayEnabled)
            {
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

                e.SpriteBatch.Draw(blank, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.5f);
                if (config.warpLists.TryGetValue(Game1.currentLocation.Name, out Dictionary<DIRECTION, string> warps))
                {
                    if (warps.TryGetValue(DIRECTION.CENTER, out string warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.CENTER ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.UP, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 5 / 2 - config.displayOffsetFromCenter), movingDir == DIRECTION.UP ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.DOWN, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing * 3 / 2 + config.displayOffsetFromCenter), movingDir == DIRECTION.DOWN ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.LEFT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.LEFT ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.RIGHT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.RIGHT ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.UPRIGHT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.UPRIGHT ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.UPLEFT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.UPLEFT ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.DOWNRIGHT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.DOWNRIGHT ? Color.Lime : Color.White);
                    if (warps.TryGetValue(DIRECTION.DOWNLEFT, out warpName))
                        e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.DOWNLEFT ? Color.Lime : Color.White);
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
                if (!overlayEnabled)
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
                { targetDir = DIRECTION.CENTER; Helper.Input.SuppressActiveKeybinds(config.center); }
                else if (config.up.JustPressed())
                { targetDir = DIRECTION.UP; Helper.Input.SuppressActiveKeybinds(config.up); }
                else if (config.down.JustPressed())
                { targetDir = DIRECTION.DOWN; Helper.Input.SuppressActiveKeybinds(config.down); }
                else if (config.right.JustPressed())
                { targetDir = DIRECTION.RIGHT; Helper.Input.SuppressActiveKeybinds(config.right); }
                else if (config.left.JustPressed())
                { targetDir = DIRECTION.LEFT; Helper.Input.SuppressActiveKeybinds(config.left); }
                else if (config.upRight.JustPressed())
                { targetDir = DIRECTION.UPRIGHT; Helper.Input.SuppressActiveKeybinds(config.upRight); }
                else if (config.upLeft.JustPressed())
                { targetDir = DIRECTION.UPLEFT; Helper.Input.SuppressActiveKeybinds(config.upLeft); }
                else if (config.downRight.JustPressed())
                { targetDir = DIRECTION.DOWNRIGHT; Helper.Input.SuppressActiveKeybinds(config.downRight); }
                else if (config.downLeft.JustPressed())
                { targetDir = DIRECTION.DOWNLEFT; Helper.Input.SuppressActiveKeybinds(config.downLeft); }

                if (targetDir != DIRECTION.NONE)
                {
                    if (config.warpLists.TryGetValue(Game1.currentLocation.Name, out Dictionary<DIRECTION, string> warps))
                    {
                        if (warps.TryGetValue(targetDir, out string warpName))
                        {
                            //travel to warp
                            xTile.Dimensions.Size size = Game1.currentLocation.map.Layers[0].LayerSize;
                            HashSet<Point> warpTiles = new HashSet<Point>();
                            foreach (Warp warp in Game1.currentLocation.warps)
                                if (warp.TargetName == warpName)
                                    warpTiles.Add(new Point(warp.X, warp.Y));
                            if (warpName == "Minecart")
                            {
                                if (Game1.currentLocation is BusStop)
                                {
                                    warpTiles.Add(new Point(4, 3));
                                    warpTiles.Add(new Point(5, 3));
                                    warpTiles.Add(new Point(6, 3));
                                }
                                else if (Game1.currentLocation is Mine)
                                {
                                    warpTiles.Add(new Point(11, 10));
                                    warpTiles.Add(new Point(12, 10));
                                }
                                else if (Game1.currentLocation is Town)
                                {
                                    warpTiles.Add(new Point(105, 79));
                                    warpTiles.Add(new Point(106, 79));
                                    warpTiles.Add(new Point(106, 79));
                                }
                            }
                            else if (warpName == "FarmHouse")
                            {
                                warpTiles.Add(new Point(64, 15));
                            }
                            target = Utils.Bfs(size, Game1.player.getTileLocationPoint(), (int x, int y) => warpTiles.Contains(new Point(x, y)), IsTilePassable, out Point[,] prevTileMap);
                            if (target.HasValue)
                            {
                                path = Utils.GeneratePathToTarget(Game1.player.getTileLocationPoint(), target.Value, prevTileMap);
                                pathIndex = 0;
                                if (path == null)
                                {
                                    Game1.addHUDMessage(new HUDMessage("Unable to construct path to " + warpName, HUDMessage.error_type));
                                    StopBot();
                                }
                                else
                                {
                                    movingDir = targetDir;
                                }
                            }
                            else
                            {
                                Game1.addHUDMessage(new HUDMessage("Unable to reach " + warpName, HUDMessage.error_type));
                                StopBot();
                            }
                        }
                    }
                    else
                    {
                        Monitor.LogOnce(Game1.currentLocation.Name + " has no warps", LogLevel.Debug);
                        StopBot();
                    }
                }
            }
        }

        private void StopBot()
        {
            target = null;
            path = null;
            movingDir = DIRECTION.NONE;
            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
        }

        private bool IsTilePassable(int x, int y)
        {
            return !Game1.currentLocation.isCollidingPosition(new Rectangle(x * Game1.tileSize + 1, y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, true, -1, false, Game1.player);
        }
    }
}
