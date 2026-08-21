using StArray.ModManager.Hooks;
using StArray.ModManager.Manager;

namespace ShowBPM.Mobile;

public static partial class GameHooks
{
    private const string LogTag = "ShowBPM";

    private static ShowBpmPlugin? _plugin;
    private static int _installed;
    private static int _failed;

    internal static bool Install(ShowBpmPlugin plugin, GameApi game)
    {
        try
        {
            Uninstall();
            _plugin = plugin;

            TryInstall("scrController.Awake", Install_ControllerAwake);
            TryInstall("scnGame.Play", Install_GamePlay);
            TryInstall("scrPressToStart.ShowText", Install_ShowText);
            // 3.3.1's CR2024 level-select entry/exit path calls this method with
            // two managed Action callbacks. It is only a HUD cleanup hook, so
            // leave the original method untouched for the C# manager's IL2CPP
            // chain; ControllerAwake and StartLoadingScene still reset HUD state.
            TryInstall("scnEditor.ResetScene", Install_EditorReset);
            TryInstall("scrController.StartLoadingScene", Install_StartLoadingScene);
            TryInstall("RDString.ChangeLanguage", Install_ChangeLanguage);

            // scrPlayer.Update 只在原始玩家更新完成后读取当前砖块，承担游戏世界内的
            // BPM 轮询；它不参与球的落砖调用链。scrFloor.LateUpdate 只负责倍率文字。
            bool playerUpdate = TryInstall("scrPlayer.Update", Install_PlayerUpdate);

            // scrFloor.LateUpdate 负责倍率文字刷新。
            // 不 Hook scrPlanet.MoveToNextFloor：关卡选择也复用该方法，且 YoonKeyViewer
            // 会对同一目标建立另一层 C# Hook；3.3.1 的球移动必须保持原始调用链。
            bool lateUpdate = TryInstall("scrFloor.LateUpdate", Install_FloorLateUpdate);
            bool drawFloorNums = TryInstall("scnEditor.DrawFloorNums", Install_DrawFloorNums);
            SpeedTextTickAvailable = lateUpdate;
            Logger.Info(
                LogTag,
                $"Gameplay tick: scrPlayer.Update={playerUpdate}; speed text hooks: LateUpdate={lateUpdate}, "
                    + $"DrawFloorNums={drawFloorNums}; native floor visibility path preserved");

            Logger.Info(LogTag, $"Installed {_installed} IL2CPP hooks ({_failed} unavailable)");
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"Hook installation failed: {exception}");
            Uninstall();
            return false;
        }
    }

    /// <summary>scrFloor.LateUpdate 是否可用；它是 Mod 唯一稳定的游戏主线程回调。</summary>
    internal static bool SpeedTextTickAvailable { get; private set; }

    private static bool TryInstall(string name, Func<bool> install)
    {
        bool ok;
        try
        {
            ok = install();
        }
        catch (Exception exception)
        {
            Logger.Warn(LogTag, $"Hook {name} threw during installation: {exception.Message}");
            ok = false;
        }

        if (ok)
            _installed++;
        else
        {
            _failed++;
            Logger.Warn(LogTag, $"Hook {name} could not be installed");
        }
        return ok;
    }

    internal static void Uninstall()
    {
        UninstallHooks();
        _plugin = null;
        _installed = 0;
        _failed = 0;
        SpeedTextTickAvailable = false;
    }

    private static void Notify(Action<ShowBpmPlugin> action, string source)
    {
        ShowBpmPlugin? plugin = _plugin;
        if (plugin == null)
            return;
        try
        {
            action(plugin);
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"{source}: {exception}");
        }
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scrController", "Awake", ParameterCount = 0)]
    private static void ControllerAwake(nint instance, nint methodInfo)
    {
        ControllerAwakeOriginal(instance, methodInfo);
        Notify(plugin => plugin.HandleControllerAwake(instance), nameof(ControllerAwake));
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scnGame", "Play", ParameterCount = 2)]
    private static byte GamePlay(nint instance, int sequenceId, byte isRestart, nint methodInfo)
    {
        byte result = GamePlayOriginal(instance, sequenceId, isRestart, methodInfo);
        Notify(plugin => plugin.HandleLevelStart(), nameof(GamePlay));
        return result;
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scrPressToStart", "ShowText", ParameterCount = 0)]
    private static void ShowText(nint instance, nint methodInfo)
    {
        ShowTextOriginal(instance, methodInfo);
        Notify(plugin => plugin.HandleLevelStart(), nameof(ShowText));
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scnEditor", "ResetScene", ParameterCount = 1)]
    private static void EditorReset(nint instance, byte clsToEditor, nint methodInfo)
    {
        EditorResetOriginal(instance, clsToEditor, methodInfo);
        Notify(plugin => plugin.HideHud(), nameof(EditorReset));
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scrController", "StartLoadingScene", ParameterCount = 1)]
    private static void StartLoadingScene(nint instance, int direction, nint methodInfo)
    {
        StartLoadingSceneOriginal(instance, direction, methodInfo);
        Notify(plugin => plugin.HideHud(), nameof(StartLoadingScene));
    }

    [UnmanagedHook("Assembly-CSharp.dll", "RDString", "ChangeLanguage", ParameterCount = 1)]
    private static void ChangeLanguage(int language, nint methodInfo)
    {
        ChangeLanguageOriginal(language, methodInfo);
        Notify(plugin => plugin.HandleLanguageChanged(language), nameof(ChangeLanguage));
    }

    [UnmanagedHook("Assembly-CSharp.dll", "scrPlayer", "Update", ParameterCount = 0)]
    private static void PlayerUpdate(nint instance, nint methodInfo)
    {
        PlayerUpdateOriginal(instance, methodInfo);
        Notify(plugin => plugin.HandleGameplayPlayerTick(instance), nameof(PlayerUpdate));
    }

    /// <summary>
    /// 对应 PC 版返回 false 的 Prefix：功能开启时不调用 trampoline，改由 Mod 自行绘制砖号，
    /// 以免游戏把叠加的倍率文字覆盖回序号。
    /// </summary>
    [UnmanagedHook("Assembly-CSharp.dll", "scnEditor", "DrawFloorNums", ParameterCount = 0)]
    private static void DrawFloorNums(nint instance, nint methodInfo)
    {
        ShowBpmPlugin? plugin = _plugin;
        if (plugin == null || !plugin.IsSpeedTextActive)
        {
            DrawFloorNumsOriginal(instance, methodInfo);
            return;
        }

        try
        {
            plugin.HandleDrawFloorNums(instance);
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"{nameof(DrawFloorNums)}: {exception}");
        }
    }

    /// <summary>对应 PC 版的 Postfix：让倍率文字随砖块淡入淡出。每帧每砖调用，属于热路径。</summary>
    [UnmanagedHook("Assembly-CSharp.dll", "scrFloor", "LateUpdate", ParameterCount = 0)]
    private static void FloorLateUpdate(nint instance, nint methodInfo)
    {
        FloorLateUpdateOriginal(instance, methodInfo);

        ShowBpmPlugin? plugin = _plugin;
        if (plugin == null)
            return;

        try
        {
            // 借这个每帧回调兑现「开关变化」，因为 Mod 的设置界面在渲染线程，
            // 而改动游戏对象必须回到游戏主线程。
            plugin.TickSpeedTextMainThread();
            if (plugin.IsSpeedTextActive)
                plugin.HandleFloorLateUpdate(instance);
        }
        catch
        {
            // 热路径：不记日志，避免一帧刷屏。
        }
    }

}
