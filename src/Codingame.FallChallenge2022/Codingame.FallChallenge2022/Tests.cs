using System.ComponentModel.Design;
using Codingame.FallChallenge2022.Business;
using FluentAssertions;
using Xunit;

namespace Codingame.FallChallenge2022
{
    // introduce recyclers
    // DONE best spot for spawning
    // increase routing range and move away from brute force (move to diamond shape)
    // spread the robots into singles

    public class Tests
    {

        //0 ScrapAmount
        //1 Owner
        //2 Units
        //3 IsRecycler
        //4 CanBuild
        //5 CanSpawn 
        //6 InRangeOfRecycler 



        [Fact]
        public void ShouldGetNeighboursForCorner()
        {
            string m = "1 1 0 0 0 0 0"; // scrap (owned by Me)
            string n = "1 -1 0 0 0 0 0"; // scrap (owned by Nobody)

            var ctx = new GameContext(3, 3);

            var patchInputs = new List<string>
            {
                n, n, n,
                n, m, n,
                n, n, n
            };

            var writer = new ListWriter(patchInputs);

            ctx.LoadPatches(writer.ReadLine);

            ctx.PatchGrid[0, 0].GetNeighbours().Should().BeEquivalentTo(new List<object>
            {
                new { X = 1, Y = 0},
                new { X = 0, Y = 1}
            });
        }

        [Fact]
        public void ShouldGetNeighbours()
        {
            string m = "1 1 0 0 0 0 0"; // scrap (owned by Me)
            string n = "1 -1 0 0 0 0 0"; // scrap (owned by Nobody)

            var ctx = new GameContext(3, 3);

            var patchInputs = new List<string>
            {
                n, n, n,
                n, m, n,
                n, n, n
            };

            var writer = new ListWriter(patchInputs);

            ctx.LoadPatches(writer.ReadLine);

            ctx.PatchGrid[1, 1].GetNeighbours().Should().BeEquivalentTo(new List<object>
            {
                new { X = 1, Y = 0},
                new { X = 2, Y = 1},
                new { X = 1, Y = 2},
                new { X = 0, Y = 1},
            });
        }


        [Fact]
        public void ShouldGetNeighboursAndExludeGrass()
        {
            string x = "0 -1 0 0 0 0 0"; // grass
            string m = "1 1 0 0 0 0 0"; // scrap (owned by Me)
            string n = "1 -1 0 0 0 0 0"; // scrap (owned by Nobody)

            var ctx = new GameContext(3, 3);

            var patchInputs = new List<string>
            {
                n, n, n,
                x, m, x,
                n, x, n
            };

            var writer = new ListWriter(patchInputs);

            ctx.LoadPatches(writer.ReadLine);

            ctx.PatchGrid[1, 1].GetNeighbours().Should().BeEquivalentTo(new List<object>
            {
                new { X = 1, Y = 0 },
            });
        }
        

        [Fact]
        public void ShouldCreateContext()
        {
            var writer = new TemplateWriter("50 60");
            var ctx = new GameContext(writer.ReadLine);

            ctx.Width.Should().Be(50);
            ctx.Height.Should().Be(60);
            ctx.PatchGrid.Length.Should().Be(50 * 60);
        }

        [Fact]
        public void ShouldLoadMatter()
        {
            var ctx = new GameContext(2, 2);

            var writer = new TemplateWriter("20 30");
            ctx.LoadMatter(writer.ReadLine);

            ctx.MyMatter.Should().Be(20);
            ctx.OppMatter.Should().Be(30);
        }

        [Fact]
        public void ShouldLoadPatches()
         {
            // arrange
            var ctx = new GameContext(2, 4);

            var patchInputs = new List<string>()
            {
                "0 0 0 0 0 0 0",
                "1 0 0 0 0 0 0",
                "1 1 0 0 0 0 0",
                "1 1 1 0 0 0 0",
                "1 1 1 1 0 0 0",
                "1 1 1 1 1 0 0",
                "1 1 1 1 1 1 0",
                "1 1 1 1 1 1 1"
            };

            var writer = new ListWriter(patchInputs);

            // act
            ctx.LoadPatches(writer.ReadLine);

            // assert
            for (int y = 0; y < ctx.Height; y++)
            {
                for (int x = 0; x < ctx.Width; x++)
                {
                    ctx.PatchGrid[x, y].Should().BeEquivalentTo(new Patch(patchInputs[y * ctx.Width + x].Split(" "), x, y, ctx), 
                        options => options
                            .Excluding(p => p.SpawnScore)
                            .Excluding(p => p.NavigationScore));
                }
            }
        }
    }
}