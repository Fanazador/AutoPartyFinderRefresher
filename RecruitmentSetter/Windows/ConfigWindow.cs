using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace RecruitmentSetter.Windows;

public class ConfigWindow : Window, IDisposable
{
  private readonly Configuration configuration;

  // We give this window a constant ID using ###.
  // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
  // and the window ID will always be "###XYZ counter window" for ImGui
  public ConfigWindow(Plugin plugin) : base("Recruiment Setter Setting###With a constant ID")
  {
    Flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse;

    Size = new Vector2(232, 90);
    SizeCondition = ImGuiCond.Always;

    configuration = plugin.Config;
  }

  public void Dispose() { }

  public override void PreDraw()
  {
  }

  public override void Draw()
  {
    // Can't ref a property, so use a local copy
    var configValue = configuration.RefreshMinuteInterval;
    if (ImGui.SliderUInt("Refresh Minute Interval", ref configValue, 1, 60))
    {
      configuration.RefreshMinuteInterval = configValue;
      // Can save immediately on change if you don't want to provide a "Save and Close" button
      configuration.Save();
    }
  }
}
