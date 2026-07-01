namespace SmartGrid;

public enum TileType
{
    Empty,        // '.'  empty: the player can build here
    House,        // 'H'  house: needs to receive power
    Generator,    // 'G'  power plant / generator (a source)
    Solar,        // 'S'  solar panel (a source)
    Wind,         // 'W'  wind turbine (a source)
    Cable,        // '#'  fixed power cable
    BrokenCable   // 'x'  broken cable (hidden at the start)
}

public class Tile
{
    public TileType Type;

    public ToolType? Placed;

    public bool Revealed;

    public bool Repaired;

    public bool Powered;

    public bool Flowing;

    public Tile(TileType type)
    {
        Type = type;
    }

    public bool IsSource =>
        Type == TileType.Generator || Type == TileType.Solar || Type == TileType.Wind;

    public bool Conducts
    {
        get
        {
            if (Placed.HasValue)
                return Tool.Conducts(Placed.Value);

            return Type switch
            {
                TileType.Generator => true,
                TileType.Solar => true,
                TileType.Wind => true,
                TileType.Cable => true,
                TileType.BrokenCable => Repaired,
                _ => false // Empty, House
            };
        }
    }

    public bool IsBuildable =>
        Type == TileType.Empty || Type == TileType.BrokenCable;
}
