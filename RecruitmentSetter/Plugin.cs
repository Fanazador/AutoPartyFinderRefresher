using Dalamud.Game;
//using Dalamud.Game.Gui.PartyFinder.Types;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.ChatMethods;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Events;
using ECommons.EzEventManager;
using ECommons.EzIpcManager;
using ECommons.GameHelpers;
using ECommons.Schedulers;
//using ECommons.SimpleGui;
using ECommons.Singletons;
using ECommons.SplatoonAPI;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Data.Parsing;
using Lumina.Excel.Sheets;
using RecruitmentSetter;
using RecruitmentSetter.Windows;
//using RecruitmentSetter;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Schema;
using XivCommon;
using static System.Net.Mime.MediaTypeNames;
namespace RecruitmentSetter;

public unsafe sealed class Plugin : IDalamudPlugin
{
  private const string CommandName = "/apr";
  private static readonly string CommandHelpMessage = """
    /apr - Toggle autoparty finder refresh
    /apr c|cfg|config - Toggle config window
    """;

  private static readonly int MAX_RETRY = 50;
  private static readonly int RETRY_DELAY = 50;

  public readonly WindowSystem WindowSystem = new("RecruitmentSetter");
  private ConfigWindow ConfigWindow { get; init; }
  public Configuration Config { get; init; }

  private bool IsPlayerLookingForGroup { get; set; } = false;

  internal volatile bool running = true;

  private double elapsedTime = 0;
  public Plugin(IDalamudPluginInterface pluginInterface)
  {
    pluginInterface.Create<Svc>();
    Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

    ConfigWindow = new ConfigWindow(this);

    WindowSystem.AddWindow(ConfigWindow);

    Svc.Commands.AddHandler(CommandName, new(OnCommand)
    {
      HelpMessage = CommandHelpMessage
    });

    // Tell the UI system that we want our windows to be drawn through the window system
    Svc.PluginInterface.UiBuilder.Draw += DrawUI;

    // This adds a button to the plugin installer entry of this plugin which allows
    // toggling the display status of the configuration ui
    Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

    Svc.Condition.ConditionChange += ConditionHandle;
    IsPlayerLookingForGroup = Svc.Condition[ConditionFlag.UsingPartyFinder];
    Svc.Framework.Update += StartTask;
    elapsedTime = 0;
    //StartTask(Svc.Framework);
  }

  public void Dispose()
  {
    // Unregister all actions to not leak anythign during disposal of plugin
    Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
    Svc.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
    Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

    WindowSystem.RemoveAllWindows();

    ConfigWindow.Dispose();

    Svc.Commands.RemoveHandler(CommandName);
    Svc.Condition.ConditionChange -= ConditionHandle;
    Svc.Framework.Update -= StartTask;
    running = false;
    IsPlayerLookingForGroup = false;
  }

  private void OnCommand(string command, string args)
  {
    
    if (args=="c" || args == "cfg" || args =="config")
    {
      ConfigWindow.Toggle();
      Svc.Log.Information("Toggling Config Window: " + IsPlayerLookingForGroup.ToString() + " " + (Convert.ToDouble(Config.RefreshMinuteInterval)).ToString());
      return;
    }
    if (args == "debug")
    {
      Svc.Log.Debug("Running: " + running.ToString() + " LFG: " + IsPlayerLookingForGroup.ToString() + " Interval: " + (Convert.ToDouble(Config.RefreshMinuteInterval)).ToString());
      return;
    }
    if (args == "test")
    {
      ExecuteTask();
      return;
    }
    Svc.Log.Information("Toggling Enable");
    running = !running;
  }


  public void DrawUI() => WindowSystem.Draw();
  public void ToggleConfigUi() => ConfigWindow.Toggle();

  void StartTask(IFramework framework)
  {
    if (!running || !IsPlayerLookingForGroup)
      return;
    if(elapsedTime > Convert.ToDouble(Config.RefreshMinuteInterval))
    {
      Svc.Log.Debug("Executing: " + elapsedTime + " " + (Convert.ToDouble(Config.RefreshMinuteInterval)).ToString());
      try
      {
        ExecuteTask();
        elapsedTime = 0;
      }
      catch (Exception e)
      {
        Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
      }
    }
    else
    {
      elapsedTime += framework.UpdateDelta.TotalMinutes;
      Svc.Log.Verbose("Updating elapsed Time: " + elapsedTime + " " + framework.UpdateDelta.TotalMinutes.ToString());
    }
  }

  void ConditionHandle(ConditionFlag flag, bool value)
  {
    if (flag == ConditionFlag.UsingPartyFinder)
      IsPlayerLookingForGroup = value;
  }

  void ExecuteTask()
  {
    Svc.Log.Information("Executing Task: " + Config.RefreshMinuteInterval.ToString());
    Task.Run(() =>
    {
      UIModule* uiModule = UIModule.Instance();
      try
      {
        uiModule->ExecuteMainCommand(57);
      }
      catch (Exception e)
      {
        Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
      }

      if (!TryWaitFor<AddonMaster.LookingForGroup>(out var group))
      {
        Svc.Log.Error("Failed to capture group window");
        return;
      }
      group.RecruitMembersOrDetails();
      if (!TryWaitFor<AddonMaster.LookingForGroupDetail>(out var groupDetail))
      {
        Svc.Log.Error("Failed to capture group detail window");
        return;
      }
      if (!TryEdit(groupDetail))
      {
        Svc.Log.Error("Failed to click on recruit button");
        return;
      }
      if (!TryWaitFor<AddonMaster.LookingForGroupCondition>(out var groupCondition))
      {
        Svc.Log.Error("Failed to capture group condition window");
        return;
      }
      ;
      if (!TryRecruit(groupCondition))
      {
        Svc.Log.Error("Failed to click on recruit button");
        return;
      }
      try
      {
        uiModule->ExecuteMainCommand(57);
      }
      catch (Exception e)
      {
        Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
      }
    });
  }
  private static bool TryWaitFor<T>(out T addonMaster) where T : IAddonMasterBase
  {
    for (var i = 0; !GenericHelpers.TryGetAddonMaster(out addonMaster) || !addonMaster.IsAddonReady; i++)
    {
      Thread.Sleep(RETRY_DELAY);
      if (i > MAX_RETRY)
      {
        return false;
      }
    }
    return true;
  }

  private static bool TryRecruit(AddonMaster.LookingForGroupCondition addon)
  {
    for (var i = 0; !addon.RecruitButton->IsEnabled; i++)
    {
      Thread.Sleep(RETRY_DELAY);
      if (i > MAX_RETRY)
      {
        return false;
      }
    }
    addon.Recruit();
    return true;
  }
  private static bool TryEdit(AddonMaster.LookingForGroupDetail addon)
  {
    for (var i = 0; !addon.JoinEditButton->IsEnabled; i++)
    {
      Thread.Sleep(RETRY_DELAY);
      if (i > MAX_RETRY)
      {
        return false;
      }
    }
    addon.JoinEdit();
    return true;
  }
}

