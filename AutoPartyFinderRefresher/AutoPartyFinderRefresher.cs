using AutoPartyFinderRefresher.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using TaskManager = ECommons.Automation.NeoTaskManager.TaskManager;
namespace AutoPartyFinderRefresher;

public unsafe sealed class AutoPartyFinderRefresher : IDalamudPlugin
{
  private const string CommandName = "/apr";
  private static readonly string CommandHelpMessage = """
    /apr - Toggle autoparty finder refresh
    /apr c | cfg | config - Toggle config window
    """;

  public readonly WindowSystem WindowSystem = new("AutoPartyFinderRefresher");
  private ConfigWindow ConfigWindow { get; init; }
  public Configuration Config { get; init; }

  private bool IsPlayerLookingForGroup { get; set; } = false;
  private TaskManager taskManager { get; set; }

  private static readonly UIModule* UiModule = UIModule.Instance();

  private bool running = true;

  private double elapsedTime = 0;

  public AutoPartyFinderRefresher(IDalamudPluginInterface pluginInterface)
  {
    pluginInterface.Create<Svc>();
    Config = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

    ConfigWindow = new ConfigWindow(this);

    WindowSystem.AddWindow(ConfigWindow);

    Svc.Commands.AddHandler(CommandName, new(OnCommand)
    {
      HelpMessage = CommandHelpMessage
    });

    Svc.PluginInterface.UiBuilder.Draw += DrawUI;
    Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

    Svc.Condition.ConditionChange += ConditionHandle;

    Svc.Framework.Update += StartTask;

    IsPlayerLookingForGroup = Svc.Condition[ConditionFlag.UsingPartyFinder];
    taskManager = new TaskManager(new TaskManagerConfiguration() { AbortOnTimeout = false, TimeoutSilently = true, TimeLimitMS = 10000, ShowDebug = true });
    elapsedTime = 0;
  }

  public void Dispose()
  {
    Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
    Svc.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;

    WindowSystem.RemoveAllWindows();

    ConfigWindow.Dispose();

    Svc.Commands.RemoveHandler(CommandName);

    Svc.Condition.ConditionChange -= ConditionHandle;

    Svc.Framework.Update -= StartTask;

    taskManager.Dispose();

    running = false;
    IsPlayerLookingForGroup = false;
    elapsedTime = 0;
  }

  private void OnCommand(string command, string args)
  {
    Svc.Log.Debug("Receiving command line: " + args);
    if (args == "c" || args == "cfg" || args == "config")
    {
      ConfigWindow.Toggle();
      return;
    }

#if DEBUG
    if (int.TryParse(args, out int value))
    {
      Svc.Log.Debug("Testing: " + value);
      DebugExecuteTask(value);
      return;
    }

    if (args == "debug")
    {
      Svc.Log.Debug("Running: " + running.ToString() + " LFG: " + IsPlayerLookingForGroup.ToString() + " Interval: " + (Convert.ToDouble(Config.RefreshMinuteInterval)).ToString());
      return;
    }
    if (args == "test")
    {
      Svc.Log.Information(args + " command received. Manually executing task.");
      ExecuteTask();
      return;
    }
#endif
    Svc.Log.Debug("Toggling Enable: " + !running);
    running = !running;
  }

  private void ConditionHandle(ConditionFlag flag, bool value)
  {
    if (flag == ConditionFlag.UsingPartyFinder)
    {
      IsPlayerLookingForGroup = value;
      if (!value)
        elapsedTime = 0;
    }
  }

  /// <summary>
  /// Wait for however long is the interval, once it's passed execute the refresh task.
  /// </summary>
  /// <param name="framework">Dalamud framwork</param>
  private void StartTask(IFramework framework)
  {
    if (!running || !IsPlayerLookingForGroup)
      return;

    if (elapsedTime > Convert.ToDouble(Config.RefreshMinuteInterval))
    {
      try
      {
        Svc.Log.Information(elapsedTime + " minute have passed executing task.");
        elapsedTime = 0;
        ExecuteTask();
      }
      catch (Exception e)
      {
        Svc.Log.Error(e.Message + "\n" + e.StackTrace ?? "");
      }
    }
    else
    {
      elapsedTime += framework.UpdateDelta.TotalMinutes;
    }
  }

