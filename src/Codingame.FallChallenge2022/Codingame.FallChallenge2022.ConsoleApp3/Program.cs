using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

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

            if (ctx.KeepGoing)
            {
                #region build

                using (var scope = new StopwatchScope("build"))
                {
                    if (ctx.MyRecyclers.Count < 6 && ctx.RoundCounter < 15)
                    {
                        if (ctx.AttackingDirection == Direction.Right)
                        {
                            var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => p.X >= (ctx.Width / 2) - 1).OrderByDescending(p => p.X).FirstOrDefault();
                            if (attackPatch != null)
                            {
                                ctx.QueueCommand($"BUILD {attackPatch.X} {attackPatch.Y}");
                                ctx.MyMatter = ctx.MyMatter - 10;
                            }
                        }

                        if (ctx.AttackingDirection == Direction.Left)
                        {
                            var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => p.X <= (ctx.Width / 2)).OrderBy(p => p.X).FirstOrDefault();
                            if (attackPatch != null)
                            {
                                ctx.QueueCommand($"BUILD {attackPatch.X} {attackPatch.Y}");
                                ctx.MyMatter = ctx.MyMatter - 10;
                            }
                        }
                    }
                    else
                    {
                        if (ctx.MyRecyclers.Count < ctx.OppRecyclers.Count)
                        {
                            var maxOppNeighbours = ctx.MyPatchesWhereCanBuild.Max(p => p.GetNeighbours().Count(n => n.Owner == Owner.Opponent && !p.IsRecycler));

                            var buildPatch = ctx.MyPatchesWhereCanBuild
                                .Where(p => p.GetNeighbours().Count(n => n.Owner == Owner.Opponent && !p.IsRecycler) == maxOppNeighbours)
                                .OrderByDescending(p => p.ScrapAmount)
                                .FirstOrDefault();

                            if (buildPatch != null)
                            {
                                ctx.QueueCommand($"BUILD {buildPatch.X} {buildPatch.Y}");
                                ctx.MyMatter = ctx.MyMatter - 10;
                            }
                        }
                    }
                }

                #endregion

                #region spawn

                using (var scope = new StopwatchScope("spawn"))
                {
                    var spawnTargetCount = (int)Math.Floor((double)ctx.MyMatter / 10);
                    var spawnPatches = ctx.MyPatchesWhereCanSpawn.Where(p => p.SpawnScore > 0).OrderByDescending(p => p.SpawnScore).Take(spawnTargetCount).ToList();

                    if (spawnPatches.Any())
                    {
                        for (int i = 0; i < spawnTargetCount; i++)
                        {
                            var spawnPatch = spawnPatches[i % spawnPatches.Count];
                            ctx.QueueCommand($"SPAWN 1 {spawnPatch.X} {spawnPatch.Y}");
                        }
                    }
                }

                #endregion

                #region move

                using (var scope = new StopwatchScope("move"))
                {
                    foreach (var patch in ctx.MyPatchesWithUnits)
                    {
                        using (var patchScope = new StopwatchScope($"patch ({patch.X},{patch.Y})"))
                        {
                            var targets = patch.GetNeighbours().OrderByDescending(n => n.NavigationScore).Take(2).ToList();

                            if (targets.Any())
                            {
                                for (int i = 0; i < patch.Units; i++)
                                {
                                    var target = targets[i % Math.Min(targets.Count, 2)];

                                    ctx.QueueCommand($"MOVE 1 {patch.X} {patch.Y} {target.X} {target.Y}");
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

public enum Owner
{
    Me = 1,
    Opponent = 0,
    None = -1
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
    public Patch[,] PatchGrid { get; }
    public ImmutableList<Patch> PatchList { get; private set; }
    public List<string> Commands { get; }
    public int RoundCounter { get; private set; }
    public ImmutableList<Patch> OppPatches { get; private set; }
    public ImmutableList<Patch> OppRecyclers { get; private set; }
    public ImmutableList<Patch> MyPatches { get; private set; }
    public ImmutableList<Patch> MyRecyclers { get; private set; }
    public ImmutableList<Patch> MyPatchesWithUnits { get; private set; }
    public ImmutableList<Patch> MyPatchesWhereCanSpawn { get; private set; }
    public ImmutableList<Patch> MyPatchesWhereCanBuild { get; private set; }
    public ImmutableList<Patch> NeutralPatches { get; private set; }
    public bool KeepGoing => MyPatches.Any(p => p.GetNeighbours().Any(n => !n.IsGrass && n.Owner != Owner.Me));
    public Direction AttackingDirection
    {
        get
        {
            if (!MyPatches.Any() || !OppPatches.Any())
            {
                return Direction.Right; // assume default value for some test scenarios where the game has not been fully initialised
            }

            return MyPatches.Min(p => p.X) < OppPatches.Min(p => p.X) ? Direction.Right : Direction.Left;
        }
    }

    public GameContext(IRouteProvider routeProvider, Func<string> readLine)
    {
        RouteProvider = routeProvider;
        var inputs = readLine().Split(' ');
        Width = int.Parse(inputs[0]);
        Height = int.Parse(inputs[1]);
        PatchGrid = new Patch[Width, Height];
        Commands = new List<string>();
    }

    public GameContext(IRouteProvider routeProvider, int width, int height)
    {
        RouteProvider = routeProvider;
        Width = width;
        Height = height;
        PatchGrid = new Patch[Width, Height];
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

                PatchGrid[x, y] = patch;
            }
        }

        PatchList = PatchGrid.Cast<Patch>().ToImmutableList();

        NeutralPatches = PatchList.Where(p => p.Owner == Owner.None).ToImmutableList();

        OppPatches = PatchList.Where(p => p.Owner == Owner.Opponent).ToImmutableList();
        OppRecyclers = OppPatches.Where(p => p.IsRecycler).ToImmutableList();

        MyPatches = PatchList.Where(p => p.Owner == Owner.Me).ToImmutableList();
        MyRecyclers = MyPatches.Where(p => p.IsRecycler).ToImmutableList();
        MyPatchesWhereCanSpawn = MyPatches.Where(p => p.CanSpawn).ToImmutableList();
        MyPatchesWhereCanBuild = MyPatches.Where(p => p.CanBuild).ToImmutableList();
        MyPatchesWithUnits = MyPatches.Where(p => p.Units > 0).ToImmutableList();

        foreach (var p in MyPatchesWhereCanSpawn)
        {
            p.SpawnScore = GetSpawnScore(p);
        }

        foreach (var p in PatchList)
        {
            p.NavigationScore = GetNavigationScore(p);
        }
    }

    private int GetNavigationScore(Patch p)
    {
        if (p.IsGrass)
        {
            return -1;
        }

        if (p.IsRecycler)
        {
            return -1;
        }

        if (p.InRangeOfRecycler && p.ScrapAmount == 1)
        {
            return -1;
        }

        var neighbours = p.GetNeighbours();

        var selfScore = p.Owner switch
        {
            Owner.Opponent => 10,
            Owner.None => 5,
            Owner.Me => 1
        };

        var neighbourScore = 1;

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.Opponent && !n.IsRecycler))
        {
            neighbourScore = 3;
        }

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.None))
        {
            neighbourScore = 2;
        }

        return selfScore * neighbourScore * p.HorizontalDistance;
    }

    private int GetSpawnScore(Patch p)
    {
        if (p.InRangeOfRecycler && p.ScrapAmount == 1)
        {
            return -1;
        }

        var neighbours = p.GetNeighbours();

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.Opponent && !n.IsRecycler))
        {
            return 10 * p.HorizontalDistance;
        }

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.None))
        {
            return 5 * p.HorizontalDistance;
        }

        return 0;
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
    public bool IsGrass => ScrapAmount == 0;
    public int ScrapAmount { get; }
    public Owner Owner { get; }
    public int Units { get; }
    public bool IsRecycler { get; }
    public bool CanBuild { get; }
    public bool CanSpawn { get; }
    public bool InRangeOfRecycler { get; }
    public int X { get; }
    public int HorizontalDistance => GameContext.AttackingDirection == Direction.Right ? X : GameContext.Width - 1 - X;
    public int Y { get; }
    public GameContext GameContext { get; }
    public int SpawnScore { get; set; }
    public int NavigationScore { get; set; }

    public Patch(string[] inputs, int x, int y, GameContext gameContext)
    {
        ScrapAmount = int.Parse(inputs[0]);
        Owner = (Owner)int.Parse(inputs[1]); // 1 = me, 0 = foe, -1 = neutral
        Units = int.Parse(inputs[2]);
        IsRecycler = inputs[3] == "1";
        CanBuild = inputs[4] == "1";
        CanSpawn = inputs[5] == "1";
        InRangeOfRecycler = inputs[6] == "1";
        X = x;
        Y = y;
        GameContext = gameContext;
    }

    public ImmutableList<Patch> GetNeighbours()
    {
        var neighbours = new List<Patch>();

        if (!OutOfBounds(this, Direction.Up)) neighbours.Add(GameContext.PatchGrid[X, Y - 1]);
        if (!OutOfBounds(this, Direction.Right)) neighbours.Add(GameContext.PatchGrid[X + 1, Y]);
        if (!OutOfBounds(this, Direction.Down)) neighbours.Add(GameContext.PatchGrid[X, Y + 1]);
        if (!OutOfBounds(this, Direction.Left)) neighbours.Add(GameContext.PatchGrid[X - 1, Y]);

        // exclude grass
        return neighbours.Where(p => p.ScrapAmount > 0).ToImmutableList();
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
                    Direction.Down => GameContext.PatchGrid[patch.X, patch.Y + 1],
                    Direction.Up => GameContext.PatchGrid[patch.X, patch.Y - 1],
                    Direction.Right => GameContext.PatchGrid[patch.X + 1, patch.Y],
                    Direction.Left => GameContext.PatchGrid[patch.X - 1, patch.Y],
                    _ => patch
                };

                if (patch.ScrapAmount == 0)
                {
                    multipliers.Clear();
                    break;
                }

                if (patch.InRangeOfRecycler && patch.ScrapAmount == 1)
                {
                    multipliers.Clear();
                    break;
                }

                if (patch.IsRecycler)
                {
                    multipliers.Clear();
                    break;
                }

                var multiplier = patch.Owner switch
                {
                    Owner.None => 2, // neutral
                    Owner.Opponent => 3, // opponent
                    Owner.Me => 1 // mine
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

                        var patches = GameContext.PatchGrid.Cast<Patch>().ToList();

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
            var nextPatch = GameContext.PatchGrid[patch.X + offsetX, patch.Y + offsetY];

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