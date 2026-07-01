using Microsoft.Xna.Framework;

namespace SmartGrid;

public enum ToolType
{
    Cable,       // cable        - connects tiles and repairs breaks
    Sensor,      // sensor       - reveals hidden faults
    Switch,      // switch       - automatic reroute via another path
    Battery,     // battery      - stores power and supplies it on shortfall
    SmartMeter,  // smart meter  - demand response: flattens the peak
    Predictor,   // predictor    - warns of an upcoming dip
    V2G          // v2g          - electric cars feed power back
}

public static class Tool
{
    public static bool Conducts(ToolType t) => t != ToolType.Sensor;

    // Player-facing strings are Dutch - everything else here is English.
    public static string Name(ToolType t) => t switch
    {
        ToolType.Cable      => "Kabel",
        ToolType.Sensor     => "Slimme sensor",
        ToolType.Switch     => "Schakelaar",
        ToolType.Battery    => "Batterij",
        ToolType.SmartMeter => "Vraagsturing",
        ToolType.Predictor  => "Voorspeller",
        ToolType.V2G        => "V2G auto's",
        _ => t.ToString()
    };

    public static string ShortLabel(ToolType t) => t switch
    {
        ToolType.Cable      => "",        // drawn as a plain wire, no text
        ToolType.Sensor     => "Sensor",
        ToolType.Switch     => "Schakel",
        ToolType.Battery    => "Accu",
        ToolType.SmartMeter => "Vraag",
        ToolType.Predictor  => "AI",
        ToolType.V2G        => "V2G",
        _ => "?"
    };

    public static string Hint(ToolType t) => t switch
    {
        ToolType.Cable      => "Verbindt tegels en repareert kapotte kabels.",
        ToolType.Sensor     => "Maakt een verborgen kapotte kabel zichtbaar.",
        ToolType.Switch     => "Leidt de stroom automatisch om via een andere route.",
        ToolType.Battery    => "Slaat stroom op en levert die bij een tekort.",
        ToolType.SmartMeter => "Vlakt de vraagpiek af zodat de productie genoeg is.",
        ToolType.Predictor  => "Voorspelt een komende dip; laadt de batterij op tijd vol.",
        ToolType.V2G        => "Geparkeerde auto's leveren tijdelijk stroom terug.",
        _ => ""
    };

    public static Color Tint(ToolType t) => t switch
    {
        ToolType.Cable      => new Color(214, 158, 92),   // copper
        ToolType.Sensor     => new Color(72, 201, 176),   // turquoise
        ToolType.Switch     => new Color(155, 120, 220),  // purple
        ToolType.Battery    => new Color(96, 200, 110),   // green
        ToolType.SmartMeter => new Color(232, 132, 178),  // pink
        ToolType.Predictor  => new Color(110, 170, 240),  // light blue
        ToolType.V2G        => new Color(180, 210, 70),   // lime
        _ => Color.Gray
    };

    // Keys here are English (code), regardless of what's shown to the player.
    public static bool TryParse(string key, out ToolType tool)
    {
        switch (key.Trim().ToLowerInvariant())
        {
            case "cable":                        tool = ToolType.Cable;      return true;
            case "sensor":                        tool = ToolType.Sensor;     return true;
            case "switch":                        tool = ToolType.Switch;     return true;
            case "battery":                       tool = ToolType.Battery;    return true;
            case "meter":
            case "smartmeter":
            case "smart_meter":
            case "demand":
            case "demandresponse":
            case "demand_response":               tool = ToolType.SmartMeter; return true;
            case "predictor":                     tool = ToolType.Predictor;  return true;
            case "v2g":                           tool = ToolType.V2G;        return true;
            default:                              tool = ToolType.Cable;      return false;
        }
    }
}
