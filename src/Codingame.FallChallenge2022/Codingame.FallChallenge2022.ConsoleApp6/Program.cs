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
                    #region emergency

                    int threshold = 3;

                    foreach (var oppPatch in island.OppPatches.Where(p => p.Units > threshold))
                    {
                        foreach (var myPatch in oppPatch.GetNeighbours().Where(p => p.Owner == Owner.Me && p.CanBuild))
                        {
                            if (island.MyMatterBudget >= 10)
                            {
                                ctx.QueueCommand($"BUILD {myPatch.X} {myPatch.Y}");
                                island.MyMatterBudget = island.MyMatterBudget - 10;
                            }
                        }
                    }

                    #endregion

                    #region build

                    Console.Error.WriteLine(island.AttackingDirection);

                    using (var scope = new StopwatchScope("build"))
                    {
                        if (ctx.RoundCounter > 1)
                        {
                            if (island.MyMatterBudget >= 10 && ctx.MyRecyclers.Count <= ctx.OppRecyclers.Count && island.MyPatchesWhereCanBuild.Any() && ctx.Islands.Any(island => island.IsContested))
                            {
                                var buildPatchCandidates = island.MyPatchesWhereCanBuild.Where(p => p.BuildScore > 0);

                                foreach (var bpc in buildPatchCandidates.OrderByDescending(p => p.BuildScore))
                                {
                                    Console.Error.WriteLine($"({bpc.X},{bpc.Y}) {bpc.BuildScore}");
                                }

                                var buildPatch = buildPatchCandidates.MaxBy(p => p.BuildScore);

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

                                int nrOfDirections = 2;

                                var targets = allTargets.Take(nrOfDirections).ToList();

                                if (targets.Any())
                                {
                                    for (int i = 0; i < patch.Units; i++)
                                    {
                                        var target = targets[i % Math.Min(targets.Count, nrOfDirections)];

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
    public int BuildScore { get; set; }

    public Island? Island => _lazyIsland.Value;

    private Lazy<Island?> _lazyIsland;

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

        _lazyIsland = new Lazy<Island?>(() => GameContext.Islands.SingleOrDefault(island => island.Patches.Contains(this)));
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


    private readonly Lazy<ImmutableList<Patch>> _lazyOppPatches;
    public ImmutableList<Patch> OppPatches => _lazyOppPatches.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyOppRecyclers;

    public ImmutableList<Patch> OppRecyclers => _lazyOppRecyclers.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyOppPatchesWithUnits;
    public ImmutableList<Patch> OppPatchesWithUnits => _lazyOppPatchesWithUnits.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyMyPatches;
    public ImmutableList<Patch> MyPatches => _lazyMyPatches.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyMyRecyclers;

    public ImmutableList<Patch> MyRecyclers => _lazyMyRecyclers.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyMyPatchesWithUnits;
    public ImmutableList<Patch> MyPatchesWithUnits => _lazyMyPatchesWithUnits.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyMyPatchesWhereCanSpawn;
    public ImmutableList<Patch> MyPatchesWhereCanSpawn => _lazyMyPatchesWhereCanSpawn.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyMyPatchesWhereCanBuild;
    public ImmutableList<Patch> MyPatchesWhereCanBuild => _lazyMyPatchesWhereCanBuild.Value;

    private readonly Lazy<ImmutableList<Patch>> _lazyNeutralPatches;
    public ImmutableList<Patch> NeutralPatches => _lazyNeutralPatches.Value;

    public bool KeepGoing => MyPatches.Any(p => p.GetNeighbours().Any(n => !n.IsGrass && n.Owner != Owner.Me));
    public bool IsContested => MyPatches.Any(p => !p.IsRecycler) && OppPatches.Any(p => !p.IsRecycler);
    public int MyUnitSurplus => MyPatches.Sum(p => p.Units) - OppPatches.Sum(p => p.Units);

    public int MyMatterBudget { get; set; }

    public Island(GameContext ctx)
    {
        GameContext = ctx;
        Patches = new HashSet<Patch>();
        MyMatterBudget = 0;

        _lazyOppPatches = new Lazy<ImmutableList<Patch>>(() => Patches.Where(p => p.Owner == Owner.Opponent).ToImmutableList());
        _lazyOppRecyclers = new Lazy<ImmutableList<Patch>>(() => OppPatches.Where(p => p.IsRecycler).ToImmutableList());
        _lazyOppPatchesWithUnits = new Lazy<ImmutableList<Patch>>(() => OppPatches.Where(p => p.Units > 0).ToImmutableList());
        _lazyMyPatches = new Lazy<ImmutableList<Patch>>(() => Patches.Where(p => p.Owner == Owner.Me).ToImmutableList());
        _lazyMyRecyclers = new Lazy<ImmutableList<Patch>>(() => MyPatches.Where(p => p.IsRecycler).ToImmutableList());
        _lazyMyPatchesWithUnits = new Lazy<ImmutableList<Patch>>(() => MyPatches.Where(p => p.Units > 0).ToImmutableList());
        _lazyMyPatchesWhereCanSpawn = new Lazy<ImmutableList<Patch>>(() => MyPatches.Where(p => p.CanSpawn).ToImmutableList());
        _lazyMyPatchesWhereCanBuild = new Lazy<ImmutableList<Patch>>(() => MyPatches.Where(p => p.CanBuild).ToImmutableList());
        _lazyNeutralPatches = new Lazy<ImmutableList<Patch>>(() => Patches.Where(p => p.Owner == Owner.None).ToImmutableList());
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

        foreach (var p in MyPatchesWhereCanBuild)
        {
            p.BuildScore = GetBuildScore(p);
        }
    }

    public int GetBuildScore(Patch patch)
    {
        var neighbours = patch.GetNeighbours();

        if (neighbours.Any(n => n.IsRecycler && n.ScrapAmount < patch.ScrapAmount))
        {
            // don't build next to a recycler that would destroy our new recycler
            return -1;
        }

        if (neighbours.Any(n => n.Y == patch.Y && n.Owner == Owner.Me && n.IsRecycler))
        {
            // don't build horizontally right next to my own recycler 
            return -1;
        }

        if (neighbours.Any(n =>
                n.Owner == Owner.Me && n.Units > 0 && n.GetNeighbours().Count(nn => nn.IsRecycler || nn.IsGrass) == 3))
        {
            // don't lock in my own units
            return -1;
        }

        var nrOfOppNeighbours = neighbours.Count(n => n.Owner == Owner.Opponent && !n.IsRecycler);

        var nrOfOppNeighbourUnits = neighbours.Where(n => n.Owner == Owner.Opponent && !n.IsRecycler).Sum(n => n.Units);

        int survivingNeighboursBonus = neighbours.Count(n => n.Owner == Owner.Me && n.ScrapAmount > patch.ScrapAmount);

        int score = patch.ScrapAmount * (nrOfOppNeighbours + 1) * (survivingNeighboursBonus + 1) * (nrOfOppNeighbourUnits + 1) + patch.HorizontalDistance;

        return score;
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

        // if(p.GameContext.RoundCounter < 1)
        //     return p.HorizontalDistance;

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
        }

        if (neighbours.Any(n => !n.IsGrass && n.Owner == Owner.None))
        {
            attackBonus = 5;
        }

        if (!IsContested)
        {
            return attackBonus;
        }

        return attackBonus + p.HorizontalDistance + spreadBonus;
    }

    // private Direction DetermineAttackingDirection()
    // {
    //     // move towards the half containing the highest nr of patches not owned by me

    //     var width = Patches.Max(patch => patch.X) - Patches.Min(patch => patch.X);
    //     var centreX = (int)Math.Floor((double)(width / 2));

    //     var countLeft = Patches.Count(p => !p.IsGrass && !p.IsRecycler && p.Owner != Owner.Me && p.X < centreX);
    //     var countRight = Patches.Count(p => !p.IsGrass && !p.IsRecycler && p.Owner != Owner.Me && p.X > centreX);

    //     return countRight > countLeft ? Direction.Right : Direction.Left;
    // }

    private Direction DetermineAttackingDirection()
    {
        var defaultDirection = Direction.Right;

        if (!MyPatchesWithUnits.Any()) return defaultDirection; // default, value is not really relevant in this case

        int myLeftMostUnitX = MyPatchesWithUnits.Min(p => p.X);
        int myRightMostUnitX = MyPatchesWithUnits.Max(p => p.X);

        int leftOppUnitCount = OppPatches.Where(p => p.X < myLeftMostUnitX).Sum(p => p.Units);
        int rightOppUnitCount = OppPatches.Where(p => p.X > myRightMostUnitX).Sum(p => p.Units);

        if (leftOppUnitCount > rightOppUnitCount) return Direction.Left;
        if (rightOppUnitCount > leftOppUnitCount) return Direction.Right;

        // we have a scenario where we are on the outside on both left and right i.e. leftOppUnitCount==rightOppUnitCount
        // in this case we need to work out wether to attack left or right based on how many of our units have "gone behind enemy lines" on left vs right

        if (!OppPatchesWithUnits.Any()) return defaultDirection; // default, value is not really relevant in this case

        int oppLeftMostUnitX = OppPatchesWithUnits.Min(p => p.X);
        int oppRightMostUnitX = OppPatchesWithUnits.Max(p => p.X);

        int leftMyUnitsBehindEnemyLinesCount = MyPatches.Where(p => p.X < oppLeftMostUnitX).Sum(p => p.Units);
        int rightMyUnitsBehindEnemyLinesCount = MyPatches.Where(p => p.X > oppRightMostUnitX).Sum(p => p.Units);

        if (rightMyUnitsBehindEnemyLinesCount > leftMyUnitsBehindEnemyLinesCount) return Direction.Left;
        if (leftMyUnitsBehindEnemyLinesCount > rightMyUnitsBehindEnemyLinesCount) return Direction.Right;

        return defaultDirection; // default
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