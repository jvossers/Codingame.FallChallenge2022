using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;


class Player
{
    static void Main(string[] args)
    {
        var ctx = new GameContext(new BruteForceRouteProvider(), Console.ReadLine);

        while (true)
        {
            #region load

            using (var scope = new StopwatchScope("load"))
            {
                ctx.LoadMatter(Console.ReadLine);
                ctx.LoadPatches(Console.ReadLine);
            }

            #endregion

            var myPatches = ctx.Patches.Cast<Patch>().Where(p => p.Owner == 1);
            var keepGoing = myPatches.Any(p => p.GetNeighbours().Any(n => n.ScrapAmount > 0 && (n.Owner == 0 || n.Owner == -1)));

            if (keepGoing)
            {
                #region build

                using (var scope = new StopwatchScope("build"))
                {
                    if (ctx.MyRecyclerCount < ctx.OppRecyclerCount)
                    {
                        //var buildPatchCandidates = ctx.Patches.Cast<Patch>().Where(p => p.CanBuild == 1).OrderByDescending(p => p.ScrapAmount).Take(10);
                        var buildPatchCandidates = ctx.Patches.Cast<Patch>().Where(p => p.CanBuild == 1);

                        var maxOppNeighbours = buildPatchCandidates.Max(p => p.GetNeighbours().Count(n => n.Owner == 0 && p.Recycler == 0));

                        var buildPatch = buildPatchCandidates
                            .Where(p => p.GetNeighbours().Count(n => n.Owner == 0 && p.Recycler == 0) == maxOppNeighbours)
                            .OrderByDescending(p => p.ScrapAmount)
                            .FirstOrDefault();

                        if (buildPatch != null)
                        {
                            ctx.QueueCommand($"BUILD {buildPatch.X} {buildPatch.Y}");
                            ctx.MyMatter = ctx.MyMatter - 10;
                        }
                    }
                }

                #endregion

                #region spawn

                using (var scope = new StopwatchScope("spawn"))
                {
                    var spawnTargetCount = (int)Math.Floor((double)ctx.MyMatter / 10);
                    var spawnPatches = ctx.Patches.Cast<Patch>().Where(p => p.SpawnScore > 0).OrderByDescending(p => p.SpawnScore).Take(spawnTargetCount).ToList();
                    if (spawnPatches.Any())
                    {
                        for (int i = 0; i < spawnTargetCount; i++)
                        {
                            var spawnPatch = spawnPatches[i % spawnPatches.Count];

                            if (spawnPatch != null && spawnPatch.CanSpawn == 1)
                            {
                                ctx.QueueCommand($"SPAWN 1 {spawnPatch.X} {spawnPatch.Y}");
                                continue;
                            }
                        }
                    }
                }

                #endregion

                #region move

                using (var scope = new StopwatchScope("move"))
                {
                    var myPatchesWithUnits = ctx.Patches.Cast<Patch>().Where(p => p.Owner == 1 && p.Units > 0);

                    foreach (var patch in myPatchesWithUnits)
                    {
                        using (var patchScope = new StopwatchScope($"patch ({patch.X},{patch.Y})"))
                        {
                            var routes = patch.GetDiverseRoutes(4, 1);

                            for (int i = 0; i < patch.Units; i++)
                            {
                                if (i < routes.Count)
                                {
                                    var route = routes[i];
                                    string command = patch.CreateMoveCommand(route.Directions.First());
                                    ctx.QueueCommand(command);
                                }
                                else
                                {
                                    // no route available with a favourable score
                                    var target = ctx.Patches.Cast<Patch>().MaxBy(p => p.SpawnScore);

                                    if (target != null)
                                    {
                                        //Console.Error.WriteLine($"moving {patch.X} {patch.Y} towards target {target.X} {target.Y}");
                                        ctx.QueueCommand($"MOVE 1 {patch.X} {patch.Y} {target.X} {target.Y}");
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion

            }

            ctx.EndRound();
        }
    }
}
















public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public class Route
{
    public List<Direction> Directions { get; }
    public int Score { get; set; }

    public Route(List<Direction> directions)
    {
        Directions = new List<Direction>();
        Directions.AddRange(directions);
    }

    public Route()
    {
        Directions = new List<Direction>();
    }

    public override string ToString()
    {
        return String.Join(",", Directions.Select(d => Enum.GetName(d)));
    }
}

public class GameContext
{
    public IRouteProvider RouteProvider { get; }

    public int Width { get; }
    public int Height { get; }
    public int MyMatter { get; set; }
    public int OppMatter { get; private set; }
    public Patch[,] Patches { get; }
    public List<string> Commands { get; }
    public int RoundCounter { get; private set; }

    public int MyRecyclerCount => Patches.Cast<Patch>().Count(p => p.Owner == 1 && p.Recycler == 1);
    public int OppRecyclerCount => Patches.Cast<Patch>().Count(p => p.Owner == 0 && p.Recycler == 1);

    public GameContext(IRouteProvider routeProvider, Func<string> readLine)
    {
        RouteProvider = routeProvider;
        var inputs = readLine().Split(' ');
        Width = int.Parse(inputs[0]);
        Height = int.Parse(inputs[1]);
        Patches = new Patch[Width, Height];
        Commands = new List<string>();
    }

    public GameContext(IRouteProvider routeProvider, int width, int height)
    {
        RouteProvider = routeProvider;
        Width = width;
        Height = height;
        Patches = new Patch[Width, Height];
        Commands = new List<string>();
    }


    public void QueueCommand(string command)
    {
        Commands.Add(command);
    }

    public void EndRound()
    {
        if (!Commands.Any())
        {
            Commands.Add("WAIT");
        }
        Console.WriteLine(String.Join(";", Commands));
        Commands.Clear();
        RoundCounter++;
    }

    public void LoadPatches(Func<string> readLine)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var inputs = readLine().Split(' ');

                var patch = new Patch(inputs, x, y, this);

                Patches[x, y] = patch;
            }
        }

        foreach (var p in Patches.Cast<Patch>().Where(p => p.CanSpawn == 1))
        {
            p.SpawnScore = GetSpawnScore(p);
        }
    }

    private int GetSpawnScore(Patch p)
    {
        var neighbours = p.GetNeighbours();

        if (neighbours.Any(n => n.ScrapAmount > 0 && n.Owner == 0 && n.Recycler == 0))
        {
            return 10;
        }

        if (neighbours.Any(n => n.ScrapAmount > 0 && n.Owner == -1))
        {
            return 5;
        }

        return 0;

        //var bestRoute = p.GetRoutes(1, 2).FirstOrDefault();
        //if (bestRoute != null)
        //{
        //    p.SpawnScore = bestRoute.Score;
        //}
    }

    public void LoadMatter(Func<string> readLine)
    {
        var inputs = readLine().Split(' ');
        MyMatter = int.Parse(inputs[0]);
        OppMatter = int.Parse(inputs[1]);
    }
}

public class Patch
{
    public int ScrapAmount { get; }
    public int Owner { get; }
    public int Units { get; }
    public int Recycler { get; }
    public int CanBuild { get; }
    public int CanSpawn { get; }
    public int InRangeOfRecycler { get; }
    public int X { get; }
    public int Y { get; }
    public GameContext GameContext { get; }
    public int SpawnScore { get; set; }

    public Patch(string[] inputs, int x, int y, GameContext gameContext)
    {
        ScrapAmount = int.Parse(inputs[0]);
        Owner = int.Parse(inputs[1]); // 1 = me, 0 = foe, -1 = neutral
        Units = int.Parse(inputs[2]);
        Recycler = int.Parse(inputs[3]);
        CanBuild = int.Parse(inputs[4]);
        CanSpawn = int.Parse(inputs[5]);
        InRangeOfRecycler = int.Parse(inputs[6]);
        X = x;
        Y = y;
        GameContext = gameContext;
    }

    public List<Patch> GetNeighbours()
    {
        var neighbours = new List<Patch>();

        if (!OutOfBounds(this, Direction.Up)) neighbours.Add(GameContext.Patches[X, Y - 1]);
        if (!OutOfBounds(this, Direction.Right)) neighbours.Add(GameContext.Patches[X + 1, Y]);
        if (!OutOfBounds(this, Direction.Down)) neighbours.Add(GameContext.Patches[X, Y + 1]);
        if (!OutOfBounds(this, Direction.Left)) neighbours.Add(GameContext.Patches[X - 1, Y]);

        // exclude grass
        neighbours = neighbours.Where(p => p.ScrapAmount > 0).ToList();

        return neighbours;
    }

    public string CreateMoveCommand(Direction direction, int count = 1)
    {
        var moveToX = direction switch
        {
            Direction.Left => X - 1,
            Direction.Right => X + 1,
            _ => X
        };

        var moveToY = direction switch
        {
            Direction.Up => Y - 1,
            Direction.Down => Y + 1,
            _ => Y
        };

        return $"MOVE {count} {X} {Y} {moveToX} {moveToY}";
    }

    public List<Route> GetRoutes(int rounds, int requiredMinimumScore = 1)
    {
        //var routes = GetRoutesPrivate(rounds);
        var routes = GameContext.RouteProvider.GetRoutes(rounds);

        ScoreRoutes(routes);

        routes = routes
            .Where(r => r.Score >= requiredMinimumScore)
            .OrderByDescending(r => r.Score)
            .ToList();

        return routes;
    }

    private List<Route> GetRoutesPrivate(int rounds)
    {
        var routes = new List<Route>();

        if (rounds == 1)
        {
            return new List<Route>()
            {
                new Route()
                {
                    Directions = { Direction.Down }
                },
                new Route()
                {
                    Directions = { Direction.Up }
                },
                new Route()
                {
                    Directions = { Direction.Left }
                },
                new Route()
                {
                    Directions = { Direction.Right }
                },
            };
        }
        else
        {
            var baseRoutes = this.GetRoutesPrivate(rounds - 1);

            foreach (var baseRoute in baseRoutes)
            {
                var routeDown = new Route(baseRoute.Directions);
                routeDown.Directions.Add(Direction.Down);
                routes.Add(routeDown);

                var routeUp = new Route(baseRoute.Directions);
                routeUp.Directions.Add(Direction.Up);
                routes.Add(routeUp);

                var routeLeft = new Route(baseRoute.Directions);
                routeLeft.Directions.Add(Direction.Left);
                routes.Add(routeLeft);

                var routeRight = new Route(baseRoute.Directions);
                routeRight.Directions.Add(Direction.Right);
                routes.Add(routeRight);
            }
        }

        return routes;
    }

    private bool OutOfBounds(Patch p, Direction direction)
    {
        return direction switch
        {
            Direction.Down => (p.Y == GameContext.Height - 1),
            Direction.Up => (p.Y == 0),
            Direction.Right => (p.X == GameContext.Width - 1),
            Direction.Left => (p.X == 0),
            _ => false
        };
    }

    private void ScoreRoutes(List<Route> routes)
    {
        foreach (var route in routes)
        {
            var multipliers = new List<int>();

            Patch patch = this;

            foreach (var direction in route.Directions)
            {
                if (OutOfBounds(patch, direction))
                {
                    multipliers.Clear();
                    break;
                }

                patch = direction switch
                {
                    Direction.Down => GameContext.Patches[patch.X, patch.Y + 1],
                    Direction.Up => GameContext.Patches[patch.X, patch.Y - 1],
                    Direction.Right => GameContext.Patches[patch.X + 1, patch.Y],
                    Direction.Left => GameContext.Patches[patch.X - 1, patch.Y],
                    _ => patch
                };

                if (patch.ScrapAmount == 0)
                {
                    multipliers.Clear();
                    break;
                }

                if (patch.InRangeOfRecycler == 1 && patch.ScrapAmount == 1)
                {
                    multipliers.Clear();
                    break;
                }

                if (patch.Recycler == 1)
                {
                    multipliers.Clear();
                    break;
                }

                var multiplier = patch.Owner switch
                {
                    -1 => 2, // neutral
                    0 => 3, // opponent
                    1 => 1 // mine
                };


                // first move boosting / downgrading
                if (!multipliers.Any())
                {
                    if (patch.GetNeighbours().Count(p => p.ScrapAmount == 0) == 3)
                    {
                        // downgrade
                        multiplier = 1;
                    }
                    else
                    {

                        var patches = GameContext.Patches.Cast<Patch>().ToList();

                        // if going in this direction will send in the direction of a opp owned tile then boost the multiplier
                        //if (direction == Direction.Right &&
                        //    patches.Any(p => p.Owner == 0 && p.Y == patch.Y && p.X > patch.X) ||
                        //    direction == Direction.Left &&
                        //    patches.Any(p => p.Owner == 0 && p.Y == patch.Y && p.X < patch.X) ||
                        //    direction == Direction.Up &&
                        //    patches.Any(p => p.Owner == 0 && p.X == patch.X && p.Y < patch.Y) ||
                        //    direction == Direction.Down &&
                        //    patches.Any(p => p.Owner == 0 && p.X == patch.X && p.Y > patch.Y))
                        if (UltimatelyLeadsToOpponentPatch(patch, direction))
                        {
                            multiplier = multiplier * 2;
                        }
                    }
                }


                multipliers.Add(multiplier);

                // crossing grass
                // crossing recycler
                // out of bound
            }

            if (multipliers.Any())
            {
                route.Score = multipliers.Aggregate((a, b) => a * b);
            }
        }
    }

    private bool UltimatelyLeadsToOpponentPatch(Patch patch, Direction direction)
    {
        int offsetX = 0;
        int offsetY = 0;

        while (true)
        {
            var nextPatch = GameContext.Patches[patch.X + offsetX, patch.Y + offsetY];

            if (OutOfBounds(nextPatch, direction))
                return false;

            if (nextPatch.ScrapAmount == 0)
                return false;

            if (nextPatch.Owner == 0)
                return true;

            offsetX = direction switch
            {
                Direction.Left => offsetX - 1,
                Direction.Right => offsetX + 1,
                _ => offsetX
            };

            offsetY = direction switch
            {
                Direction.Up => offsetY - 1,
                Direction.Down => offsetY + 1,
                _ => offsetY
            };
        }
    }

    public List<Route> GetDiverseRoutes(int rounds, int requiredMinimumScore = 1)
    {
        var routes = GetRoutes(rounds, requiredMinimumScore);

        routes = routes.GroupBy(r => r.Directions.First()).Select(g => g.First()).ToList();

        return routes;
    }
}

public interface IRouteProvider
{
    List<Route> GetRoutes(int rounds);
}

public class BruteForceRouteProvider : IRouteProvider
{
    public List<Route> GetRoutes(int rounds)
    {
        var routes = new List<Route>();

        if (rounds == 1)
        {
            return new List<Route>()
                {
                    new Route()
                    {
                        Directions = { Direction.Down }
                    },
                    new Route()
                    {
                        Directions = { Direction.Up }
                    },
                    new Route()
                    {
                        Directions = { Direction.Left }
                    },
                    new Route()
                    {
                        Directions = { Direction.Right }
                    },
                };
        }
        else
        {
            var baseRoutes = GetRoutes(rounds - 1);

            foreach (var baseRoute in baseRoutes)
            {
                var routeDown = new Route(baseRoute.Directions);
                routeDown.Directions.Add(Direction.Down);
                routes.Add(routeDown);

                var routeUp = new Route(baseRoute.Directions);
                routeUp.Directions.Add(Direction.Up);
                routes.Add(routeUp);

                var routeLeft = new Route(baseRoute.Directions);
                routeLeft.Directions.Add(Direction.Left);
                routes.Add(routeLeft);

                var routeRight = new Route(baseRoute.Directions);
                routeRight.Directions.Add(Direction.Right);
                routes.Add(routeRight);
            }
        }

        return routes;
    }
}

public class TwoDirectionsRouteProvider : IRouteProvider
{
    public List<Route> GetRoutes(int rounds)
    {
        var count = (int)Math.Pow(2, rounds);

        var routes = new List<Route>();

        for (int i = 0; i < count - 1; i++)
        {
            string binary = Convert.ToString(i, 2);
            binary = binary.PadLeft(rounds, '0');

            var routeUpRight = new Route();
            var routeRightDown = new Route();
            var routeDownLeft = new Route();
            var routeLeftUp = new Route();

            foreach (var c in binary)
            {
                if (c == '1')
                {
                    routeUpRight.Directions.Add(Direction.Up);
                    routeRightDown.Directions.Add(Direction.Right);
                    routeDownLeft.Directions.Add(Direction.Down);
                    routeLeftUp.Directions.Add(Direction.Left);
                }

                if (c == '0')
                {
                    routeUpRight.Directions.Add(Direction.Right);
                    routeRightDown.Directions.Add(Direction.Down);
                    routeDownLeft.Directions.Add(Direction.Left);
                    routeLeftUp.Directions.Add(Direction.Up);
                }
            }

            routes.Add(routeUpRight);
            routes.Add(routeRightDown);
            routes.Add(routeDownLeft);
            routes.Add(routeLeftUp);
        }

        return routes;
    }
}

public class StopwatchScope : IDisposable
{
    private DateTime _start;
    private string _name;

    public StopwatchScope(string name)
    {
        _name = name;
        _start = DateTime.UtcNow;
    }

    public void Dispose()
    {
        Console.Error.WriteLine($"{_name}: {DateTime.UtcNow.Subtract(_start).TotalMilliseconds}ms");
    }
}