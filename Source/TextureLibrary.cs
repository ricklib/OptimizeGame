using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmartGrid;

public class TextureLibrary
{
    private readonly GraphicsDevice _device;
    private readonly string _contentRoot;
    private readonly Dictionary<string, Texture2D> _cache = new();
    private readonly HashSet<string> _fallbackKeys = new();

    public static readonly Color UnknownKeyColor = new(220, 30, 200);

    public TextureLibrary(GraphicsDevice device, string contentRoot)
    {
        _device = device;
        _contentRoot = contentRoot;
    }

    public bool IsFallback(string key) => _fallbackKeys.Contains(key);

    public Texture2D Get(string key, Color? fallbackColor = null)
    {
        if (_cache.TryGetValue(key, out Texture2D existing))
            return existing;

        Texture2D loaded = TryLoadFromDisk(key);
        Texture2D texture = loaded ?? GeneratePlaceholder(key, fallbackColor ?? Icons.FallbackColorFor(key));

        _cache[key] = texture;
        return texture;
    }

    public void Reload()
    {
        foreach (Texture2D tex in _cache.Values)
            tex.Dispose();
        _cache.Clear();
        _fallbackKeys.Clear();
    }

    private Texture2D TryLoadFromDisk(string key)
    {
        string path = Path.Combine(_contentRoot, key.Replace('/', Path.DirectorySeparatorChar) + ".png");
        if (!File.Exists(path))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(path);
            return Texture2D.FromStream(_device, stream);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not load texture '{key}' from {path}: {e.Message}");
            return null;
        }
    }

    // ---------------------------------------------------------- placeholder

    private const int PlaceholderSize = 32;
    private const int CheckerCell = 8;

    private Texture2D GeneratePlaceholder(string key, Color tint)
    {
        _fallbackKeys.Add(key);

        var data = new Color[PlaceholderSize * PlaceholderSize];
        Color light = tint;
        Color dark = Shade(tint, 0.55f);
        Color border = Shade(tint, 0.3f);

        for (int y = 0; y < PlaceholderSize; y++)
        for (int x = 0; x < PlaceholderSize; x++)
        {
            bool onEdge = x == 0 || y == 0 || x == PlaceholderSize - 1 || y == PlaceholderSize - 1;
            bool checker = ((x / CheckerCell) + (y / CheckerCell)) % 2 == 0;
            data[y * PlaceholderSize + x] = onEdge ? border : (checker ? light : dark);
        }

        var texture = new Texture2D(_device, PlaceholderSize, PlaceholderSize);
        texture.SetData(data);
        return texture;
    }

    private static Color Shade(Color c, float amount) =>
        new((byte)(c.R * amount), (byte)(c.G * amount), (byte)(c.B * amount), c.A);
}
