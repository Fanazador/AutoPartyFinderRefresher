using Dalamud.Configuration;
using Dalamud.IoC;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using System;

namespace RecruitmentSetter;

[Serializable]
public class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 0;

  public uint RefreshMinuteInterval { get; set; } = 10;

  // The below exist just to make saving less cumbersome
  public void Save()
  {
        Svc.PluginInterface.SavePluginConfig(this);
  }
}
