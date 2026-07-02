using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class LevelDataUnitTests
    {
        private static string[] Lines(params string[] lines) => lines;

        [Fact]
        public void Parse_SingleLevel_ReadsAllFields()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "title: Kapotte kabel",
                "problem: Het huis heeft geen stroom.",
                "goal: Repareer de kabel.",
                "message: Sensors helpen storingen vinden.",
                "tools: cable, sensor",
                "phase: Normaal | solar=100 demand=30",
                "map:",
                "G.H",
                "..."
            );

            List<Level> levels = LevelData.Parse(lines);

            Level level = Assert.Single(levels);
            Assert.Equal("Kapotte kabel", level.Title);
            Assert.Equal("Het huis heeft geen stroom.", level.Problem);
            Assert.Equal("Repareer de kabel.", level.Goal);
            Assert.Equal("Sensors helpen storingen vinden.", level.Message);
            Assert.Equal(new[] { ToolType.Cable, ToolType.Sensor }, level.Tools);
            Assert.Equal(2, level.Height);
            Assert.Equal(3, level.Width);
            Assert.Equal("G.H", level.MapRows[0]);
            Assert.Equal("...", level.MapRows[1]);

            Phase phase = Assert.Single(level.Phases);
            Assert.Equal("Normaal", phase.Name);
            Assert.Equal(100, phase.SolarOutput);
            Assert.Equal(30, phase.HouseDemand);
        }

        [Fact]
        public void Parse_MultipleLevels_SeparatedByHeaderMarkers()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "title: Een",
                "map:",
                "G.H",
                "=== Level 2 ===",
                "title: Twee",
                "map:",
                "G#H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Assert.Equal(2, levels.Count);
            Assert.Equal("Een", levels[0].Title);
            Assert.Equal("Twee", levels[1].Title);
        }

        [Fact]
        public void Parse_CommentLines_AreIgnored()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "# this is a comment",
                "title: Met commentaar",
                "# another comment",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Level level = Assert.Single(levels);
            Assert.Equal("Met commentaar", level.Title);
        }

        [Fact]
        public void Parse_LineWithoutColon_IsIgnoredWithoutCrashing()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "this line has no colon",
                "title: Nog steeds oke",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Level level = Assert.Single(levels);
            Assert.Equal("Nog steeds oke", level.Title);
        }

        [Fact]
        public void Parse_Tools_SkipsUnknownAndDedupsRepeats()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "tools: cable, not-a-real-tool, cable, battery",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Level level = Assert.Single(levels);
            Assert.Equal(new[] { ToolType.Cable, ToolType.Battery }, level.Tools);
        }

        [Fact]
        public void Parse_MultiplePhases_AreReadInOrder()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "phase: Dag | solar=100 demand=30",
                "phase: Nacht | solar=0 demand=30 predictor",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Level level = Assert.Single(levels);
            Assert.Equal(2, level.Phases.Count);

            Assert.Equal("Dag", level.Phases[0].Name);
            Assert.Equal(100, level.Phases[0].SolarOutput);
            Assert.False(level.Phases[0].RequiresPredictor);

            Assert.Equal("Nacht", level.Phases[1].Name);
            Assert.Equal(0, level.Phases[1].SolarOutput);
            Assert.True(level.Phases[1].RequiresPredictor);
        }

        [Fact]
        public void Parse_PhaseWithNoParams_UsesDefaults()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "phase: Simpel",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);
            Phase phase = Assert.Single(levels).Phases[0];

            Assert.Equal("Simpel", phase.Name);
            Assert.Equal(new Phase().SolarOutput, phase.SolarOutput);
            Assert.Equal(new Phase().HouseDemand, phase.HouseDemand);
        }

        [Fact]
        public void Parse_LevelWithoutPhase_GetsOneDefaultPhase()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "title: Geen fase",
                "map:",
                "G.H"
            );

            List<Level> levels = LevelData.Parse(lines);

            Phase phase = Assert.Single(Assert.Single(levels).Phases);
            Assert.Equal("Normal", phase.Name); // Phase's own default
        }

        [Fact]
        public void Parse_MapRowsShorterThanWidest_ArePaddedWithDots()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "map:",
                "G....H",
                "..",
                "...."
            );

            List<Level> levels = LevelData.Parse(lines);
            Level level = Assert.Single(levels);

            Assert.Equal(6, level.Width);
            Assert.Equal("G....H", level.MapRows[0]);
            Assert.Equal("......", level.MapRows[1]);
            Assert.Equal("......", level.MapRows[2]);
        }

        [Fact]
        public void Parse_MapReading_StopsAtBlankLineOrNextHeader()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "map:",
                "G.H",
                "",
                "goal: dit hoort niet meer bij de map"
            );

            List<Level> levels = LevelData.Parse(lines);
            Level level = Assert.Single(levels);

            Assert.Single(level.MapRows);
            Assert.Equal("dit hoort niet meer bij de map", level.Goal);
        }

        [Fact]
        public void Parse_LevelWithoutMap_IsDropped_FallsBackToDefault()
        {
            string[] lines = Lines(
                "=== Level 1 ===",
                "title: Geen kaart"
            );

            List<Level> levels = LevelData.Parse(lines);

            // The header-only level has Height 0 and gets filtered out; since no
            // levels remain, Parse falls back to the built-in default level.
            Level level = Assert.Single(levels);
            Assert.NotEqual("Geen kaart", level.Title);
            Assert.True(level.Height > 0);
        }

        [Fact]
        public void Parse_EmptyInput_ReturnsFallbackLevel()
        {
            List<Level> levels = LevelData.Parse(System.Array.Empty<string>());

            Level level = Assert.Single(levels);
            Assert.True(level.Height > 0);
            Assert.True(level.Tools.Count > 0);
        }
    }
}
