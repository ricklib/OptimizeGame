using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartGrid;

namespace Smartgrid.Tests
{
    public class GameStateUnitTests
    {
        private static List<Level> ThreeLevels()
        {
            var levels = new List<Level>();
            for (int i = 0; i < 3; i++)
            {
                Level level = TestLevels.Build(
                    new[] { "G.H" },
                    tools: i == 0 ? new[] { ToolType.Cable, ToolType.Sensor } : System.Array.Empty<ToolType>());
                level.Title = $"Level {i + 1}";
                levels.Add(level);
            }
            return levels;
        }

        [Fact]
        public void Constructor_StartsAtLevelZero_OnStartScreen()
        {
            var state = new GameState(ThreeLevels());

            Assert.Equal(0, state.LevelIndex);
            Assert.Equal(Screen.Start, state.Current);
        }

        [Fact]
        public void Begin_LoadsFirstLevel_AndSwitchesToPlaying()
        {
            var state = new GameState(ThreeLevels());

            state.Begin();

            Assert.Equal(Screen.Playing, state.Current);
            Assert.Equal(0, state.LevelIndex);
            Assert.NotNull(state.Grid);
            Assert.Equal("Level 1", state.CurrentLevel.Title);
        }

        [Fact]
        public void LoadCurrentLevel_SelectsFirstTool_WhenLevelHasTools()
        {
            var state = new GameState(ThreeLevels());

            state.BeginAt(0); // level 0 has [Cable, Sensor]

            Assert.Equal(ToolType.Cable, state.SelectedTool);
        }

        [Fact]
        public void LoadCurrentLevel_SelectsNoTool_WhenLevelHasNoTools()
        {
            var state = new GameState(ThreeLevels());

            state.BeginAt(1); // level 1 has no tools

            Assert.Null(state.SelectedTool);
        }

        [Fact]
        public void LoadCurrentLevel_ResetsPhaseTrackingFields()
        {
            var state = new GameState(ThreeLevels());
            state.BeginAt(0);
            state.DisplayPhase = 5;
            state.PhaseTimer = 3.2f;

            state.LoadCurrentLevel();

            Assert.Equal(0, state.DisplayPhase);
            Assert.Equal(0f, state.PhaseTimer);
        }

        [Theory]
        [InlineData(-5, 0)]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(99, 2)] // clamps to Levels.Count - 1
        public void BeginAt_ClampsIndexIntoValidRange(int requested, int expected)
        {
            var state = new GameState(ThreeLevels());

            state.BeginAt(requested);

            Assert.Equal(expected, state.LevelIndex);
            Assert.Equal(Screen.Playing, state.Current);
        }

        [Fact]
        public void IsLastLevel_TrueOnlyOnFinalLevel()
        {
            var state = new GameState(ThreeLevels());

            state.BeginAt(0);
            Assert.False(state.IsLastLevel);

            state.BeginAt(2);
            Assert.True(state.IsLastLevel);
        }

        [Fact]
        public void Advance_MovesToNextLevel_WhenNotLast()
        {
            var state = new GameState(ThreeLevels());
            state.BeginAt(0);

            state.Advance();

            Assert.Equal(1, state.LevelIndex);
            Assert.Equal(Screen.Playing, state.Current);
            Assert.Equal("Level 2", state.CurrentLevel.Title);
        }

        [Fact]
        public void Advance_OnLastLevel_GoesToEndScreen_WithoutChangingIndex()
        {
            var state = new GameState(ThreeLevels());
            state.BeginAt(2);

            state.Advance();

            Assert.Equal(2, state.LevelIndex);
            Assert.Equal(Screen.End, state.Current);
        }

        [Fact]
        public void RestartAll_ResetsToLevelZero_OnStartScreen()
        {
            var state = new GameState(ThreeLevels());
            state.BeginAt(2);

            state.RestartAll();

            Assert.Equal(0, state.LevelIndex);
            Assert.Equal(Screen.Start, state.Current);
        }
    }
}
