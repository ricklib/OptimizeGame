int startLevel = 0;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--level") int.TryParse(args[i + 1], out startLevel);

using var game = new SmartGrid.Game1(startLevel);
game.Run();
