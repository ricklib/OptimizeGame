using System.Collections.Generic;

namespace SmartGrid;

public class Grid
{
    public const int BatteryPower = 100;
    public const int V2GPower = 100;
    public const int MeterReduction = 50;

    public int Width { get; }
    public int Height { get; }

    private readonly Tile[,] _tiles;

    public Grid(Level level)
    {
        Width = level.Width;
        Height = level.Height;
        _tiles = new Tile[Width, Height];

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            _tiles[x, y] = new Tile(FromChar(level.MapRows[y][x]));
    }

    private static TileType FromChar(char c) => c switch
    {
        'G' => TileType.Generator,
        'S' => TileType.Solar,
        'W' => TileType.Wind,
        'H' => TileType.House,
        '#' => TileType.Cable,
        'x' => TileType.BrokenCable,
        'X' => TileType.BrokenCable,
        _ => TileType.Empty
    };

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
    public Tile At(int x, int y) => _tiles[x, y];

    public int TotalHouses
    {
        get
        {
            int n = 0;
            foreach (Tile t in _tiles)
                if (t.Type == TileType.House) n++;
            return n;
        }
    }

    // ---------------------------------------------------------------- editing

    public bool Place(int x, int y, ToolType tool)
    {
        if (!InBounds(x, y)) return false;
        Tile t = _tiles[x, y];

        // A cable on a revealed broken cable = a repair.
        if (tool == ToolType.Cable && t.Type == TileType.BrokenCable && t.Revealed && !t.Repaired)
        {
            t.Repaired = true;
            return true;
        }

        if (t.Type == TileType.Empty && !t.Placed.HasValue)
        {
            t.Placed = tool;
            if (tool == ToolType.Sensor)
                RevealAround(x, y);
            return true;
        }

        return false;
    }

    public bool Remove(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        Tile t = _tiles[x, y];

        if (t.Placed.HasValue)
        {
            t.Placed = null;
            return true;
        }
        if (t.Type == TileType.BrokenCable && t.Repaired)
        {
            t.Repaired = false;
            return true;
        }
        return false;
    }

    private void RevealAround(int cx, int cy)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = cx + dx, y = cy + dy;
            if (InBounds(x, y) && _tiles[x, y].Type == TileType.BrokenCable)
                _tiles[x, y].Revealed = true;
        }
    }

    // -------------------------------------------------------------- simulation

    public int Simulate(Level level, int phaseIndex)
    {
        var net = Analyse(level);
        Phase phase = level.Phases[phaseIndex];

        foreach (Tile t in _tiles)
        {
            t.Powered = false;
            t.Flowing = false;
        }

        int count = net.Count;
        var sufficient = new bool[count];

        for (int c = 0; c < count; c++)
        {
            Component comp = net.Components[c];

            int sourceProd = comp.Solar * phase.SolarOutput
                           + comp.Wind * phase.WindOutput
                           + comp.Generator * phase.GeneratorOutput;

            int demand = comp.Houses * phase.HouseDemand - comp.Meters * MeterReduction;
            if (demand < 0) demand = 0;

            int production = sourceProd + comp.V2G * V2GPower;

            // A battery only helps if it could charge somewhere, and only
            // covers an announced dip together with a predictor.
            bool batteryUsable = comp.CanCharge && (!phase.RequiresPredictor || comp.HasPredictor);
            if (demand > sourceProd && batteryUsable)
                production += comp.Batteries * BatteryPower;

            sufficient[c] = production > 0 && production >= demand;
        }

        int powered = 0;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (_tiles[x, y].Type != TileType.House) continue;
            foreach (int c in AdjacentNetworks(net.Id, x, y))
            {
                if (sufficient[c]) { _tiles[x, y].Powered = true; break; }
            }
            if (_tiles[x, y].Powered) powered++;
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int c = net.Id[x, y];
            if (c >= 0 && sufficient[c] && net.Components[c].Houses > 0 && _tiles[x, y].Conducts)
                _tiles[x, y].Flowing = true;
        }

        return powered;
    }

    public bool IsSolved(Level level)
    {
        int total = TotalHouses;
        if (total == 0) return false;
        for (int p = 0; p < level.Phases.Count; p++)
            if (Simulate(level, p) < total)
                return false;
        return true;
    }

    // --------------------------------------------------- network analysis (internal)

    private class Component
    {
        public int Solar, Wind, Generator;
        public int Batteries, V2G, Meters, Houses;
        public bool HasPredictor;
        public bool CanCharge;
    }

    private class NetworkInfo
    {
        public int[,] Id;
        public List<Component> Components = new();
        public int Count => Components.Count;
    }

    private NetworkInfo Analyse(Level level)
    {
        var info = new NetworkInfo { Id = new int[Width, Height] };
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            info.Id[x, y] = -1;

        var queue = new Queue<int>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (info.Id[x, y] != -1 || !_tiles[x, y].Conducts) continue;

            int id = info.Components.Count;
            var comp = new Component();
            info.Components.Add(comp);

            info.Id[x, y] = id;
            queue.Enqueue(y * Width + x);

            while (queue.Count > 0)
            {
                int p = queue.Dequeue();
                int px = p % Width, py = p / Width;
                Tally(comp, _tiles[px, py]);

                foreach ((int nx, int ny) in Neighbours(px, py))
                {
                    if (info.Id[nx, ny] == -1 && _tiles[nx, ny].Conducts)
                    {
                        info.Id[nx, ny] = id;
                        queue.Enqueue(ny * Width + nx);
                    }
                }
            }
        }

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (_tiles[x, y].Type != TileType.House) continue;
            foreach (int c in AdjacentNetworks(info.Id, x, y))
                info.Components[c].Houses++;
        }

        foreach (Component comp in info.Components)
        {
            foreach (Phase ph in level.Phases)
            {
                int prod = comp.Solar * ph.SolarOutput
                         + comp.Wind * ph.WindOutput
                         + comp.Generator * ph.GeneratorOutput;
                if (prod > comp.Houses * ph.HouseDemand)
                {
                    comp.CanCharge = true;
                    break;
                }
            }
        }

        return info;
    }

    private static void Tally(Component comp, Tile t)
    {
        if (t.Placed.HasValue)
        {
            switch (t.Placed.Value)
            {
                case ToolType.Battery:    comp.Batteries++; break;
                case ToolType.V2G:        comp.V2G++; break;
                case ToolType.SmartMeter: comp.Meters++; break;
                case ToolType.Predictor:  comp.HasPredictor = true; break;
            }
            return; // a placed element is never a source
        }

        switch (t.Type)
        {
            case TileType.Solar:     comp.Solar++; break;
            case TileType.Wind:      comp.Wind++; break;
            case TileType.Generator: comp.Generator++; break;
        }
    }

    private List<int> AdjacentNetworks(int[,] id, int x, int y)
    {
        var found = new List<int>();
        foreach ((int nx, int ny) in Neighbours(x, y))
        {
            int c = id[nx, ny];
            if (c >= 0 && !found.Contains(c))
                found.Add(c);
        }
        return found;
    }

    private IEnumerable<(int, int)> Neighbours(int x, int y)
    {
        if (x > 0) yield return (x - 1, y);
        if (x < Width - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < Height - 1) yield return (x, y + 1);
    }
}
