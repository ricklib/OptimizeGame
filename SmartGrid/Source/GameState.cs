using System;
using System.Collections.Generic;

namespace SmartGrid;

public enum Screen
{
    Start,
    Playing,
    Won,
    Message,
    End
}

public class GameState
{
    public Screen Current = Screen.Start;

    public List<Level> Levels { get; }
    public int LevelIndex { get; private set; }

    public Grid Grid { get; private set; }
    public ToolType? SelectedTool { get; set; }

    // Phases auto-advance during display so the player sees the event happen.
    public int DisplayPhase;
    public float PhaseTimer;

    public Level CurrentLevel => Levels[LevelIndex];
    public bool IsLastLevel => LevelIndex >= Levels.Count - 1;

    public GameState(List<Level> levels)
    {
        Levels = levels;
        LevelIndex = 0;
    }

    public void Begin() => BeginAt(0);

    public void BeginAt(int index)
    {
        LevelIndex = Math.Clamp(index, 0, Levels.Count - 1);
        LoadCurrentLevel();
        Current = Screen.Playing;
    }

    public void LoadCurrentLevel()
    {
        Grid = new Grid(CurrentLevel);
        SelectedTool = CurrentLevel.Tools.Count > 0 ? CurrentLevel.Tools[0] : (ToolType?)null;
        DisplayPhase = 0;
        PhaseTimer = 0f;
    }

    public void Advance()
    {
        if (IsLastLevel)
        {
            Current = Screen.End;
        }
        else
        {
            LevelIndex++;
            LoadCurrentLevel();
            Current = Screen.Playing;
        }
    }

    public void RestartAll()
    {
        LevelIndex = 0;
        Current = Screen.Start;
    }
}
