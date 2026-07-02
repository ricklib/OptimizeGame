using System.Collections.Generic;

namespace SmartGrid;

public class Phase
{
    public string Name = "Normal";

    public int SolarOutput = 100;
    public int WindOutput = 100;
    public int GeneratorOutput = 100;

    public int HouseDemand = 30;

    public bool RequiresPredictor = false;

    public int OutputFor(TileType source) => source switch
    {
        TileType.Solar => SolarOutput,
        TileType.Wind => WindOutput,
        TileType.Generator => GeneratorOutput,
        _ => 0
    };
}

public class Level
{
    public string Title = "";
    public string Problem = "";
    public string Goal = "";
    public string Message = "";

    public List<ToolType> Tools = new();
    public List<Phase> Phases = new();

    public List<string> MapRows = new();

    public int Width => MapRows.Count > 0 ? MapRows[0].Length : 0;
    public int Height => MapRows.Count;
}
