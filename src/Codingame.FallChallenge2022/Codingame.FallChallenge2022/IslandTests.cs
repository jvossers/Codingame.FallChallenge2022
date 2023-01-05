using Codingame.FallChallenge2022.Business;
using FluentAssertions;
using FluentAssertions.Equivalency;
using Xunit;

namespace Codingame.FallChallenge2022;

public class IslandTests
{
    // todo: treat recycler as grass when it comes to island boundaries

    [Fact]
    public void ShouldFindIslandsAndTreatRecyclersAsBarriers()
    {
        string m = "1 1 0 0 0 0 0"; // scrap (owned by Me)
        string o = "1 0 0 0 0 0 0"; // scrap (owned by Opponent)
        string n = "1 -1 0 0 0 0 0"; // scrap (owned by Nobody)
        string r = "5 -1 0 1 0 0 0"; // recycler
        string x = "0 -1 0 0 0 0 0"; // grass


        var ctx = new GameContext(5, 5);

        var patchInputs = new List<string>
        {
            n, n, r, m, m,
            n, n, r, m, m,
            x, x, r, x, x,
            o, o, r, o, o,
            o, o, r, o, o
        };

        var writer = new ListWriter(patchInputs);

        ctx.LoadPatches(writer.ReadLine);

        ctx.Islands.Should().BeEquivalentTo(
            new List<Island>
            {
                new(ctx){
                    Patches = { 
                        ctx.PatchGrid[0, 0],
                        ctx.PatchGrid[1, 0],
                        ctx.PatchGrid[0, 1],
                        ctx.PatchGrid[1, 1]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[3, 0],
                        ctx.PatchGrid[4, 0],
                        ctx.PatchGrid[3, 1],
                        ctx.PatchGrid[4, 1]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[0, 3],
                        ctx.PatchGrid[1, 3],
                        ctx.PatchGrid[0, 4],
                        ctx.PatchGrid[1, 4]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[3, 3],
                        ctx.PatchGrid[4, 3],
                        ctx.PatchGrid[3, 4],
                        ctx.PatchGrid[4, 4]
                    }
                }
            }, (options) => options.Excluding(island => island.HorizontalAttackingDirection).Excluding(island => island.VerticalAttackingDirection));
    }

    [Fact]
    public void ShouldFindIslands()
    {
        string m = "1 1 0 0 0 0 0"; // scrap (owned by Me)
        string o = "1 0 0 0 0 0 0"; // scrap (owned by Opponent)
        string n = "1 -1 0 0 0 0 0"; // scrap (owned by Nobody)
        string x = "0 -1 0 0 0 0 0"; // grass


        var ctx = new GameContext(5, 5);

        var patchInputs = new List<string>
        {
            n, n, x, m, m,
            n, n, x, m, m,
            x, x, x, x, x,
            o, o, x, o, o,
            o, o, x, o, o
        };

        var writer = new ListWriter(patchInputs);

        ctx.LoadPatches(writer.ReadLine);

        ctx.Islands.Should().BeEquivalentTo(
            new List<Island>
            {
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[0, 0],
                        ctx.PatchGrid[1, 0],
                        ctx.PatchGrid[0, 1],
                        ctx.PatchGrid[1, 1]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[3, 0],
                        ctx.PatchGrid[4, 0],
                        ctx.PatchGrid[3, 1],
                        ctx.PatchGrid[4, 1]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[0, 3],
                        ctx.PatchGrid[1, 3],
                        ctx.PatchGrid[0, 4],
                        ctx.PatchGrid[1, 4]
                    }
                },
                new(ctx){
                    Patches = {
                        ctx.PatchGrid[3, 3],
                        ctx.PatchGrid[4, 3],
                        ctx.PatchGrid[3, 4],
                        ctx.PatchGrid[4, 4]
                    }
                }
            }
        , (options) => options.Excluding(island => island.HorizontalAttackingDirection).Excluding(island => island.VerticalAttackingDirection));
    }
}