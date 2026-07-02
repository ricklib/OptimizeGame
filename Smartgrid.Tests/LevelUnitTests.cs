using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class LevelUnitTests
    {
        [Fact]
        public void WidthHeight_EmptyMap_AreZero()
        {
            var level = new Level();
            Assert.Equal(0, level.Width);
            Assert.Equal(0, level.Height);
        }

        [Fact]
        public void WidthHeight_MatchMapRows()
        {
            var level = new Level();
            level.MapRows.Add("G..H");
            level.MapRows.Add("....");
            level.MapRows.Add("....");

            Assert.Equal(4, level.Width);
            Assert.Equal(3, level.Height);
        }

        [Theory]
        [InlineData(TileType.Solar, 40)]
        [InlineData(TileType.Wind, 70)]
        [InlineData(TileType.Generator, 90)]
        [InlineData(TileType.House, 0)]
        [InlineData(TileType.Cable, 0)]
        [InlineData(TileType.Empty, 0)]
        public void OutputFor_ReturnsCorrectOutput(TileType source, int expected)
        {
            var phase = new Phase { SolarOutput = 40, WindOutput = 70, GeneratorOutput = 90 };
            Assert.Equal(expected, phase.OutputFor(source));
        }
    }
}
