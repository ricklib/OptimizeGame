using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class TileUnitTests
    {
        [Theory]
        [InlineData(TileType.Generator, true)]
        [InlineData(TileType.Solar, true)]
        [InlineData(TileType.Wind, true)]
        [InlineData(TileType.Cable, true)]
        [InlineData(TileType.Empty, false)]
        [InlineData(TileType.House, false)]
        public void Conducts_MatchesTileType_WhenNothingPlaced(TileType type, bool expected)
        {
            var tile = new Tile(type);
            Assert.Equal(expected, tile.Conducts);
        }

        [Fact]
        public void Conducts_BrokenCable_OnlyAfterRepair()
        {
            var tile = new Tile(TileType.BrokenCable);
            Assert.False(tile.Conducts);

            tile.Repaired = true;
            Assert.True(tile.Conducts);
        }

        [Fact]
        public void Conducts_PlacedTool_OverridesUnderlyingTile()
        {
            // An empty tile normally doesn't conduct, but placing most tools on
            // it makes it conduct - that's how the player bridges gaps.
            var tile = new Tile(TileType.Empty) { Placed = ToolType.Cable };
            Assert.True(tile.Conducts);
        }

        [Fact]
        public void Conducts_PlacedSensor_DoesNotConduct()
        {
            var tile = new Tile(TileType.Empty) { Placed = ToolType.Sensor };
            Assert.False(tile.Conducts);
        }

        [Theory]
        [InlineData(TileType.Generator, true)]
        [InlineData(TileType.Solar, true)]
        [InlineData(TileType.Wind, true)]
        [InlineData(TileType.House, false)]
        [InlineData(TileType.Cable, false)]
        [InlineData(TileType.Empty, false)]
        [InlineData(TileType.BrokenCable, false)]
        public void IsSource_OnlyTrueForGeneratorSolarWind(TileType type, bool expected)
        {
            var tile = new Tile(type);
            Assert.Equal(expected, tile.IsSource);
        }

        [Theory]
        [InlineData(TileType.Empty, true)]
        [InlineData(TileType.BrokenCable, true)]
        [InlineData(TileType.House, false)]
        [InlineData(TileType.Generator, false)]
        [InlineData(TileType.Cable, false)]
        public void IsBuildable_OnlyEmptyOrBrokenCable(TileType type, bool expected)
        {
            var tile = new Tile(type);
            Assert.Equal(expected, tile.IsBuildable);
        }
    }
}
