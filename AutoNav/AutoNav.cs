using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using User32121Lib;
using static AutoNav.Config;

namespace AutoNav
{
    public class AutoNav : Mod
    {
        Config config;

        private IAPI user32121API;

        bool overlayEnabled = false;

        Texture2D blank;

        HashSet<Point> targetWarps, targetTiles;
        Dictionary<Point, Point> targetWarpToActualWarp;
        DIRECTION movingDir;

        readonly float sqrtHalf = MathF.Sqrt(0.5f);

        public override void Entry(IModHelper helper)
        {
            ReloadConfig();
            if (!config.enabled)
                return;

            ModPatches.PatchInput(this);

            helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            helper.Events.Player.Warped += Player_Warped;
            helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;
        }

        private void DisableMod()
        {
            Helper.Events.Input.ButtonsChanged += Input_ButtonsChanged;
            Helper.Events.Display.RenderedWorld += Display_RenderedWorld;
            Helper.Events.Player.Warped += Player_Warped;
            Helper.Events.GameLoop.GameLaunched += GameLoop_GameLaunched;

            ModPatches.ClearKeys();
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
            user32121API = Helper.ModRegistry.GetApi<IAPI>("user32121.User32121Lib");
            if (user32121API == null)
            {
                Monitor.Log("Unable to load user32121.User32121Lib", LogLevel.Error);
                DisableMod();
            }
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
                e.SpriteBatch.Draw(blank, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.5f);
                if (config.warpLists.TryGetValue(Game1.currentLocation.Name, out Dictionary<DIRECTION, string> warps))
                {
                    if (warps.TryGetValue(DIRECTION.CENTER, out string warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.CENTER ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.UP, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 5 / 2 - config.displayOffsetFromCenter), movingDir == DIRECTION.UP ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.DOWN, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X / 2, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing * 3 / 2 + config.displayOffsetFromCenter), movingDir == DIRECTION.DOWN ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.LEFT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.LEFT ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.RIGHT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing / 2), movingDir == DIRECTION.RIGHT ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.UPRIGHT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.UPRIGHT ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.UPLEFT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 - Game1.dialogueFont.LineSpacing * 3 / 2 - config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.UPLEFT ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.DOWNRIGHT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 + config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.DOWNRIGHT ? Color.Lime : Color.White); }
                    if (warps.TryGetValue(DIRECTION.DOWNLEFT, out warpName))
                    { warpName = warpName.Split('|').Last(); e.SpriteBatch.DrawString(Game1.dialogueFont, warpName, new Vector2(Game1.viewport.Width / 2 - Game1.dialogueFont.MeasureString(warpName).X - config.displayOffsetFromCenter * sqrtHalf, Game1.viewport.Height / 2 + Game1.dialogueFont.LineSpacing / 2 + config.displayOffsetFromCenter * sqrtHalf), movingDir == DIRECTION.DOWNLEFT ? Color.Lime : Color.White); }
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

        private bool IsPathFindingComplete()
        {
            return Game1.activeClickableMenu != null || Game1.fadeToBlack;
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
                    StopBot();

                    if (config.warpLists.TryGetValue(Game1.currentLocation.Name, out Dictionary<DIRECTION, string> warps))
                    {
                        if (warps.TryGetValue(targetDir, out string warpData))
                        {
                            //travel to warp
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

                            user32121API.Pathfind((x, y) => targetWarps.Contains(new Point(x, y)) || targetTiles.Contains(new Point(x, y)), IsPassable);

                            if (user32121API.HasPath())
                                movingDir = targetDir;
                            else
                                Game1.addHUDMessage(new HUDMessage("Unable to reach " + warpData, HUDMessage.error_type));
                        }
                    }
                    else
                        Monitor.LogOnce(Game1.currentLocation.Name + " has no warps configured", LogLevel.Debug);
                }
            }
        }

        private void StopBot()
        {
            user32121API.CancelPathfinding();
            targetTiles = null;
            movingDir = DIRECTION.NONE;
            ModPatches.SetKeyUp(Game1.options.moveUpButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveDownButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveLeftButton[0].key);
            ModPatches.SetKeyUp(Game1.options.moveRightButton[0].key);
        }

        private TileData IsPassable(int x, int y)
        {
            if (targetTiles.Contains(new Point(x, y)))
                return new TileData(TileData.ACTION.ACTIONBUTTON, null, 1, IsPathFindingComplete);
            else if (targetWarps.Contains(new Point(x, y)))
                return new TileData(TileData.ACTION.CUSTOM, null, 1, () => false, () => ContinueMovingToEndOfTile(x, y));
            else
                return user32121API.DefaultIsPassable(x, y);
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
    }
}
