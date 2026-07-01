using System;
using System.Collections.Generic;
using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SmartGrid;

public class Game1 : Game
{
    private const int W = 980;
    private const int H = 720;
    private const int InfoH = 130;
    private const int MaxTile = 58;
    private const float PhaseSeconds = 1.7f;

    private static readonly Color BgTop = new(34, 41, 60);
    private static readonly Color BgBottom = new(17, 21, 33);
    private static readonly Color SlotEmpty = new(40, 48, 68);
    private static readonly Color SlotCable = new(58, 50, 40);
    private static readonly Color TextLight = new(234, 240, 250);
    private static readonly Color TextDim = new(160, 170, 192);
    private static readonly Color Accent = new(150, 230, 170);
    private static readonly Color Warm = new(255, 214, 92);

    private readonly GraphicsDeviceManager _graphics;
    private readonly int _startLevel;
    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private TextureLibrary _textures;
    private GameState _state;

    private DynamicSpriteFont _fontSmall, _fontBody, _fontStrong, _fontTitle;

    private MouseState _mouse, _prevMouse;
    private KeyboardState _keys, _prevKeys;

    private bool _solved;
    private int _poweredNow;
    private int _totalHouses;

    public Game1(int startLevel = 0)
    {
        _startLevel = startLevel;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = W;
        _graphics.PreferredBackBufferHeight = H;
        _graphics.ApplyChanges();
        Window.Title = "WAT NOU? - hou het stroomnet draaiende";
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        string contentDir = Path.Combine(AppContext.BaseDirectory, "Content");
        _textures = new TextureLibrary(GraphicsDevice, Path.Combine(contentDir, "Textures"));

        var regular = new FontSystem();
        regular.AddFont(File.ReadAllBytes(Path.Combine(contentDir, "Fonts", "DejaVuSans.ttf")));
        var bold = new FontSystem();
        bold.AddFont(File.ReadAllBytes(Path.Combine(contentDir, "Fonts", "DejaVuSans-Bold.ttf")));
        _fontSmall = regular.GetFont(15);
        _fontBody = regular.GetFont(18);
        _fontStrong = bold.GetFont(18);
        _fontTitle = bold.GetFont(26);

        _state = new GameState(LevelData.LoadAll());
        _state.BeginAt(_startLevel > 0 ? _startLevel - 1 : 0);
    }

    // ==================================================================== update

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _mouse = Mouse.GetState();
        _keys = Keyboard.GetState();

        if (_keys.IsKeyDown(Keys.Escape)) Exit();

        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;

        if (KeyPressed(Keys.R)) _state.LoadCurrentLevel();
        if (KeyPressed(Keys.Right)) SwitchLevel(1);
        if (KeyPressed(Keys.Left)) SwitchLevel(-1);

        for (int i = 0; i < level.Tools.Count && i < 9; i++)
            if (KeyPressed(Keys.D1 + i))
                _state.SelectedTool = level.Tools[i];

        if (TryTileAt(MousePoint, out int tx, out int ty))
        {
            if (_mouse.LeftButton == ButtonState.Pressed && _state.SelectedTool.HasValue) grid.Place(tx, ty, _state.SelectedTool.Value);
            else if (_mouse.RightButton == ButtonState.Pressed) grid.Remove(tx, ty);
        }

        _state.PhaseTimer += dt;
        if (level.Phases.Count > 1 && _state.PhaseTimer >= PhaseSeconds)
        {
            _state.PhaseTimer = 0f;
            _state.DisplayPhase = (_state.DisplayPhase + 1) % level.Phases.Count;
        }

        _solved = grid.IsSolved(level);
        _totalHouses = grid.TotalHouses;
        _poweredNow = grid.Simulate(level, _state.DisplayPhase);

