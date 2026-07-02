using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class GridUnitTests
    {
        // ---------------------------------------------------------- construction

        [Fact]
        public void Constructor_ParsesEveryKnownTileChar()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "GSWH#x.X" });

            Assert.Equal(TileType.Generator, grid.At(0, 0).Type);
            Assert.Equal(TileType.Solar, grid.At(1, 0).Type);
            Assert.Equal(TileType.Wind, grid.At(2, 0).Type);
            Assert.Equal(TileType.House, grid.At(3, 0).Type);
            Assert.Equal(TileType.Cable, grid.At(4, 0).Type);
            Assert.Equal(TileType.BrokenCable, grid.At(5, 0).Type);
            Assert.Equal(TileType.Empty, grid.At(6, 0).Type);
            Assert.Equal(TileType.BrokenCable, grid.At(7, 0).Type); // 'X' also broken cable
        }

        [Fact]
        public void TotalHouses_CountsAllHouseTiles()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[]
            {
            "H.H",
            "..H",
        });

            Assert.Equal(3, grid.TotalHouses);
        }

        [Fact]
        public void InBounds_RejectsOutOfRangeCoordinates()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "G.H" });

            Assert.True(grid.InBounds(0, 0));
            Assert.True(grid.InBounds(2, 0));
            Assert.False(grid.InBounds(-1, 0));
            Assert.False(grid.InBounds(3, 0));
            Assert.False(grid.InBounds(0, 1));
        }

        // ---------------------------------------------------------------- Place

        [Fact]
        public void Place_OnEmptyTile_Succeeds()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "..." });

            bool ok = grid.Place(1, 0, ToolType.Cable);

            Assert.True(ok);
            Assert.Equal(ToolType.Cable, grid.At(1, 0).Placed);
        }

        [Fact]
        public void Place_OnOccupiedTile_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "..." });
            grid.Place(1, 0, ToolType.Cable);

            bool ok = grid.Place(1, 0, ToolType.Battery);

            Assert.False(ok);
            Assert.Equal(ToolType.Cable, grid.At(1, 0).Placed); // unchanged
        }

        [Fact]
        public void Place_OnNonEmptyNonBrokenTile_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "G" });

            bool ok = grid.Place(0, 0, ToolType.Cable);

            Assert.False(ok);
        }

        [Fact]
        public void Place_OutOfBounds_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "." });

            Assert.False(grid.Place(5, 5, ToolType.Cable));
        }

        [Fact]
        public void Place_Sensor_RevealsBrokenCablesInSurroundingRing()
        {
            // x . . at (0,0..2); sensor placed at the empty tile (1,0) should
            // reveal the broken cable within its 3x3 neighbourhood but not
            // the one two tiles away.
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "x.x.." });
            // positions: 0=x (in range), 1=. (sensor here), 2=x (in range, dx=1), 3=. , 4=.
            // add a further-away broken cable to prove it's NOT revealed
            // (redo with an explicit far tile instead, see next test for range).

            grid.Place(1, 0, ToolType.Sensor);

            Assert.True(grid.At(0, 0).Revealed);
            Assert.True(grid.At(2, 0).Revealed);
        }

        [Fact]
        public void Place_Sensor_DoesNotRevealBrokenCablesOutsideRange()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { ".x...x" });
            // Sensor at (0,0): reveals x=0,1 (within its row neighbourhood).
            // The broken cable at x=5 is far outside the 3x3 ring.
            grid.Place(0, 0, ToolType.Sensor);

            Assert.True(grid.At(1, 0).Revealed);
            Assert.False(grid.At(5, 0).Revealed);
        }

        // --------------------------------------------------------------- Repair

        [Fact]
        public void Place_CableOnRevealedBrokenCable_RepairsIt()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { ".x" });
            grid.Place(0, 0, ToolType.Sensor); // reveals the broken cable at (1,0)

            bool ok = grid.Place(1, 0, ToolType.Cable);

            Assert.True(ok);
            Assert.True(grid.At(1, 0).Repaired);
            Assert.True(grid.At(1, 0).Conducts);
            // Repairing doesn't count as "placing a tool" on that tile.
            Assert.Null(grid.At(1, 0).Placed);
        }

        [Fact]
        public void Place_CableOnUnrevealedBrokenCable_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "x" });

            bool ok = grid.Place(0, 0, ToolType.Cable);

            Assert.False(ok);
            Assert.False(grid.At(0, 0).Repaired);
        }

        // --------------------------------------------------------------- Remove

        [Fact]
        public void Remove_PlacedTool_ClearsIt()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "." });
            grid.Place(0, 0, ToolType.Cable);

            bool ok = grid.Remove(0, 0);

            Assert.True(ok);
            Assert.Null(grid.At(0, 0).Placed);
        }

        [Fact]
        public void Remove_RepairedBrokenCable_UnrepairsIt()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { ".x" });
            grid.Place(0, 0, ToolType.Sensor);
            grid.Place(1, 0, ToolType.Cable); // repairs

            bool ok = grid.Remove(1, 0);

            Assert.True(ok);
            Assert.False(grid.At(1, 0).Repaired);
            Assert.False(grid.At(1, 0).Conducts);
        }

        [Fact]
        public void Remove_EmptyTile_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "." });

            Assert.False(grid.Remove(0, 0));
        }

        [Fact]
        public void Remove_OutOfBounds_Fails()
        {
            (Level _, Grid grid) = TestLevels.BuildGrid(new[] { "." });

            Assert.False(grid.Remove(9, 9));
        }

        // ------------------------------------------------------------ Simulate

        [Fact]
        public void Simulate_DirectlyConnectedHouse_IsPowered()
        {
            var phase = new Phase { SolarOutput = 100, GeneratorOutput = 100, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#H" }, new[] { phase });

            int powered = grid.Simulate(level, 0);

            Assert.Equal(1, powered);
            Assert.True(grid.At(2, 0).Powered);
        }

        [Fact]
        public void Simulate_DisconnectedHouse_IsNotPowered()
        {
            var phase = new Phase();
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G.H" }, new[] { phase });

            int powered = grid.Simulate(level, 0);

            Assert.Equal(0, powered);
            Assert.False(grid.At(2, 0).Powered);
        }

        [Fact]
        public void Simulate_PlayerBridgesGapWithCable_PowersHouse()
        {
            var phase = new Phase();
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G.H" }, new[] { phase });

            grid.Place(1, 0, ToolType.Cable);
            int powered = grid.Simulate(level, 0);

            Assert.Equal(1, powered);
        }

        [Fact]
        public void Simulate_DemandExceedsProduction_HouseNotPowered()
        {
            var phase = new Phase { GeneratorOutput = 20, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#H" }, new[] { phase });

            int powered = grid.Simulate(level, 0);

            Assert.Equal(0, powered);
        }

        [Fact]
        public void Simulate_SeparateNetworks_AreEvaluatedIndependently()
        {
            var phase = new Phase { GeneratorOutput = 100, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[]
            {
            "G#H...H", // left network is connected and sufficient
        }, new[] { phase });
            // The second H (index 6) has no source nearby -> stays unpowered,
            // while the first H (index 2) is powered - proves components don't
            // leak power into each other.

            int powered = grid.Simulate(level, 0);

            Assert.Equal(1, powered);
            Assert.True(grid.At(2, 0).Powered);
            Assert.False(grid.At(6, 0).Powered);
        }

        [Fact]
        public void Simulate_HouseAdjacentToTwoNetworks_PoweredIfEitherSuffices()
        {
            // H sits between two separate one-tile networks; only the right one
            // (Generator) can reach demand, the left one (weak Solar) can't.
            var phase = new Phase { SolarOutput = 0, GeneratorOutput = 100, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#H#G" }, new[] { phase });

            int powered = grid.Simulate(level, 0);

            Assert.Equal(1, powered);
            Assert.True(grid.At(2, 0).Powered);
        }

        [Fact]
        public void SmartMeter_ReducesDemand_MakesInsufficientNetworkSufficient()
        {
            var phase = new Phase { GeneratorOutput = 100, HouseDemand = 120 };
            // Bridge tile (index 2) left unplaced initially, then filled with a
            // SmartMeter to both bridge the gap and reduce demand.
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#.#H" }, new[] { phase });

            grid.Place(2, 0, ToolType.SmartMeter);
            int powered = grid.Simulate(level, 0);

            // demand = 120 - 50 (meter) = 70; production = 100 -> sufficient
            Assert.Equal(1, powered);
        }

        [Fact]
        public void SmartMeter_Absent_NetworkStaysInsufficient()
        {
            var phase = new Phase { GeneratorOutput = 100, HouseDemand = 120 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#.#H" }, new[] { phase });

            grid.Place(2, 0, ToolType.Cable); // bridges, but no demand reduction
            int powered = grid.Simulate(level, 0);

            Assert.Equal(0, powered);
        }

        [Fact]
        public void Battery_CoversShortfall_WhenNetworkCanChargeElsewhere()
        {
            var day = new Phase { Name = "Day", SolarOutput = 100, HouseDemand = 30 };
            var night = new Phase { Name = "Night", SolarOutput = 0, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#.#H" }, new[] { day, night });

            grid.Place(2, 0, ToolType.Battery);

            Assert.Equal(1, grid.Simulate(level, 0)); // day: solar alone is enough
            Assert.Equal(1, grid.Simulate(level, 1)); // night: battery covers it
        }

        [Fact]
        public void Battery_Absent_NightPhaseFails()
        {
            var day = new Phase { Name = "Day", SolarOutput = 100, HouseDemand = 30 };
            var night = new Phase { Name = "Night", SolarOutput = 0, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#.#H" }, new[] { day, night });

            grid.Place(2, 0, ToolType.Cable); // bridges, but no battery

            Assert.Equal(1, grid.Simulate(level, 0));
            Assert.Equal(0, grid.Simulate(level, 1));
        }

        [Fact]
        public void Battery_RequiresPredictor_WhenPhaseDemandsIt()
        {
            var day = new Phase { Name = "Day", SolarOutput = 100, HouseDemand = 30 };
            var dip = new Phase { Name = "Dip", SolarOutput = 0, HouseDemand = 30, RequiresPredictor = true };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#.#.#H" }, new[] { day, dip });

            grid.Place(2, 0, ToolType.Battery);
            grid.Place(4, 0, ToolType.Cable); // bridges, but is NOT a predictor

            Assert.Equal(1, grid.Simulate(level, 0));
            Assert.Equal(0, grid.Simulate(level, 1)); // battery alone isn't enough
        }

        [Fact]
        public void Battery_WithPredictor_CoversAnnouncedDip()
        {
            var day = new Phase { Name = "Day", SolarOutput = 100, HouseDemand = 30 };
            var dip = new Phase { Name = "Dip", SolarOutput = 0, HouseDemand = 30, RequiresPredictor = true };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#.#.#H" }, new[] { day, dip });

            grid.Place(2, 0, ToolType.Battery);
            grid.Place(4, 0, ToolType.Predictor);

            Assert.Equal(1, grid.Simulate(level, 0));
            Assert.Equal(1, grid.Simulate(level, 1));
        }

        [Fact]
        public void V2G_ActsAsDirectExtraProduction()
        {
            var phase = new Phase { GeneratorOutput = 20, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#.#H" }, new[] { phase });

            // 20 alone isn't enough for demand 30, but V2G adds 100 more.
            grid.Place(2, 0, ToolType.V2G);

            Assert.Equal(1, grid.Simulate(level, 0));
        }

        [Fact]
        public void RepairedBrokenCable_RestoresConnection()
        {
            var phase = new Phase();
            // Row 0 is the power path: G - . - x - . - H.
            // Row 1 just holds the sensor, one tile below the break, so revealing
            // it doesn't put a non-conducting Sensor tile *on* the path itself.
            (Level level, Grid grid) = TestLevels.BuildGrid(new[]
            {
            "G.x.H",
            ".....",
        }, new[] { phase });

            // Before repair: the broken cable blocks the connection.
            Assert.Equal(0, grid.Simulate(level, 0));

            grid.Place(2, 1, ToolType.Sensor); // reveals the break at (2,0)
            grid.Place(1, 0, ToolType.Cable);  // bridge before the break
            grid.Place(2, 0, ToolType.Cable);  // repairs the break
            grid.Place(3, 0, ToolType.Cable);  // bridge after the break

            Assert.Equal(1, grid.Simulate(level, 0));
        }

        // ------------------------------------------------------------ IsSolved

        [Fact]
        public void IsSolved_AllPhasesSufficient_ReturnsTrue()
        {
            var phase = new Phase();
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#H" }, new[] { phase });

            Assert.True(grid.IsSolved(level));
        }

        [Fact]
        public void IsSolved_AnyInsufficientPhase_ReturnsFalse()
        {
            var day = new Phase { Name = "Day", SolarOutput = 100, HouseDemand = 30 };
            var night = new Phase { Name = "Night", SolarOutput = 0, HouseDemand = 30 };
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "S#H" }, new[] { day, night });

            Assert.False(grid.IsSolved(level));
        }

        [Fact]
        public void IsSolved_NoHousesAtAll_ReturnsFalse()
        {
            var phase = new Phase();
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G#." }, new[] { phase });

            Assert.False(grid.IsSolved(level));
        }

        [Fact]
        public void IsSolved_BecomesTrueOnceGapIsBridged()
        {
            var phase = new Phase();
            (Level level, Grid grid) = TestLevels.BuildGrid(new[] { "G.H" }, new[] { phase });

            Assert.False(grid.IsSolved(level));

            grid.Place(1, 0, ToolType.Cable);

            Assert.True(grid.IsSolved(level));
        }
    }
}
