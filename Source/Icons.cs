using Microsoft.Xna.Framework;

namespace SmartGrid;

public static class Icons
{
    // World / source tiles
    public const string Generator = "sources/generator";
    public const string Solar = "sources/solar";
    public const string Wind = "sources/wind";
    public const string House = "house";
    public const string BrokenCable = "broken_cable";

    // Placed IT tools
    public const string Cable = "tools/cable";
    public const string Sensor = "tools/sensor";
    public const string Switch = "tools/switch";
    public const string Battery = "tools/battery";
    public const string SmartMeter = "tools/smart_meter";
    public const string Predictor = "tools/predictor";
    public const string V2G = "tools/v2g";

    // Event overlays
    public const string Cloud = "fx/cloud";

    private static readonly Color GeneratorColor = new(232, 156, 92);
    private static readonly Color SolarColor = new(255, 201, 66);
    private static readonly Color WindColor = new(214, 222, 236);
    private static readonly Color HouseColor = new(236, 200, 132);
    private static readonly Color BrokenCableColor = new(222, 86, 72);
    private static readonly Color CloudColor = new(225, 230, 240);

    public static Color FallbackColorFor(string key) => key switch
    {
        Generator => GeneratorColor,
        Solar => SolarColor,
        Wind => WindColor,
        House => HouseColor,
        BrokenCable => BrokenCableColor,
        Cloud => CloudColor,
        Cable => Tool.Tint(ToolType.Cable),
        Sensor => Tool.Tint(ToolType.Sensor),
        Switch => Tool.Tint(ToolType.Switch),
        Battery => Tool.Tint(ToolType.Battery),
        SmartMeter => Tool.Tint(ToolType.SmartMeter),
        Predictor => Tool.Tint(ToolType.Predictor),
        V2G => Tool.Tint(ToolType.V2G),
        _ => TextureLibrary.UnknownKeyColor
    };

    public static string KeyFor(ToolType tool) => tool switch
    {
        ToolType.Cable => Cable,
        ToolType.Sensor => Sensor,
        ToolType.Switch => Switch,
        ToolType.Battery => Battery,
        ToolType.SmartMeter => SmartMeter,
        ToolType.Predictor => Predictor,
        ToolType.V2G => V2G,
        _ => Cable
    };
}
