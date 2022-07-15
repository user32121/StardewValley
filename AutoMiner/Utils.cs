using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xTile.Dimensions;

namespace AutoMiner
{
    internal static class Utils
    {
        public static Dictionary<int, string> objIndexToName = new Dictionary<int, string>()
        {
            { 8, "Topaz Node" },
            { 10, "Amethyst Node" },
            { 14, "Aquamarine Node" },
            { 6, "Jade Node" },
            { 12, "Emerald Node" },
            { 4, "Ruby Node" },
            { 2, "Diamond Node" },
            { 44, "Gem Node" },
            { 26, "Mystic Node" },
            { 75, "Geode Node" },
            { 76, "Frozen Geode Node" },
            { 77, "Magma Geode Node" },
            { 819, "Omni Geode Node" },
            { 843, "Cinder Shard Node" },
            { 844, "Cinder Shard Node" },
            { 751, "Copper Node" },
            { 849, "Copper Node" },
            { 290, "Iron Node" },
            { 850, "Iron Node" },
            { 764, "Gold Node" },
            { 765, "Iridium Node" },
            { 95, "Radioactive Node" },
        };



        static Mod mod;

        static Stopwatch stopwatch = new Stopwatch();

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

        public static void Initialize(Mod mod)
        {
            Utils.mod = mod;
        }

        public static Point? Bfs(Size size, Point start, Func<int, int, bool> isTileTarget, Func<int, int, bool?> isTilePassable, out Point[,] prevTileMap, Func<int, int, int> getTileCost = null)
        {
            int[,] scoreMap = new int[size.Width, size.Height];
            prevTileMap = new Point[size.Width, size.Height];
            for (int x = 0; x < scoreMap.GetLength(0); x++)
                for (int y = 0; y < scoreMap.GetLength(1); y++)
                    scoreMap[x, y] = int.MaxValue;
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

                if (stopwatch.Elapsed.TotalSeconds > 3)
                {
                    mod.Monitor.Log("Bfs took longer than 3 seconds, killing process", LogLevel.Error);
                    break;
                }

                Point p = toProcess.Dequeue();
                for (int x2 = Math.Max(p.X - 1, 0); x2 <= Math.Min(p.X + 1, size.Width - 1); x2++)
                    for (int y2 = Math.Max(p.Y - 1, 0); y2 <= Math.Min(p.Y + 1, size.Height - 1); y2++)
                        if (isTileTarget(x2, y2))
                        {
                            //found a target
                            if (x2 != p.X || y2 != p.Y)
                                prevTileMap[x2, y2] = p;
                            mod.Monitor.VerboseLog("bfs ran " + i + " iterations");
                            return new Point(x2, y2);
                        }

                foreach (Point dir in directions)
                {
                    Point p2 = p + dir;
                    if (p2.X >= 0 && p2.Y >= 0 && p2.X < scoreMap.GetLength(0) && p2.Y < scoreMap.GetLength(1))
                    {
                        if (dir.X == 0 || dir.Y == 0)
                        {
                            //cardinal
                            int totalTileCost = cardinalTravelCost + (getTileCost?.Invoke(p2.X, p2.Y) ?? 0);
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + totalTileCost && isTilePassable(p2.X, p2.Y) != false)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + totalTileCost;
                                prevTileMap[p2.X, p2.Y] = p;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                        else
                        {
                            //diagonal
                            int totalTileCost = diagonalTravelCost + (getTileCost?.Invoke(p2.X, p2.Y) ?? 0);
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + diagonalTravelCost && isTilePassable(p.X, p2.Y) == true && isTilePassable(p2.X, p.Y) == true && isTilePassable(p2.X, p2.Y) == true)
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + diagonalTravelCost;
                                prevTileMap[p2.X, p2.Y] = p;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                    }
                }
            }
            mod.Monitor.VerboseLog("bfs ran " + i + " iterations");
            stopwatch.Stop();
            return null;
        }

        public static List<Vector2> GeneratePathToTarget(Point start, Point target, Point[,] prevTileMap)
        {
            List<Vector2> result = new List<Vector2>();

            Point pos = target;
            stopwatch.Start();

            while (pos != start)
            {
                if (stopwatch.Elapsed.TotalSeconds > 3)
                {
                    mod.Monitor.Log("Path construction took longer than 3 seconds, killing process", LogLevel.Error);
                    return null;
                }

                if (prevTileMap[pos.X, pos.Y] == Point.Zero)
                {
                    //unable to find path, possibly broken prevTileMap?
                    mod.Monitor.Log("unable to contruct path to target", LogLevel.Debug);
                    return null;
                }

                result.Add(pos.ToVector2());
                pos = prevTileMap[pos.X, pos.Y];
            }
            result.Add(pos.ToVector2());
            result.Reverse();
            mod.Monitor.VerboseLog("Path done, length: " + result.Count);

            return result;
        }
    }
}
