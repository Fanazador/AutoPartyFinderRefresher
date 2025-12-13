using Dalamud.Configuration;
using ECommons.DalamudServices;
using System;

namespace AutoPartyFinderRefresher;

[Serializable]
public class Configuration : IPluginConfiguration
{
  public int Version { get; set; } = 1;

  public uint RefreshMinuteInterval { get; set; } = 10;

  public void Save()
  {
      Svc.PluginInterface.SavePluginConfig(this);
  }
}
