using Microsoft.Xna.Framework;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xTile.Dimensions;

namespace CharacterTracker
{
    internal static class Utils
    {
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

        public static Point? Bfs(Size size, Point start, Func<int, int, bool> isTileTarget, Func<int, int, bool> isTilePassable, out Point[,] prevTileMap)
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
                for (int x2 = Math.Max(p.X - 1, 0); x2 <= Math.Min(p.X + 1, scoreMap.GetLength(0)); x2++)
                    for (int y2 = Math.Max(p.Y - 1, 0); y2 <= Math.Min(p.Y + 1, scoreMap.GetLength(1)); y2++)
                        if (isTileTarget(x2, y2))
                        {
                            //found a target
                            if (x2 != p.X || y2 != p.Y)
                                prevTileMap[x2, y2] = new Point(p.X, p.Y);
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
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + cardinalTravelCost && isTilePassable(p2.X, p2.Y))
                            {
                                scoreMap[p2.X, p2.Y] = scoreMap[p.X, p.Y] + cardinalTravelCost;
                                prevTileMap[p2.X, p2.Y] = p;
                                toProcess.Enqueue(new Point(p2.X, p2.Y));
                            }
                        }
                        else
                        {
                            //diagonal
                            if (scoreMap[p2.X, p2.Y] > scoreMap[p.X, p.Y] + diagonalTravelCost && isTilePassable(p.X, p2.Y) && isTilePassable(p2.X, p.Y) && isTilePassable(p2.X, p2.Y))
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
                    result.Clear();
                    return result;
                }

                if (prevTileMap[pos.X, pos.Y] == Point.Zero)
                {
                    //unable to find path, possibly broken prevTileMap?
                    mod.Monitor.Log("unable to contruct path to target", LogLevel.Debug);
                    result.Clear();
                    return result;
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