        _prevMouse = _mouse;
        _prevKeys = _keys;
        base.Update(gameTime);
    }

    private void SwitchLevel(int delta)
    {
        int next = _state.LevelIndex + delta;
        if (next < 0 || next >= _state.Levels.Count) return;
        _state.BeginAt(next);
    }

    // ====================================================================== draw

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BgBottom);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Gradient(new Rectangle(0, 0, W, H), BgTop, BgBottom);

        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;
        Phase phase = level.Phases[_state.DisplayPhase];

        DrawInfoBar(level, phase);

        (int ox, int oy, int tile) = Layout(level);
        DrawGrid(grid, level, phase, ox, oy, tile);

        DrawFooter(level);

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void DrawInfoBar(Level level, Phase phase)
    {
        Fill(new Rectangle(0, 0, W, InfoH), new Color(38, 45, 64));

        Text(_fontStrong, $"LEVEL {_state.LevelIndex + 1} / {_state.Levels.Count}", 24, 10, Accent);
        Text(_fontTitle, level.Title, 24, 28, TextLight);

        float y = 62;
        y += WrappedText(_fontSmall, "Probleem: " + level.Problem, 24, y, 600, TextDim);
        WrappedText(_fontSmall, "Doel: " + level.Goal, 24, y + 2, 600, TextLight);

        bool allPowered = _poweredNow >= _totalHouses && _totalHouses > 0;
        Text(_fontStrong, "FASE: " + phase.Name, W - 300, 14, Accent);
        Text(_fontBody, $"{_poweredNow} / {_totalHouses} huizen met stroom", W - 300, 40, allPowered ? Accent : Warm);

        if (_solved)
            Text(_fontStrong, "OPGELOST! (druk op \u2192 voor het volgende level)", W - 300, 66, Accent);
    }

    private void DrawGrid(Grid grid, Level level, Phase phase, int ox, int oy, int tile)
    {
        for (int y = 0; y < level.Height; y++)
        for (int x = 0; x < level.Width; x++)
        {
            Tile t = grid.At(x, y);
            var slot = new Rectangle(ox + x * tile + 2, oy + y * tile + 2, tile - 4, tile - 4);
            Fill(slot, SlotColor(t));

            string key = IconFor(t);
            if (key != null)
            {
                Color tint = IconTint(t, phase);
                int pad = tile / 6;
                var icon = new Rectangle(slot.X + pad, slot.Y + pad, slot.Width - pad * 2, slot.Height - pad * 2);
                _spriteBatch.Draw(_textures.Get(key), icon, tint);
            }
        }

        if (TryTileAt(MousePoint, out int hx, out int hy))
        {
            var slot = new Rectangle(ox + hx * tile + 2, oy + hy * tile + 2, tile - 4, tile - 4);
            Border(slot, 2, new Color(150, 190, 230));
        }
    }

    private void DrawFooter(Level level)
    {
        string hint = _state.SelectedTool.HasValue
            ? Tool.Name(_state.SelectedTool.Value) + ": " + Tool.Hint(_state.SelectedTool.Value)
            : "Geen gereedschap geselecteerd.";
        Text(_fontSmall, hint, 24, H - 54, TextDim);

        Text(_fontSmall,
            "Linkermuisknop: plaatsen  \u00b7  rechtermuisknop: weghalen  \u00b7  cijfertoetsen: kies gereedschap  " +
            "\u00b7  R: level opnieuw  \u00b7  \u2190/\u2192: level wisselen",
            24, H - 30, TextDim);
    }

    // =========================================================== tile appearance

    private static string IconFor(Tile t)
    {
        if (t.Placed.HasValue) return Icons.KeyFor(t.Placed.Value);
        return t.Type switch
        {
            TileType.Generator => Icons.Generator,
            TileType.Solar => Icons.Solar,
            TileType.Wind => Icons.Wind,
            TileType.House => Icons.House,
            TileType.BrokenCable when t.Revealed && !t.Repaired => Icons.BrokenCable,
            _ => null
        };
    }

    private static Color SlotColor(Tile t) =>
        t.Conducts && !t.Placed.HasValue && !t.IsSource && t.Type != TileType.House ? SlotCable : SlotEmpty;

    private static readonly Color Dim = new(100, 100, 106);

    private static Color IconTint(Tile t, Phase phase)
    {
        if (t.Placed.HasValue) return Color.White;
        return t.Type switch
        {
            TileType.House => t.Powered ? Color.White : Dim,
            TileType.Solar => phase.SolarOutput > 0 ? Color.White : Dim,
            TileType.Wind => phase.WindOutput > 0 ? Color.White : Dim,
            TileType.Generator => phase.GeneratorOutput > 0 ? Color.White : Dim,
            _ => Color.White
        };
    }

    // ====================================================================== layout

    private (int, int, int) Layout(Level level)
    {
        int top = InfoH + 12;
        int availH = H - 60 - top;
        int availW = W - 80;
        int tile = Math.Min(MaxTile, Math.Min(availW / level.Width, availH / level.Height));
        int ox = (W - level.Width * tile) / 2;
        int oy = top + (availH - level.Height * tile) / 2;
        return (ox, oy, tile);
    }

    private bool TryTileAt(Point p, out int tx, out int ty)
    {
        (int ox, int oy, int tile) = Layout(_state.CurrentLevel);
        tx = (int)Math.Floor((p.X - ox) / (float)tile);
        ty = (int)Math.Floor((p.Y - oy) / (float)tile);
        return p.X >= ox && p.Y >= oy && _state.Grid.InBounds(tx, ty);
    }

    // ======================================================================= draw helpers

    private void Fill(Rectangle r, Color c) => _spriteBatch.Draw(_pixel, r, c);

    private void Border(Rectangle r, int t, Color c)
    {
        Fill(new Rectangle(r.X, r.Y, r.Width, t), c);
        Fill(new Rectangle(r.X, r.Bottom - t, r.Width, t), c);
        Fill(new Rectangle(r.X, r.Y, t, r.Height), c);
        Fill(new Rectangle(r.Right - t, r.Y, t, r.Height), c);
    }

    private void Gradient(Rectangle r, Color top, Color bottom)
    {
        int steps = Math.Min(r.Height, 96);
        for (int i = 0; i < steps; i++)
        {
            float f = i / (float)(steps - 1);
            int y = r.Y + i * r.Height / steps;
            int h = r.Height / steps + 1;
            Fill(new Rectangle(r.X, y, r.Width, h), Color.Lerp(top, bottom, f));
        }
    }

    private void Text(DynamicSpriteFont font, string text, float x, float y, Color c)
        => _spriteBatch.DrawString(font, text, new Vector2(x, y), c);

    // Returns the height used, so callers can stack lines below it.
    private float WrappedText(DynamicSpriteFont font, string text, float x, float y, float maxWidth, Color c)
    {
        float lineHeight = font.MeasureString("Ag").Y;
        float startY = y;
        foreach (string line in WrapLines(font, text, maxWidth))
        {
            Text(font, line, x, y, c);
            y += lineHeight;
        }
        return y - startY;
    }

    private static IEnumerable<string> WrapLines(DynamicSpriteFont font, string text, float maxWidth)
    {
        string line = "";
        foreach (string word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (font.MeasureString(candidate).X > maxWidth && line.Length > 0)
            {
                yield return line;
                line = word;
            }
            else line = candidate;
        }
        if (line.Length > 0) yield return line;
    }

    // ========================================================================= input

    private Point MousePoint => new(_mouse.X, _mouse.Y);
    private bool LeftClick => _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;
    private bool RightClick => _mouse.RightButton == ButtonState.Pressed && _prevMouse.RightButton == ButtonState.Released;
    private bool KeyPressed(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);
}