  /// <summary>
  /// Using task manager queue commands to execute in order to refresh the pf timer.
  /// </summary>
  private void ExecuteTask()
  {
    const int PartyFinderCommand = 57;
    const int RecruitMemberCommand = 14;
    const int JoinOrEditCommand = 12;
    const int ResetSlotCommand = 32;
    const int ApplyChangeCommand = 0;

    AtkUnitBase* group = null;
    AtkUnitBase* groupDetail = null;
    AtkUnitBase* groupCondition = null;

    taskManager.Enqueue((Action)(() => GenericHelpers.TryGetAddonByName("LookingForGroup", out group)));
    taskManager.Enqueue(() => { if (group == null) UiModule->ExecuteMainCommand(PartyFinderCommand); });
    taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroup", out group) && GenericHelpers.IsAddonReady(group));
    taskManager.Enqueue(() => Callback.Fire(group, true, RecruitMemberCommand));
    taskManager.EnqueueDelay(500);
    taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupDetail", out groupDetail) && GenericHelpers.IsAddonReady(groupDetail));
    taskManager.Enqueue(() => Callback.Fire(groupDetail, true, JoinOrEditCommand));
    taskManager.EnqueueDelay(700);
    taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupCondition", out groupCondition) && GenericHelpers.IsAddonReady(groupCondition));
    taskManager.Enqueue(() => Callback.Fire(groupCondition, true, ResetSlotCommand));
    taskManager.EnqueueDelay(700);
    taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupCondition", out groupCondition) && GenericHelpers.IsAddonReady(groupCondition));
    taskManager.Enqueue(() => Callback.Fire(groupCondition, true, ApplyChangeCommand));
  }

#if DEBUG
  /// <summary>
  /// Passing in the command to test
  /// </summary>
  private void DebugExecuteTask(Int32 command)
  {
    const int PartyFinderCommand = 57;
    const int RecruitMemberCommand = 14;
    const int JoinOrEditCommand = 12;
    const int OnePlayerPerJobCommand = 23;
    const int ResetSlotCommand = 32;
    const int ApplyChangeCommand = 0;

    AtkUnitBase* group = null;
    AtkUnitBase* groupDetail = null;
    AtkUnitBase* groupCondition = null;

    //taskManager.Enqueue((Action)(() => GenericHelpers.TryGetAddonByName("LookingForGroup", out group)));
    //taskManager.Enqueue(() => { if (group == null) UiModule->ExecuteMainCommand(PartyFinderCommand); });
    //taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroup", out group) && GenericHelpers.IsAddonReady(group));
    //taskManager.Enqueue(() => Callback.Fire(group, true, RecruitMemberCommand));
    //taskManager.EnqueueDelay(500);
    //taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupDetail", out groupDetail) && GenericHelpers.IsAddonReady(groupDetail));
    //taskManager.Enqueue(() => Callback.Fire(groupDetail, true, JoinOrEditCommand));
    //taskManager.EnqueueDelay(700);
    Svc.Log.Information("Command: " + command);
    taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupCondition", out groupCondition) && GenericHelpers.IsAddonReady(groupCondition));
    taskManager.Enqueue(() => Callback.Fire(groupCondition, true, command));
    //taskManager.EnqueueDelay(700);
    //taskManager.Enqueue(() => GenericHelpers.TryGetAddonByName("LookingForGroupCondition", out groupCondition) && GenericHelpers.IsAddonReady(groupCondition));
    //taskManager.Enqueue(() => Callback.Fire(groupCondition, true, ApplyChangeCommand));
  }
  #endif

  private void DrawUI() => WindowSystem.Draw();
  private void ToggleConfigUi() => ConfigWindow.Toggle();
}

