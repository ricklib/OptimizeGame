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
    private const int SolvedFooterH = 140;
    private const int GridMargin = 40;
    private const int MinTile = 12;
    private const int ToolbarIconSize = 56;
    private const int ToolbarSpacing = 14;
    private const int MaxTile = 58;
    private const float PhaseSeconds = 1.7f;
    private const int OnboardingStepCount = 3;
    private const float LevelCompleteDelaySeconds = 5f;

    // Base font sizes, defined in the same 720-tall virtual canvas space as
    // everything else. The actual rasterization size is these values * the
    // current window scale, so glyphs stay sharp instead of being stretched
    // up from a fixed small size.
    private const int SmallFontSize = 15;
    private const int BodyFontSize = 18;
    private const int StrongFontSize = 18;
    private const int TitleFontSize = 26;
    private const int HeroFontSize = 64;
    private const int SubHeroFontSize = 22;

    private static readonly Color BgTop = new(34, 41, 60);
    private static readonly Color BgBottom = new(17, 21, 33);
    private static readonly Color SlotEmpty = new(40, 48, 68);
    private static readonly Color SlotCable = new(58, 50, 40);
    private static readonly Color TextLight = new(234, 240, 250);
    private static readonly Color TextDim = new(160, 170, 192);
    private static readonly Color Accent = new(150, 230, 170);
    private static readonly Color Warm = new(255, 214, 92);
    private static readonly Color Dim = new(100, 100, 106);
    private static readonly Color PanelBg = new(38, 45, 64);
    private static readonly Color CloudTint = new(225, 230, 240, 220);

    private enum FontKind { Small, Body, Strong, Title, Hero, SubHero }

    private enum AppState { Menu, Onboarding, Playing, LevelComplete }

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

    // True right after a new level starts (either the very first level, or
    // advancing from the level-complete screen). While true, grid clicks are
    // ignored so that the mouse button still being held down from clicking
    // "Doorgaan"/the solved-footer button doesn't immediately place/remove a
    // tile on the new level the instant it appears. Cleared once both mouse
    // buttons are observed released.
    private bool _gridInputLocked;

    // Menu / onboarding flow.
    private AppState _appState = AppState.Menu;
    private int _onboardingStep;

    private static readonly string[] OnboardingLine1 =
    {
        "Leg verbindingen en plaats slimme IT",
        "Elk level heeft een klein probleem:",
        "Los het op met het juiste gereedschap",
    };

    private static readonly string[] OnboardingLine2 =
    {
        "zodat alle huizen hun stroom houden.",
        "Een wolk voor de zon, een vraagpiek of wegvallende wind.",
        "en ontdek hoe een echte smart grid werkt!",
    };

    // Level-complete flow: how long the "OPGELOST!" state is shown before the
    // lesson screen appears automatically (Enter skips the wait).
    private float _solvedTimer;

    // Short heading, "Resultaat" text (solved footer), and "Boodschap" text
    // (lesson screen) per level, in the same order as Levels.txt. Kept local
    // to Game1 rather than added to Level/LevelData so it doesn't depend on
    // their exact shape - move it there if you'd rather keep content and
    // code together.
    private static readonly (string Name, string Result, string Lesson)[] LevelLessons =
    {
        ("Kapotte kabel",
            "Het huis heeft weer stroom. Bekijk het resultaat.",
            "Slimme sensors geven realtime inzicht in storingen, zodat een fout snel gevonden en gemaakt wordt."),
        ("Wegvallende route",
            "De stroom wordt automatisch via de andere route omgeleid.",
            "Een smart grid kan zichzelf gedeeltelijk herstellen."),
        ("Bewolkte dag",
            "De batterijen leveren stroom wanneer de zonnepanelen minder produceren.",
            "Opslag maakt duurzame energie betrouwbaarder."),
        ("Avondspits",
            "Geen stroomstoring, ondanks de piek in de vraag.",
            "Slimme stroomnetwerken kunnen stroomstoringen voorkomen."),
        ("Wisselende wind",
            "Je ziet de toekomstige productie aankomen en kunt je voorbereiden.",
            "Voorspellende software helpt het net stabiel te houden."),
        ("Stroomtekort",
            "EV's leveren tijdelijk stroom terug om het tekort op te vangen.",
            "EV's kunnen onderdeel worden van het energienetwerk en tekorten voorkomen."),
        ("Virtuele energiecentrale",
            "Alle apparaten samen functioneren als één grote energiecentrale.",
            "Software kan duizenden kleine bronnen energie coördineren."),
        ("Slimme stad",
            "De stroomvoorziening blijft stabiel en duurzaam.",
            "Een smart grid combineert duurzame energie met slimme IT-oplossingen."),
    };

    private static (string Name, string Result, string Lesson) LessonFor(int levelIndex) =>
        levelIndex >= 0 && levelIndex < LevelLessons.Length
            ? LevelLessons[levelIndex]
            : ("Level voltooid", "Goed gedaan!", "Goed gedaan!");

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
        Window.Title = "WATT NU? - hou het slimme stroomnet draaiend";

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

        // GameState is created once the player actually starts the game (see
        // StartGame), so the menu/onboarding flow can render before any level
        // is loaded.
    }

    // ============================================================= update

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _mouse = Mouse.GetState();
        _keys = Keyboard.GetState();

        if (_keys.IsKeyDown(Keys.Escape))
            Exit();

        switch (_appState)
        {
            case AppState.Menu:
                UpdateMenu();
                break;
            case AppState.Onboarding:
                UpdateOnboarding();
                break;
            case AppState.Playing:
                UpdatePlaying(dt);
                break;
            case AppState.LevelComplete:
                UpdateLevelComplete();
                break;
        }

        _prevMouse = _mouse;
        _prevKeys = _keys;
        base.Update(gameTime);
    }

    private void UpdateMenu()
    {
        (_, Rectangle button) = ComputeMenuLayout();
        if (Confirmed(button))
            _appState = AppState.Onboarding;
    }

    private void UpdateOnboarding()
    {
        (_, Rectangle button) = ComputeOnboardingLayout();
        if (!Confirmed(button))
            return;

        if (_onboardingStep < OnboardingStepCount - 1)
            _onboardingStep++;
        else
            StartGame();
    }

    private void StartGame()
    {
        _state = new GameState(LevelData.LoadAll());
        _state.BeginAt(_startLevel > 0 ? _startLevel - 1 : 0);
        _appState = AppState.Playing;
        _gridInputLocked = true;
    }

    private void UpdatePlaying(float dt)
    {
        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;

        // Uses last frame's _solved so the grid doesn't jump size the same
        // frame it gets solved; the footer grows a frame later, which is
        // imperceptible.
        _layout = ComputeLayout(level, FooterHeightFor(_solved));

        HandleLevelHotkeys(level);
        HandleToolbarInput(level);
        HandleGridInput(grid);
        AdvancePhase(level, dt);

        bool wasSolved = _solved;
        _solved = grid.IsSolved(level);
        _totalHouses = grid.TotalHouses;
        _poweredNow = grid.Simulate(level, _state.DisplayPhase);

        if (_solved)
        {
            if (!wasSolved) _solvedTimer = 0f;
            _solvedTimer += dt;

            // Clicking "Wat heb ik geleerd?" (or Enter/Space) skips the wait;
            // otherwise the lesson screen appears on its own after the delay.
            Rectangle solvedButton = ComputeSolvedFooterButton();
            if (_solvedTimer >= LevelCompleteDelaySeconds || Confirmed(solvedButton))
                _appState = AppState.LevelComplete;
        }
        else
        {
            _solvedTimer = 0f;
        }
    }

    private static int FooterHeightFor(bool solved) => solved ? SolvedFooterH : FooterH;

    private void UpdateLevelComplete()
    {
        Rectangle button = ComputeLevelCompleteButton();
        if (Confirmed(button))
            AdvanceAfterLevelComplete();
    }

    private void AdvanceAfterLevelComplete()
    {
        int next = _state.LevelIndex + 1;
        _solvedTimer = 0f;

        if (next < _state.Levels.Count)
        {
            _state.BeginAt(next);
            _appState = AppState.Playing;
            _gridInputLocked = true;
        }
        else
        {
            // Finished the last level - back to the main menu.
            _appState = AppState.Menu;
        }
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
        if (_gridInputLocked)
        {
            // Stay locked until the player has fully released both mouse
            // buttons at least once - this is what actually discards the
            // "still held down from confirming the previous screen" state,
            // rather than just waiting out a single frame.
            if (_mouse.LeftButton == ButtonState.Released && _mouse.RightButton == ButtonState.Released)
                _gridInputLocked = false;
            else
                return;
        }

        if (!TryTileAt(MousePoint, out int tx, out int ty))
            return;

        if (_mouse.LeftButton == ButtonState.Pressed && _state.SelectedTool.HasValue)
            grid.Place(tx, ty, _state.SelectedTool.Value);
        else if (_mouse.RightButton == ButtonState.Pressed)
            grid.Remove(tx, ty);
    }

    // A single click (not a drag) selects a tool, mirroring the 1-9 hotkeys -
    // this is what lets the whole level be played with the mouse alone.
    private void HandleToolbarInput(Level level)
    {
        if (!MouseClicked)
            return;

        foreach ((ToolType tool, Rectangle rect) in ComputeToolbarItems(level))
        {
            if (rect.Contains(MousePoint))
            {
                _state.SelectedTool = tool;
                return;
            }
        }
    }

    /// <summary>
    /// Vertical stack of tool swatches along the left edge, below the info
    /// bar. Shared by the click-hit-test in Update and the two draw passes
    /// so all three always agree on the same geometry.
    /// </summary>
    private List<(ToolType Tool, Rectangle Rect)> ComputeToolbarItems(Level level)
    {
        var items = new List<(ToolType, Rectangle)>();
        int x = GridMargin;
        int y = InfoH + 20;
        foreach (ToolType tool in level.Tools)
        {
            items.Add((tool, new Rectangle(x, y, ToolbarIconSize, ToolbarIconSize)));
            y += ToolbarIconSize + ToolbarSpacing;
        }
        return items;
    }

    /// <summary>Total horizontal space the toolbar column reserves, including margins on both sides.</summary>
    private static int ToolbarReservedWidth => GridMargin + ToolbarIconSize + GridMargin;

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
        switch (_appState)
        {
            case AppState.Menu:
                DrawMenuScreen();
                break;
            case AppState.Onboarding:
                DrawOnboardingScreen();
                break;
            case AppState.Playing:
                DrawPlayingScreen();
                break;
            case AppState.LevelComplete:
                DrawLevelCompleteScreen();
                break;
        }

        base.Draw(gameTime);
    }

    private void DrawPlayingScreen()
    {
        Level level = _state.CurrentLevel;
        Grid grid = _state.Grid;
        Phase phase = level.Phases[_state.DisplayPhase];
        int footerHeight = FooterHeightFor(_solved);

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
        DrawToolbar(level);
        DrawToolbarTooltipBackground(level);
        DrawGrid(grid, level, phase);
        if (_solved)
            DrawSolvedFooterBackground(footerHeight);
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
        DrawToolbarLabels(level);
        DrawToolbarTooltipText(level);
        if (_solved)
            DrawSolvedFooterText(footerHeight);
        else
            DrawFooterText();
        _spriteBatch.End();
    }

    // ============================================================= toolbar (mouse tool picker)

    private void DrawToolbar(Level level)
    {
        foreach ((ToolType tool, Rectangle rect) in ComputeToolbarItems(level))
        {
            bool selected = _state.SelectedTool == tool;
            Fill(rect, selected ? new Color(60, 74, 100) : SlotEmpty);

            string key = Icons.KeyFor(tool);
            int pad = rect.Width / 6;
            var icon = new Rectangle(rect.X + pad, rect.Y + pad, rect.Width - pad * 2, rect.Height - pad * 2);
            _spriteBatch.Draw(_textures.Get(key), icon, Color.White);

            if (selected)
                Border(rect, 2, Accent);
            else if (rect.Contains(MousePoint))
                Border(rect, 2, new Color(150, 190, 230));
        }
    }

    private void DrawToolbarLabels(Level level)
    {
        var items = ComputeToolbarItems(level);
        for (int i = 0; i < items.Count; i++)
            Text(FontKind.Small, (i + 1).ToString(), items[i].Rect.X + 4, items[i].Rect.Y + 2, TextDim);
    }

    /// <summary>Returns the toolbar item currently under the mouse, if any.</summary>
    private (ToolType Tool, Rectangle Rect)? HoveredToolbarItem(Level level)
    {
        foreach ((ToolType tool, Rectangle rect) in ComputeToolbarItems(level))
            if (rect.Contains(MousePoint))
                return (tool, rect);
        return null;
    }

    private const int TooltipMaxTextWidth = 220;
    private const int TooltipPadding = 10;

    /// <summary>
    /// Layout (box + text) for the tooltip shown while hovering a toolbar
    /// item, or null if nothing is hovered. Shared by the background and
    /// text draw passes so they always agree on the same box.
    /// </summary>
    private (Rectangle Box, string Text)? ComputeToolbarTooltip(Level level)
    {
        (ToolType Tool, Rectangle Rect)? hovered = HoveredToolbarItem(level);
        if (hovered == null)
            return null;

        ToolType tool = hovered.Value.Tool;
        Rectangle itemRect = hovered.Value.Rect;
        string text = $"{Tool.Name(tool)}: {Tool.Hint(tool)}";

        int textHeight = (int)Math.Ceiling(MeasureWrappedHeight(FontKind.Small, text, TooltipMaxTextWidth));
        int boxW = TooltipMaxTextWidth + TooltipPadding * 2;
        int boxH = textHeight + TooltipPadding * 2;

        int x = itemRect.Right + 12;
        int y = itemRect.Y;
        // Keep the tooltip fully on-screen vertically; it never needs
        // horizontal clamping since the toolbar sits against the left edge.
        y = Math.Clamp(y, 8, H - boxH - 8);

        return (new Rectangle(x, y, boxW, boxH), text);
    }

    private void DrawToolbarTooltipBackground(Level level)
    {
        (Rectangle Box, string Text)? tooltip = ComputeToolbarTooltip(level);
        if (tooltip == null) return;

        Fill(tooltip.Value.Box, PanelBg);
        Border(tooltip.Value.Box, 2, Accent);
    }

    private void DrawToolbarTooltipText(Level level)
    {
        (Rectangle Box, string Text)? tooltip = ComputeToolbarTooltip(level);
        if (tooltip == null) return;

        Rectangle box = tooltip.Value.Box;
        WrappedText(FontKind.Small, tooltip.Value.Text, box.X + TooltipPadding, box.Y + TooltipPadding,
            box.Width - TooltipPadding * 2, TextLight);
    }

    private void DrawInfoBarBackground()
    {
        Fill(new Rectangle(0, 0, _virtualWidth, InfoH), PanelBg);
    }

    private void DrawInfoBarText(Level level, Phase phase)
    {
        int rightX = _virtualWidth - 360;

        Text(FontKind.Strong, $"LEVEL {_state.LevelIndex + 1} / {_state.Levels.Count}", 24, 10, Accent);
        Text(FontKind.Title, level.Title, 24, 28, TextLight);

        float y = 62;
        y += WrappedText(FontKind.Small, "Probleem: " + level.Problem, 24, y, 600, TextDim);
        WrappedText(FontKind.Small, "Doel: " + level.Goal, 24, y + 2, 600, TextLight);

        bool allPowered = _totalHouses > 0 && _poweredNow >= _totalHouses;
        Text(FontKind.Strong, "FASE: " + phase.Name, rightX, 14, Accent);
        Text(FontKind.Body, $"{_poweredNow} / {_totalHouses} huizen met stroom", rightX, 40, allPowered ? Accent : Warm);
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

    // ============================================================= solved footer

    private Rectangle ComputeSolvedFooterButton()
    {
        const int w = 260;
        const int h = 56;
        int bandTop = H - SolvedFooterH;
        int x = _virtualWidth - GridMargin - w;
        int y = bandTop + (SolvedFooterH - h) / 2;
        return new Rectangle(x, y, w, h);
    }

    private void DrawSolvedFooterBackground(int footerHeight)
    {
        int bandTop = H - footerHeight;
        Fill(new Rectangle(0, bandTop, _virtualWidth, footerHeight), PanelBg);
        Fill(new Rectangle(0, bandTop, _virtualWidth, 2), TextDim);

        var badgeCenter = new Point(GridMargin + 30, bandTop + footerHeight / 2);
        DrawCheckBadge(badgeCenter, 30, 3, Accent, BgBottom);

        DrawButtonChrome(ComputeSolvedFooterButton());
    }

    private void DrawSolvedFooterText(int footerHeight)
    {
        (_, string result, _) = LessonFor(_state.LevelIndex);
        Rectangle button = ComputeSolvedFooterButton();
        int bandTop = H - footerHeight;
        int textX = GridMargin + 70;

        Text(FontKind.Title, "Opgelost!", textX, bandTop + 22, TextLight);
        WrappedText(FontKind.Small, result, textX, bandTop + 58, button.X - textX - 24, TextDim);

        TextCenteredIn(button, FontKind.Strong, "Wat heb ik geleerd?", Accent);
    }

    // ============================================================= menu screen

    private (Rectangle Box, Rectangle Button) ComputeMenuLayout()
    {
        int boxW = Math.Clamp(_virtualWidth - 320, 320, 520);
        const int boxH = 220;
        var box = new Rectangle((_virtualWidth - boxW) / 2, 300, boxW, boxH);
        var button = new Rectangle(_virtualWidth / 2 - 130, box.Bottom + 40, 260, 64);
        return (box, button);
    }

    private void DrawMenuScreen()
    {
        (Rectangle box, Rectangle button) = ComputeMenuLayout();

        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(BgBottom);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Gradient(new Rectangle(0, 0, _virtualWidth, H), BgTop, BgBottom);
        DrawDiagramBox(box, showCloud: false, showBattery: false, housesPowered: true);
        DrawButtonChrome(button);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, _destinationRectangle, Color.White);
        _spriteBatch.End();

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        int cx = _virtualWidth / 2;
        TextCentered(FontKind.Hero, "WATT NU?", cx, 70, TextLight);
        TextCentered(FontKind.SubHero, "Houd het slimme stroomnet draaiend.", cx, 150, TextDim);
        TextCenteredIn(button, FontKind.Strong, "Start Spel", Accent);
        TextCentered(FontKind.Small, "Klik op Start Spel of druk op Enter.", cx, H - 40, TextDim);
        _spriteBatch.End();
    }

    // ============================================================= onboarding screen

    private (Rectangle Box, Rectangle Button) ComputeOnboardingLayout()
    {
        int boxW = Math.Clamp(_virtualWidth - 200, 400, 640);
        const int boxH = 280;
        var box = new Rectangle((_virtualWidth - boxW) / 2, 230, boxW, boxH);
        var button = new Rectangle(_virtualWidth / 2 - 110, box.Bottom + 60, 220, 56);
        return (box, button);
    }

    private void DrawOnboardingScreen()
    {
        (Rectangle box, Rectangle button) = ComputeOnboardingLayout();

        // Cloud rolls in from step 2 onward; the battery (the fix) only
        // appears once the player reaches the final step. Houses read as
        // "dimmed" only while the problem is on screen and unresolved.
        bool showCloud = _onboardingStep >= 1;
        bool showBattery = _onboardingStep >= 2;
        bool housesPowered = _onboardingStep != 1;

        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(BgBottom);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Gradient(new Rectangle(0, 0, _virtualWidth, H), BgTop, BgBottom);
        DrawDiagramBox(box, showCloud, showBattery, housesPowered);
        DrawButtonChrome(button);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, _destinationRectangle, Color.White);
        _spriteBatch.End();

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        int cx = _virtualWidth / 2;
        TextCentered(FontKind.Title, OnboardingLine1[_onboardingStep], cx, 60, TextLight);
        TextCentered(FontKind.Body, OnboardingLine2[_onboardingStep], cx, 96, TextDim);

        string label = _onboardingStep < OnboardingStepCount - 1 ? "Volgende" : "Start Spel";
        TextCenteredIn(button, FontKind.Strong, label, Accent);

        TextCentered(FontKind.Small, $"{_onboardingStep + 1} / {OnboardingStepCount}", cx, box.Bottom + 16, TextDim);
        TextCentered(FontKind.Small, "Klik op de knop of druk op Enter.", cx, H - 40, TextDim);
        _spriteBatch.End();
    }

    // ============================================================= level complete screen

    private Rectangle ComputeLevelCompleteButton() =>
        new(_virtualWidth / 2 - 110, 470, 220, 56);

    private void DrawLevelCompleteScreen()
    {
        (string name, _, string lesson) = LessonFor(_state.LevelIndex);
        Rectangle button = ComputeLevelCompleteButton();
        int cx = _virtualWidth / 2;
        var badgeCenter = new Point(cx, 150);
        const int badgeRadius = 42;

        GraphicsDevice.SetRenderTarget(_renderTarget);
        GraphicsDevice.Clear(BgBottom);

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        Gradient(new Rectangle(0, 0, _virtualWidth, H), BgTop, BgBottom);
        DrawCheckBadge(badgeCenter, badgeRadius, 3, Accent, PanelBg);
        DrawButtonChrome(button);
        _spriteBatch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _spriteBatch.Draw(_renderTarget, _destinationRectangle, Color.White);
        _spriteBatch.End();

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
        TextCentered(FontKind.Title, "Wat je hebt geleerd", cx, 220, TextLight);
        TextCentered(FontKind.Strong, name, cx, 254, Accent);
        WrappedTextCentered(FontKind.Body, lesson, cx, 296, Math.Min(560, _virtualWidth - 160), TextDim);
        TextCenteredIn(button, FontKind.Strong, "Doorgaan", Accent);
        TextCentered(FontKind.Small, "Klik op Doorgaan of druk op Enter.", cx, H - 40, TextDim);
        _spriteBatch.End();
    }

    // ============================================================= badge / checkmark drawing

    private void DrawCheckBadge(Point center, int radius, int ringThickness, Color ringColor, Color fillColor)
    {
        FillCircle(center, radius, ringColor);
        FillCircle(center, radius - ringThickness, fillColor);
        // The checkmark icon is white/alpha art, tinted here to match the
        // ring color, same as every other icon drawn via DrawIcon.
        DrawIcon(Icons.Check, center, (int)(radius * 1.3f), ringColor);
    }

    private void FillCircle(Point center, int radius, Color c)
    {
        for (int y = -radius; y <= radius; y++)
        {
            int dx = (int)Math.Sqrt(Math.Max(0, radius * radius - y * y));
            Fill(new Rectangle(center.X - dx, center.Y + y, dx * 2, 1), c);
        }
    }

    // ============================================================= shared mini smart-grid diagram
    //
    // Draws a small illustrative grid (solar source -> two houses) used by
    // both the start screen and onboarding. A cloud can partially cover the
    // solar panel (the "problem"), and a battery can be dropped onto the
    // branch feeding the second house (the "fix"), mirroring the actual
    // source/tool/house relationships from real levels.

    private void DrawDiagramBox(Rectangle box, bool showCloud, bool showBattery, bool housesPowered)
    {
        Border(box, 2, TextDim);

        int iconSize = Math.Clamp(Math.Min(box.Width, box.Height) / 6, 36, 64);
        const int lineThickness = 3;

        var solar = new Point(box.X + box.Width / 4, box.Y + box.Height / 2);
        var house1 = new Point(box.X + box.Width - box.Width / 6, box.Y + box.Height / 5);
        var house2 = new Point(box.X + box.Width / 2 + box.Width / 10, box.Y + box.Height - box.Height / 5);
        var battery = new Point(house2.X, box.Y + box.Height / 8);

        int trunkY = solar.Y;
        int branchTop = showBattery ? battery.Y : trunkY;

        // Horizontal trunk from the solar source across to house 1.
        Fill(new Rectangle(solar.X, trunkY - lineThickness / 2, house1.X - solar.X, lineThickness), TextDim);
        // Branch up into house 1.
        Fill(new Rectangle(house1.X - lineThickness / 2, house1.Y, lineThickness, trunkY - house1.Y), TextDim);
        // Branch down into house 2 (extends up to the battery when present).
        Fill(new Rectangle(house2.X - lineThickness / 2, branchTop, lineThickness, house2.Y - branchTop), TextDim);

        Color houseTint = housesPowered ? Color.White : Dim;
        DrawIcon(Icons.House, house1, iconSize, houseTint);
        DrawIcon(Icons.House, house2, iconSize, houseTint);

        if (showCloud)
            DrawIcon(Icons.Cloud, solar, (int)(iconSize * 1.5f), CloudTint);

        DrawIcon(Icons.Solar, solar, iconSize, Color.White);

        if (showBattery)
            DrawIcon(Icons.Battery, battery, iconSize, Color.White);
    }

    private void DrawIcon(string key, Point center, int size, Color tint)
    {
        var rect = new Rectangle(center.X - size / 2, center.Y - size / 2, size, size);
        _spriteBatch.Draw(_textures.Get(key), rect, tint);
    }

    private void DrawButtonChrome(Rectangle button)
    {
        Border(button, 2, Accent);
        Fill(Inset(button, 2), PanelBg);
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
    private (int OffsetX, int OffsetY, int TileSize) ComputeLayout(Level level, int footerHeight)
    {
        int top = InfoH + 12;
        int left = ToolbarReservedWidth;
        int availW = _virtualWidth - left - GridMargin;
        int availH = H - footerHeight - top;

        int tileByWidth = availW / Math.Max(level.Width, 1);
        int tileByHeight = availH / Math.Max(level.Height, 1);
        int tile = Math.Clamp(Math.Min(tileByWidth, tileByHeight), MinTile, MaxTile);

        int ox = left + (availW - level.Width * tile) / 2;
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

    private static Rectangle Inset(Rectangle r, int amount) =>
        new(r.X + amount, r.Y + amount, r.Width - amount * 2, r.Height - amount * 2);

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
            FontKind.Hero => (_fontSystemBold, HeroFontSize),
            FontKind.SubHero => (_fontSystemRegular, SubHeroFontSize),
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

    /// <summary>Draws text horizontally centered on centerX (logical coordinates).</summary>
    private void TextCentered(FontKind kind, string text, float centerX, float y, Color c)
    {
        DynamicSpriteFont font = GetFont(kind);
        float logicalWidth = font.MeasureString(text).X / _scale;
        Text(kind, text, centerX - logicalWidth / 2f, y, c);
    }

    /// <summary>Draws text centered both horizontally and vertically within a logical rectangle.</summary>
    private void TextCenteredIn(Rectangle rect, FontKind kind, string text, Color c)
    {
        DynamicSpriteFont font = GetFont(kind);
        Vector2 size = font.MeasureString(text);
        float logicalW = size.X / _scale;
        float logicalH = size.Y / _scale;
        float x = rect.X + (rect.Width - logicalW) / 2f;
        float y = rect.Y + (rect.Height - logicalH) / 2f;
        Text(kind, text, x, y, c);
    }

    /// <summary>Draws word-wrapped text, centering each line on centerX.</summary>
    private void WrappedTextCentered(FontKind kind, string text, float centerX, float y, float maxWidth, Color c)
    {
        DynamicSpriteFont font = GetFont(kind);
        float logicalLineHeight = font.MeasureString("Ag").Y / _scale;
        foreach (string line in WrapLines(font, text, maxWidth * _scale))
        {
            TextCentered(kind, line, centerX, y, c);
            y += logicalLineHeight;
        }
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

    /// <summary>Measures the logical height word-wrapped text would take, without drawing it.</summary>
    private float MeasureWrappedHeight(FontKind kind, string text, float maxWidth)
    {
        DynamicSpriteFont font = GetFont(kind);
        float logicalLineHeight = font.MeasureString("Ag").Y / _scale;
        int lineCount = 0;
        foreach (string _ in WrapLines(font, text, maxWidth * _scale))
            lineCount++;
        return lineCount * logicalLineHeight;
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

    private bool MouseClicked =>
        _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;

    /// <summary>True if the given on-screen button was clicked, or Enter/Space was pressed.</summary>
    private bool Confirmed(Rectangle button) =>
        (MouseClicked && button.Contains(MousePoint)) || KeyPressed(Keys.Enter) || KeyPressed(Keys.Space);

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