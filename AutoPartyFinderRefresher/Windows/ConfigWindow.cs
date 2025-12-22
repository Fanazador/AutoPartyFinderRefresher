using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace AutoPartyFinderRefresher.Windows;

public class ConfigWindow : Window, IDisposable
{
  private readonly Configuration configuration;

  public uint Width = 500;
  public uint Height = 90;

  private const uint IntervalMin = 1;
  private const uint IntervalMax = 60;
  public ConfigWindow(AutoPartyFinderRefresher plugin) : base("Auto Party Finder Refresher Setting###APRConfig")
  {
    Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse;

    Size = new Vector2(Width, Height);

    configuration = plugin.Config;
  }

  public void Dispose() { }

  public override void PreDraw()
  {
  }

  public override void Draw()
  {
    var intervalValue = configuration.RefreshMinuteInterval;
    if (ImGui.SliderUInt("Refresh Minute Interval", ref intervalValue, IntervalMin, IntervalMax))
    {
      configuration.RefreshMinuteInterval = intervalValue;
      configuration.Save();
    }
  }
}
