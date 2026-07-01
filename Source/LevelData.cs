using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SmartGrid;

public static class LevelData
{
    public static List<Level> LoadAll()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Content", "levels.txt");
        try
        {
            if (File.Exists(path))
                return Parse(File.ReadAllLines(path));
        }
        catch (Exception e)
        {
            Console.WriteLine("Could not read levels.txt: " + e.Message);
        }

        // Fallback so the game always has at least one level to start.
        return new List<Level> { Fallback() };
    }

    public static List<Level> Parse(string[] lines)
    {
        var levels = new List<Level>();
        Level current = null;
        bool readingMap = false;

        foreach (string raw in lines)
        {
            string line = raw.TrimEnd('\r', '\n');

            if (readingMap)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("==="))
                {
                    readingMap = false;
                }
                else
                {
                    current?.MapRows.Add(line);
                    continue;
                }
            }

            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.StartsWith("#")) continue; // comment

            if (trimmed.StartsWith("==="))
            {
                current = new Level();
                levels.Add(current);
                continue;
            }

            if (current == null) continue;

            int colon = trimmed.IndexOf(':');
            if (colon < 0) continue;

            string key = trimmed.Substring(0, colon).Trim().ToLowerInvariant();
            string value = trimmed.Substring(colon + 1).Trim();

            switch (key)
            {
                case "title":    current.Title = value; break;
                case "problem":  current.Problem = value; break;
                case "goal":     current.Goal = value; break;
                case "message":  current.Message = value; break;
                case "tools":    ParseTools(current, value); break;
                case "phase":    current.Phases.Add(ParsePhase(value)); break;
                case "map":      readingMap = true; break;
            }
        }

        foreach (Level lvl in levels)
            Finish(lvl);

        levels.RemoveAll(l => l.Height == 0);
        return levels.Count > 0 ? levels : new List<Level> { Fallback() };
    }

    private static void ParseTools(Level level, string value)
    {
        foreach (string part in value.Split(','))
        {
            if (Tool.TryParse(part, out ToolType tool) && !level.Tools.Contains(tool))
                level.Tools.Add(tool);
        }
    }

    private static Phase ParsePhase(string value)
    {
        var phase = new Phase();
        string[] halves = value.Split('|');
        phase.Name = halves[0].Trim();

        if (halves.Length > 1)
        {
            string[] tokens = halves[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                int eq = token.IndexOf('=');
                if (eq < 0)
                {
                    if (token.Equals("predictor", StringComparison.OrdinalIgnoreCase))
                        phase.RequiresPredictor = true;
                    continue;
                }

                string pk = token.Substring(0, eq).Trim().ToLowerInvariant();
                if (!int.TryParse(token.Substring(eq + 1).Trim(),
                        NumberStyles.Integer, CultureInfo.InvariantCulture, out int pv))
                    continue;

                switch (pk)
                {
                    case "solar":     phase.SolarOutput = pv; break;
                    case "wind":      phase.WindOutput = pv; break;
                    case "generator": phase.GeneratorOutput = pv; break;
                    case "demand":    phase.HouseDemand = pv; break;
                }
            }
        }
        return phase;
    }

    private static void Finish(Level level)
    {
        if (level.Phases.Count == 0)
            level.Phases.Add(new Phase());

        int width = 0;
        foreach (string row in level.MapRows)
            if (row.Length > width) width = row.Length;

        for (int i = 0; i < level.MapRows.Count; i++)
            if (level.MapRows[i].Length < width)
                level.MapRows[i] = level.MapRows[i].PadRight(width, '.');
    }

    private static Level Fallback()
    {
        var lvl = new Level
        {
            Title = "Verbind het huis",
            Problem = "Het huis heeft geen stroom.",
            Goal = "Leg kabels van de centrale naar het huis.",
            Message = "Stroom stroomt van een bron via kabels naar de huizen."
        };
        lvl.Tools.Add(ToolType.Cable);
        lvl.Phases.Add(new Phase());
        lvl.MapRows.Add("............");
        lvl.MapRows.Add(".G........H.");
        lvl.MapRows.Add("............");
        return lvl;
    }
}
