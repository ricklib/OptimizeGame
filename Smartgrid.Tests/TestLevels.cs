using System.Collections.Generic;
using Smartgrid;
using SmartGrid;

namespace Smartgrid.Tests
{
    internal static class TestLevels
    {
        public static Level Build(string[] mapRows, IEnumerable<Phase>? phases = null, IEnumerable<ToolType>? tools = null)
        {
            
            
            var level = new Level
            {
                Title = "Test level",
                Problem = "Test problem",
                Goal = "Test goal",
            };

            level.MapRows.AddRange(mapRows);

            if (phases != null)
                level.Phases.AddRange(phases);
            else
                level.Phases.Add(new Phase());

            if (tools != null)
                level.Tools.AddRange(tools);

            return level;
        }

        public static (Level Level, Grid Grid) BuildGrid(string[] mapRows, IEnumerable<Phase>? phases = null, IEnumerable<ToolType>? tools = null)
        {
            Level level = Build(mapRows, phases, tools);
            return (level, new Grid(level));
        }

    }
}