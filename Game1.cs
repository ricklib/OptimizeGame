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
    // ============================================================= constants

    private const int H = 720;
    private const int InfoH = 130;
    private const int FooterH = 60;
    private const int GridMargin = 40;
    private const int MinTile = 12;
    private const int MaxTile = 58;
    private const float PhaseSeconds = 1.7f;

    // Base font sizes, defined in the same 720-tall virtual canvas space as
    // everything else. The actual rasterization size is these values * the
    // current window scale, so glyphs stay sharp instead of being stretched
    // up from a fixed small size.
    private const int SmallFontSize = 15;
    private const int BodyFontSize = 18;
    private const int StrongFontSize = 18;
    private const int TitleFontSize = 26;

    private static readonly Color BgTop = new(34, 41, 60);
    private static readonly Color BgBottom = new(17, 21, 33);
    private static readonly Color SlotEmpty = new(40, 48, 68);
    private static readonly Color SlotCable = new(58, 50, 40);
    private static readonly Color TextLight = new(234, 240, 250);
    private static readonly Color TextDim = new(160, 170, 192);
    private static readonly Color Accent = new(150, 230, 170);
    private static readonly Color Warm = new(255, 214, 92);
    private static readonly Color Dim = new(100, 100, 106);

    private enum FontKind { Small, Body, Strong, Title }

    // ============================================================= fields

    private readonly GraphicsDeviceManager _graphics;
    private readonly int _startLevel;

    private SpriteBatch _spriteBatch;
    private Texture2D _pixel;
    private TextureLibrary _textures;
    private GameState _state;

    private RenderTarget2D _renderTarget;
    private Rectangle _destinationRectangle;

    // The virtual canvas is always H (720) tall, but its width is recomputed
    // on every resize to match the window's aspect ratio exactly. That means
    // the canvas can be scaled up by a single uniform factor to exactly fill
    // the window - no leftover space (no bars) and no non-uniform stretch
    // (no squished/stretched tiles).
    private int _virtualWidth = 980;
    private float _scale = 1f;

    // FontSystems rasterize on demand at whatever size we ask for, which is how
    // we keep text crisp at any window size (see GetFont below).
    private FontSystem _fontSystemRegular;
    private FontSystem _fontSystemBold;

    private MouseState _mouse, _prevMouse;
    private KeyboardState _keys, _prevKeys;

    // Cached once per frame so Update/Draw/hit-testing all agree on the same geometry.
    private (int OffsetX, int OffsetY, int TileSize) _layout;

    private bool _solved;
    private int _poweredNow;
    private int _totalHouses;

    // ============================================================= construction

    public Game1(int startLevel = 0)
    {
        _startLevel = startLevel;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        _graphics.PreferredBackBufferWidth = _virtualWidth;
        _graphics.PreferredBackBufferHeight = H;
        _graphics.ApplyChanges();

        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnWindowResized;
        Window.Title = "WAT NOU? - hou het stroomnet draaiende";

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        RecomputeCanvas();

        string contentDir = Path.Combine(AppContext.BaseDirectory, "Content");
        _textures = new TextureLibrary(GraphicsDevice, Path.Combine(contentDir, "Textures"));

        _fontSystemRegular = new FontSystem();
        _fontSystemRegular.AddFont(File.ReadAllBytes(Path.Combine(contentDir, "Fonts", "DejaVuSans.ttf")));
        _fontSystemBold = new FontSystem();
        _fontSystemBold.AddFont(File.ReadAllBytes(Path.Combine(contentDir, "Fonts", "DejaVuSans-Bold.ttf")));

        _state = new GameState(LevelData.LoadAll());
        _state.BeginAt(_startLevel > 0 ? _startLevel - 1 : 0);
    }

    // ============================================================= update

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _mouse = Mouse.GetState();
        _keys = Keyboard.GetState();

        if (_keys.IsKeyDown(Keys.Escape))
            Exit();

        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;

        _layout = ComputeLayout(level);

        HandleLevelHotkeys(level);
        HandleGridInput(grid);
        AdvancePhase(level, dt);

        _solved = grid.IsSolved(level);
        _totalHouses = grid.TotalHouses;
        _poweredNow = grid.Simulate(level, _state.DisplayPhase);

        _prevMouse = _mouse;
        _prevKeys = _keys;
        base.Update(gameTime);
    }

    private void HandleLevelHotkeys(Level level)
    {
        if (KeyPressed(Keys.R)) _state.LoadCurrentLevel();
        if (KeyPressed(Keys.Right)) SwitchLevel(1);
        if (KeyPressed(Keys.Left)) SwitchLevel(-1);

        for (int i = 0; i < level.Tools.Count && i < 9; i++)
            if (KeyPressed(Keys.D1 + i))
                _state.SelectedTool = level.Tools[i];
    }

    // Click-and-drag by design: holding the button places/removes on every
    // tile the cursor passes over, not just on the initial click.
    private void HandleGridInput(Grid grid)
    {
        if (!TryTileAt(MousePoint, out int tx, out int ty))
            return;

        if (_mouse.LeftButton == ButtonState.Pressed && _state.SelectedTool.HasValue)
            grid.Place(tx, ty, _state.SelectedTool.Value);
        else if (_mouse.RightButton == ButtonState.Pressed)
            grid.Remove(tx, ty);
    }

    private void AdvancePhase(Level level, float dt)
    {
        _state.PhaseTimer += dt;
        if (level.Phases.Count <= 1 || _state.PhaseTimer < PhaseSeconds)
            return;

        _state.PhaseTimer = 0f;
        _state.DisplayPhase = (_state.DisplayPhase + 1) % level.Phases.Count;
    }

    private void SwitchLevel(int delta)
    {
        int next = _state.LevelIndex + delta;
        if (next < 0 || next >= _state.Levels.Count) return;
        _state.BeginAt(next);
    }

    // ============================================================= draw

    protected override void Draw(GameTime gameTime)
    {
        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;
        Phase phase = level.Phases[_state.DisplayPhase];

        // Pass 1: pixel-art scene (background, grid slots, icons) rendered to
        // the virtual canvas. Scaling this up with point-filtering is what
        // keeps the tile art crisp and blocky on purpose, and since the canvas
        // aspect ratio matches the window's, the scale-up is uniform (no
        // stretching) and fills the window exactly (no bars).
        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(BgBottom);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Gradient(new Rectangle(0, 0, _virtualWidth, H), BgTop, BgBottom);
        DrawInfoBarBackground();
        DrawGrid(grid, level, phase);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, _destinationRectangle, Color.White);
        _spriteBatch.End();

        // Pass 2: text, drawn straight to the backbuffer at native resolution.
        // Fonts are rasterized at (base size * window scale), so text stays
        // sharp at any window size instead of being stretched from a small
        // glyph like it would be if it went through the low-res render target.
        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        DrawInfoBarText(level, phase);
        DrawFooterText();
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawInfoBarBackground()
    {
        Fill(new Rectangle(0, 0, _virtualWidth, InfoH), new Color(38, 45, 64));
    }

    private void DrawInfoBarText(Level level, Phase phase)
    {
        const int padding = 16;
        const int panelWidth = 360;
        int rightX = _virtualWidth - panelWidth;

        Text(FontKind.Strong, $"LEVEL {_state.LevelIndex + 1} / {_state.Levels.Count}", 24, 10, Accent);
        Text(FontKind.Title, level.Title, 24, 28, TextLight);

        float y = 62;
        y += WrappedText(FontKind.Small, "Probleem: " + level.Problem, 24, y, 600, TextDim);
        WrappedText(FontKind.Small, "Doel: " + level.Goal, 24, y + 2, 600, TextLight);

        bool allPowered = _totalHouses > 0 && _poweredNow >= _totalHouses;
        Text(FontKind.Strong, "FASE: " + phase.Name, rightX, 14, Accent);
        Text(FontKind.Body, $"{_poweredNow} / {_totalHouses} huizen met stroom", rightX, 40, allPowered ? Accent : Warm);

        if (_solved)
            WrappedText(
                FontKind.Strong,
                "OPGELOST! (druk op \u2192 voor het volgende level)",
                rightX + padding,
                66,
                panelWidth - padding * 2,
                Accent);
    }

    private void DrawGrid(Grid grid, Level level, Phase phase)
    {
        (int ox, int oy, int tile) = _layout;

        for (int y = 0; y < level.Height; y++)
            for (int x = 0; x < level.Width; x++)
            {
                Tile t = grid.At(x, y);
                var slot = new Rectangle(ox + x * tile + 2, oy + y * tile + 2, tile - 4, tile - 4);
                Fill(slot, SlotColor(t));

                string key = IconFor(t);
                if (key == null) continue;

                Color tint = IconTint(t, phase);
                int pad = tile / 6;
                var icon = new Rectangle(slot.X + pad, slot.Y + pad, slot.Width - pad * 2, slot.Height - pad * 2);
                _spriteBatch.Draw(_textures.Get(key), icon, tint);
            }

        if (TryTileAt(MousePoint, out int hx, out int hy))
        {
            var hoverSlot = new Rectangle(ox + hx * tile + 2, oy + hy * tile + 2, tile - 4, tile - 4);
            Border(hoverSlot, 2, new Color(150, 190, 230));
        }
    }

    private void DrawFooterText()
    {
        string hint = _state.SelectedTool.HasValue
            ? Tool.Name(_state.SelectedTool.Value) + ": " + Tool.Hint(_state.SelectedTool.Value)
            : "Geen gereedschap geselecteerd.";
        Text(FontKind.Small, hint, 24, H - 54, TextDim);

        Text(FontKind.Small,
            "Linkermuisknop: plaatsen  \u00b7  rechtermuisknop: weghalen  \u00b7  cijfertoetsen: kies gereedschap  " +
            "\u00b7  R: level opnieuw  \u00b7  \u2190/\u2192: level wisselen",
            24, H - 30, TextDim);
    }

    // ============================================================= tile appearance

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

    // ============================================================= layout

    /// <summary>
    /// Computes the on-screen grid geometry, sizing tiles to fit the space
    /// between the info bar and footer (up to MaxTile) and centering the
    /// result both horizontally and vertically within that space.
    /// </summary>
    private (int OffsetX, int OffsetY, int TileSize) ComputeLayout(Level level)
    {
        int top = InfoH + 12;
        int availW = _virtualWidth - GridMargin * 2;
        int availH = H - FooterH - top;

        int tileByWidth = availW / Math.Max(level.Width, 1);
        int tileByHeight = availH / Math.Max(level.Height, 1);
        int tile = Math.Clamp(Math.Min(tileByWidth, tileByHeight), MinTile, MaxTile);

        int ox = (_virtualWidth - level.Width * tile) / 2;
        int oy = top + (availH - level.Height * tile) / 2;

        return (ox, oy, tile);
    }

    private bool TryTileAt(Point p, out int tx, out int ty)
    {
        (int ox, int oy, int tile) = _layout;
        tx = (int)Math.Floor((p.X - ox) / (float)tile);
        ty = (int)Math.Floor((p.Y - oy) / (float)tile);
        return p.X >= ox && p.Y >= oy && _state.Grid.InBounds(tx, ty);
    }

    // ============================================================= draw helpers

    private void Fill(Rectangle r, Color c) => _spriteBatch.Draw(_pixel, r, c);

    private void Border(Rectangle r, int thickness, Color c)
    {
        Fill(new Rectangle(r.X, r.Y, r.Width, thickness), c);
        Fill(new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);
        Fill(new Rectangle(r.X, r.Y, thickness, r.Height), c);
        Fill(new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);
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

    // Rasterizes at (base size * current window scale) so glyphs are drawn at
    // native resolution instead of being stretched up from a fixed small size.
    private DynamicSpriteFont GetFont(FontKind kind)
    {
        (FontSystem system, int baseSize) = kind switch
        {
            FontKind.Small => (_fontSystemRegular, SmallFontSize),
            FontKind.Body => (_fontSystemRegular, BodyFontSize),
            FontKind.Strong => (_fontSystemBold, StrongFontSize),
            FontKind.Title => (_fontSystemBold, TitleFontSize),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        int size = Math.Max(1, (int)Math.Round(baseSize * _scale));
        return system.GetFont(size);
    }

    // (x, y) are logical coordinates in the virtual canvas space, like
    // everywhere else in this class; this converts them to real screen pixels.
    private void Text(FontKind kind, string text, float x, float y, Color c)
    {
        DynamicSpriteFont font = GetFont(kind);
        var screenPos = new Vector2(
            _destinationRectangle.X + x * _scale,
            _destinationRectangle.Y + y * _scale);
        _spriteBatch.DrawString(font, text, screenPos, c);
    }

    /// <summary>Draws word-wrapped text and returns the logical height it used.</summary>
    private float WrappedText(FontKind kind, string text, float x, float y, float maxWidth, Color c)
    {
        DynamicSpriteFont font = GetFont(kind);
        float logicalLineHeight = font.MeasureString("Ag").Y / _scale;
        float startY = y;
        foreach (string line in WrapLines(font, text, maxWidth * _scale))
        {
            Text(kind, line, x, y, c);
            y += logicalLineHeight;
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
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0) yield return line;
    }

    // ============================================================= input helpers

    private Point MousePoint
    {
        get
        {
            return new Point(
                (int)((_mouse.X - _destinationRectangle.X) / _scale),
                (int)((_mouse.Y - _destinationRectangle.Y) / _scale));
        }
    }

    private bool KeyPressed(Keys k) => _keys.IsKeyDown(k) && _prevKeys.IsKeyUp(k);

    // ============================================================= window handling

    private void OnWindowResized(object sender, EventArgs e) => RecomputeCanvas();

    /// <summary>
    /// Recomputes the virtual canvas to match the window's current aspect
    /// ratio (fixed height, variable width) and rebuilds the render target
    /// at that size. Because the canvas aspect ratio always matches the
    /// window's, a single uniform scale maps one onto the other exactly:
    /// no leftover space to bar off, and no need to stretch axes unevenly.
    /// </summary>
    private void RecomputeCanvas()
    {
        int windowWidth = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferWidth);
        int windowHeight = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferHeight);

        _virtualWidth = Math.Max(200, (int)Math.Round(H * (windowWidth / (float)windowHeight)));
        _scale = windowHeight / (float)H;
        _destinationRectangle = new Rectangle(0, 0, windowWidth, windowHeight);

        _renderTarget?.Dispose();
        _renderTarget = new RenderTarget2D(
            GraphicsDevice,
            _virtualWidth,
            H,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);
    }
}