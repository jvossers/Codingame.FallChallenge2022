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
        var ctx = new GameContext(Console.ReadLine);

        while (true)
        {
            #region load

            using (var scope = new StopwatchScope("load"))
            {
                ctx.LoadMatter(Console.ReadLine);
                ctx.LoadPatches(Console.ReadLine);

                ctx.QueueCommand($"MESSAGE {ctx.Islands.Count(island => island.IsContested)}/{ctx.Islands.Count}");
            }

            #endregion

            foreach (var island in ctx.Islands)
            {
                if (island.KeepGoing)
                {
                    #region build

                    Console.Error.WriteLine(island.AttackingDirection);

                    using (var scope = new StopwatchScope("build"))
                    {
                        //if(island.MyRecyclers.Count < 6 && ctx.RoundCounter < 25)
                        if (false)
                        {
                            if (island.AttackingDirection == Direction.Right)
                            {
                                var attackPatch = island.MyPatchesWhereCanBuild.Where(p => p.X >= (ctx.Width / 2) - 1 && p.X % 2 == 1).OrderByDescending(p => p.X).FirstOrDefault();
                                //var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => p.X >= (ctx.Width / 2)-1 && p.X % 2 == 1 && p.Y % 2 == 1).OrderByDescending(p => p.X).FirstOrDefault();
                                //var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => (p.X == (ctx.Width / 2)-1 || p.X == (ctx.Width / 2)+1) && p.Y % 2 == 1).OrderByDescending(p => p.X).FirstOrDefault();
                                if (attackPatch != null)
                                {
                                    ctx.QueueCommand($"BUILD {attackPatch.X} {attackPatch.Y}");
                                    island.MyMatterBudget = island.MyMatterBudget - 10;
                                }
                            }

                            if (island.AttackingDirection == Direction.Left)
                            {
                                var attackPatch = island.MyPatchesWhereCanBuild.Where(p => p.X <= (ctx.Width / 2) && p.X % 2 == 1).OrderBy(p => p.X).FirstOrDefault();
                                //var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => p.X <= (ctx.Width / 2) && p.X % 2 == 1 && p.Y % 2 == 1).OrderBy(p => p.X).FirstOrDefault();
                                //var attackPatch = ctx.MyPatchesWhereCanBuild.Where(p => (p.X == (ctx.Width / 2) || p.X == (ctx.Width / 2) - 2) && p.Y % 2 == 1).OrderBy(p => p.X).FirstOrDefault();
                                if (attackPatch != null)
                                {
                                    ctx.QueueCommand($"BUILD {attackPatch.X} {attackPatch.Y}");
                                    island.MyMatterBudget = island.MyMatterBudget - 10;
                                }
                            }
                        }
                        else
                        {
                            if (island.MyMatterBudget >= 10 && ctx.MyRecyclers.Count <= ctx.OppRecyclers.Count && island.MyPatchesWhereCanBuild.Any() && ctx.Islands.Any(island => island.IsContested))
                            {
                                var maxOppNeighbours = island.MyPatchesWhereCanBuild.Max(p => p.GetNeighbours().Count(n => n.Owner == Owner.Opponent && !p.IsRecycler));

                                var buildPatch = island.MyPatchesWhereCanBuild
                                    .Where(p => p.GetNeighbours().Count(n => n.Owner == Owner.Opponent && !p.IsRecycler) == maxOppNeighbours)
                                    .OrderByDescending(p => p.ScrapAmount)
                                    .FirstOrDefault();

                                if (buildPatch != null)
                                {
                                    ctx.QueueCommand($"BUILD {buildPatch.X} {buildPatch.Y}");
                                    island.MyMatterBudget = island.MyMatterBudget - 10;
                                }
                            }
                        }
                    }

                    #endregion

                    #region spawn

                    using (var scope = new StopwatchScope("spawn"))
                    {
                        var spawnTargetCount = (int)Math.Floor((double)island.MyMatterBudget / 10);
                        var spawnPatches = island.MyPatchesWhereCanSpawn.Where(p => p.SpawnScore > 0).OrderByDescending(p => p.SpawnScore).Take(spawnTargetCount).ToList();

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
                        foreach (var patch in island.MyPatchesWithUnits)
                        {
                            using (var patchScope = new StopwatchScope($"patch ({patch.X},{patch.Y})"))
                            {
                                var allTargets = patch.GetNeighbours().OrderByDescending(n => n.NavigationScore);

                                foreach (var t in allTargets)
                                {
                                    Console.Error.WriteLine($"-- ({t.X},{t.Y}) " + t.NavigationScore);
                                }

                                var targets = allTargets.Take(2).ToList();

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
            }

            ctx.EndRound();
        }
    }
}

/////////////////////////////////////////////////////

#region library
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


public class GameContext
{
    public int Width { get; }
    public int Height { get; }
    public int MyMatter { get; set; }
    public int OppMatter { get; private set; }
    public Patch[,] PatchGrid { get; }
    public ImmutableList<Patch> PatchList { get; private set; }
    public List<string> Commands { get; }
    public int RoundCounter { get; private set; }
    public List<Island> Islands { get; set; }
    public ImmutableList<Patch> OppPatches => PatchList.Where(p => p.Owner == Owner.Opponent).ToImmutableList();
    public ImmutableList<Patch> OppRecyclers => OppPatches.Where(p => p.IsRecycler).ToImmutableList();
    public ImmutableList<Patch> MyPatches => PatchList.Where(p => p.Owner == Owner.Me).ToImmutableList();
    public ImmutableList<Patch> MyRecyclers => MyPatches.Where(p => p.IsRecycler).ToImmutableList();

    public GameContext(Func<string> readLine)
    {
        var inputs = readLine().Split(' ');
        Width = int.Parse(inputs[0]);
        Height = int.Parse(inputs[1]);
        PatchGrid = new Patch[Width, Height];
        Commands = new List<string>();
    }

    public GameContext(int width, int height)
    {
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

        Islands = GetIslands();

        foreach (var island in Islands)
        {
            island.Initialize();
        }

        if (Islands.Any(island => island.IsContested))
        {
            var islandNeedingBudget = Islands.Where(island => island.IsContested).MinBy(island => island.MyUnitSurplus);
            islandNeedingBudget!.MyMatterBudget = MyMatter;
        }
        else
        {
            var islandNeedingBudget = Islands.Where(island => island.MyPatches.Any()).MaxBy(island => island.NeutralPatches.Count + island.OppPatches.Count);
            islandNeedingBudget!.MyMatterBudget = MyMatter;
        }
    }

    private List<Island> GetIslands()
    {
        var finder = new IslandFinder();

        return finder.GetIslands(this);
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
    public int HorizontalDistance => Island!.AttackingDirection == Direction.Right ? X + 1 : GameContext.Width - X; // todo: make relative to island boundaries
    public int Y { get; }
    public GameContext GameContext { get; }
    public int SpawnScore { get; set; }
    public int NavigationScore { get; set; }
    public Island? Island => GameContext.Islands.SingleOrDefault(island => island.Patches.Contains(this));

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

    protected bool Equals(Patch other)
    {
        return X == other.X && Y == other.Y;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Patch)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
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
}

public class Island
{
    public GameContext GameContext { get; private set; }

    public Direction AttackingDirection { get; private set; }
    public HashSet<Patch> Patches { get; private set; }
    public ImmutableList<Patch> OppPatches => Patches.Where(p => p.Owner == Owner.Opponent).ToImmutableList();
    public ImmutableList<Patch> OppRecyclers => OppPatches.Where(p => p.IsRecycler).ToImmutableList();
    public ImmutableList<Patch> MyPatches => Patches.Where(p => p.Owner == Owner.Me).ToImmutableList();
    public ImmutableList<Patch> MyRecyclers => MyPatches.Where(p => p.IsRecycler).ToImmutableList();
    public ImmutableList<Patch> MyPatchesWithUnits => MyPatches.Where(p => p.Units > 0).ToImmutableList();
    public ImmutableList<Patch> MyPatchesWhereCanSpawn => MyPatches.Where(p => p.CanSpawn).ToImmutableList();
    public ImmutableList<Patch> MyPatchesWhereCanBuild => MyPatches.Where(p => p.CanBuild).ToImmutableList();
    public ImmutableList<Patch> NeutralPatches => Patches.Where(p => p.Owner == Owner.None).ToImmutableList();
    public bool KeepGoing => MyPatches.Any(p => p.GetNeighbours().Any(n => !n.IsGrass && n.Owner != Owner.Me));
    public bool IsContested => MyPatches.Any() && OppPatches.Any();
    public int MyUnitSurplus => MyPatches.Sum(p => p.Units) - OppPatches.Sum(p => p.Units);

    public int MyMatterBudget { get; set; }

    public Island(GameContext ctx)
    {
        GameContext = ctx;
        Patches = new HashSet<Patch>();
        MyMatterBudget = 0;
    }

    public void Initialize()
    {
        AttackingDirection = DetermineAttackingDirection();

        foreach (var p in MyPatchesWhereCanSpawn)
        {
            p.SpawnScore = GetSpawnScore(p);
        }

        foreach (var p in Patches)
        {
            p.NavigationScore = GetNavigationScore(p);
        }
    }

    public int GetNavigationScore(Patch p)
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

        if (p.Owner == Owner.Me && neighbours.Count == 1)
        {
            return -1;
        }

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

        if (selfScore == 1 && neighbourScore == 1)
        {
            // todo: if nothing interesting nearby, then find nearest interesting patch and navigate to it
        }

        return selfScore * neighbourScore * p.HorizontalDistance;
    }

    public int GetSpawnScore(Patch p)
    {
        if (p.InRangeOfRecycler && p.ScrapAmount == 1)
        {
            return -1;
        }

        var neighbours = p.GetNeighbours();

        var spreadBonus = 0;
        var attackBonus = 0;

        if (p.Units == 0)
        {
            spreadBonus += 2;
        }

        if (!neighbours.Any(n => n.Owner == Owner.Me && n.Units > 0))
        {
            spreadBonus += 4;
        }

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.Opponent && !n.IsRecycler))
        {
            attackBonus = 10;

            //return 10 + p.HorizontalDistance + spreadBonus;
        }

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.None))
        {
            attackBonus = 5;

            //return 5 + p.HorizontalDistance + spreadBonus;
        }

        //return 0;
        return attackBonus + p.HorizontalDistance + spreadBonus;
    }

    private Direction DetermineAttackingDirection()
    {
        var defaultDirection = Direction.Right;

        //if (GameContext.RoundCounter == 0)
        //{
        //    if (!MyPatches.Any() || !OppPatches.Any())
        //    {
        //        return defaultDirection; // assume default value for some test scenarios where the game has not been fully initialised
        //    }

        //    return MyPatches.Min(p => p.X) < OppPatches.Min(p => p.X) ? Direction.Right : Direction.Left;
        //}

        if (!MyPatchesWithUnits.Any()) return defaultDirection; // default, value is not really relevant in this case

        int leftMostUnitX = MyPatchesWithUnits.Min(p => p.X);
        int rightMostUnitX = MyPatchesWithUnits.Max(p => p.X);

        int leftPatchCount = OppPatches.Where(p => p.X < leftMostUnitX).Sum(p => p.Units);
        int rightPatchCount = OppPatches.Where(p => p.X > rightMostUnitX).Sum(p => p.Units);

        if (leftPatchCount > rightPatchCount) return Direction.Left;
        if (rightPatchCount > leftPatchCount) return Direction.Right;
        return defaultDirection; // default

        //if (AttackingDirection == Direction.Left && leftPatchCount > rightPatchCount)
        //{
        //    return Direction.Right;
        //}

        //if (AttackingDirection == Direction.Right && rightPatchCount > leftPatchCount)
        //{
        //    return Direction.Left;
        //}

        //return AttackingDirection;
    }
}


public class IslandFinder
{
    public List<Island> GetIslands(GameContext ctx)
    {
        var islands = new List<Island>();

        var patchesDiscoveredSoFar = new HashSet<Patch>();

        foreach (var patch in ctx.PatchList.Where(p => !p.IsGrass && !p.IsRecycler))
        {
            var island = new Island(ctx);

            Explore(patch, patchesDiscoveredSoFar, island);

            if (island.Patches.Any())
            {
                islands.Add(island);
            }
        }

        return islands;
    }

    private void Explore(Patch patch, HashSet<Patch> patchesDiscoveredSoFar, Island island)
    {
        if (patchesDiscoveredSoFar.Contains(patch))
        {
            return;
        }

        patchesDiscoveredSoFar.Add(patch);
        island.Patches.Add(patch);

        var neighbours = patch.GetNeighbours().Where(n => !n.IsRecycler);

        foreach (var neighbour in neighbours)
        {
            Explore(neighbour, patchesDiscoveredSoFar, island);
        }
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

#endregion