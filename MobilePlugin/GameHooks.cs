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

            // 逐个安装：生成的 InstallHooks() 是「全有或全无」，任何一个装不上都会让整个 Mod
            // 加载失败。这里只把 MoveToNextFloor 当作必需项，其余失败仅降级并记录是哪一个。
            bool core = TryInstall("scrPlanet.MoveToNextFloor", Install_MoveToNextFloor);
            if (!core)
            {
                Logger.Error(LogTag, "Required hook scrPlanet.MoveToNextFloor could not be installed");
                Uninstall();
                return false;
            }

            TryInstall("scrController.Awake", Install_ControllerAwake);
            TryInstall("scnGame.Play", Install_GamePlay);
            TryInstall("scrPressToStart.ShowText", Install_ShowText);
            TryInstall("scrUIController.WipeToBlack", Install_WipeToBlack);
            TryInstall("scnEditor.ResetScene", Install_EditorReset);
            TryInstall("scrController.StartLoadingScene", Install_StartLoadingScene);
            TryInstall("RDString.ChangeLanguage", Install_ChangeLanguage);

            // 速度倍率文字所需的三个 Hook；缺任何一个都只是该功能降级。
            bool lateUpdate = TryInstall("scrFloor.LateUpdate", Install_FloorLateUpdate);
            bool becameVisible = TryInstall("scrFloor.OnBecameVisible", Install_OnBecameVisible);
            bool drawFloorNums = TryInstall("scnEditor.DrawFloorNums", Install_DrawFloorNums);
            SpeedTextTickAvailable = lateUpdate;
            Logger.Info(
                LogTag,
                $"Speed text hooks: LateUpdate={lateUpdate}, "
                    + $"OnBecameVisible={becameVisible}, DrawFloorNums={drawFloorNums}");

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

    [UnmanagedHook("Assembly-CSharp.dll", "scrPlanet", "MoveToNextFloor", ParameterCount = 3)]
    private static void MoveToNextFloor(nint instance, nint floor, float exitAngle, int hitMargin, nint methodInfo)
    {
        MoveToNextFloorOriginal(instance, floor, exitAngle, hitMargin, methodInfo);
        Notify(plugin => plugin.HandleMoveToNextFloor(floor), nameof(MoveToNextFloor));
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

    [UnmanagedHook("Assembly-CSharp.dll", "scrUIController", "WipeToBlack", ParameterCount = 3)]
    private static void WipeToBlack(nint instance, int direction, nint onComplete, nint onCancel, nint methodInfo)
    {
        WipeToBlackOriginal(instance, direction, onComplete, onCancel, methodInfo);
        Notify(plugin => plugin.HideHud(), nameof(WipeToBlack));
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

    /// <summary>对应 PC 版返回 false 的 Prefix：功能开启时接管砖块可见时的显示逻辑。</summary>
    [UnmanagedHook("Assembly-CSharp.dll", "scrFloor", "OnBecameVisible", ParameterCount = 0)]
    private static void OnBecameVisible(nint instance, nint methodInfo)
    {
        ShowBpmPlugin? plugin = _plugin;
        if (plugin == null || !plugin.IsSpeedTextActive)
        {
            OnBecameVisibleOriginal(instance, methodInfo);
            return;
        }

        try
        {
            plugin.HandleFloorBecameVisible(instance);
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"{nameof(OnBecameVisible)}: {exception}");
        }
    }
}
