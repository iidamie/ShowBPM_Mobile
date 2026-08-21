using System.Globalization;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using StArray.ModManager.Inspector;
using StArray.ModManager.Manager;
using StArray.ModManager.Runtime;
using StArray.ModManager.RuntimeAbstractions;

namespace ShowBPM.Mobile;

public enum HudAlignment
{
    Left,
    Center,
    Right,
}

/// <summary>速度倍率文字的计算基准。</summary>
public enum SpeedTextMode
{
    Tile,
    Real,
}

public sealed class ShowBpmPlugin : IModPlugin, IModSettings
{
    private const string LogTag = "ShowBPM";
    private const double TwoPi = 6.2831854820251465d;
    private const double Pi = 3.1415927410125732d;

    /// <summary>编辑器砖号的默认颜色，用于把被叠加过的文字还原回去。</summary>
    private static readonly NativeColor BasicColor = new(1f, 0f, 1f, 1f);

    private readonly object _stateLock = new();

    private GameApi? _game;
    private GitHubUpdateService? _updateService;
    private nint _controller;
    private bool _hudVisible;
    private bool _beforeMultipress;
    private double _beforeBpm;
    private double _baseBpm;
    private double _pitch = 1d;
    private double _playbackSpeed = 1d;
    private double _tileBpm;
    private double _realBpm;
    private int _kps;
    private int _languageCode = 10;
    private bool _renderErrorLogged;
    private bool _speedTextSupported;
    private bool _speedTextApplied;
    private bool _lastShowSpeedText;
    private SpeedTextMode _lastSpeedTextBasis = SpeedTextMode.Tile;
    private bool _speedTextDiagnosticLogged;
    private bool _speedTextRefreshPending;
    private bool _speedTextRetryAttempted;
    private bool _speedTextDrawDiagnosticLogged;
    private bool _speedTextVisibilityDiagnosticLogged;
    private volatile bool _speedTextLevelReady;
    private nint _speedTextFloorList;
    private nint _lastGameplayFloor;

    [ModSettingLabel("Show tile BPM")]
    public bool ShowTileBpm = true;
    [ModSettingLabel("Tile BPM template")]
    public string TileBpmText = "타일 BPM - {value}";
    [ModSettingLabel("Show real BPM")]
    public bool ShowRealBpm = true;
    [ModSettingLabel("Real BPM template")]
    public string RealBpmText = "체감 BPM - {value}";
    [ModSettingLabel("Show KPS")]
    public bool ShowKps = true;
    [ModSettingLabel("KPS template")]
    public string KpsText = "초당 클릭 수 - {value}";
    [ModSettingLabel("Show speed multiplier on tiles")]
    public bool ShowSpeedText;
    [ModSettingLabel("Speed multiplier basis")]
    public SpeedTextMode SpeedTextBasis = SpeedTextMode.Tile;
    [ModSettingLabel("Use shadow")]
    public bool UseShadow = true;
    [ModSettingLabel("Use bold")]
    public bool UseBold;
    [ModSettingLabel("Zero padding")]
    public bool ZeroPadding = true;
    [ModSettingLabel("Ignore multipress")]
    public bool IgnoreMultipress;
    [ModSettingLabel("Decimal places")]
    [ModSettingRange(0, 6)]
    public int DecimalPlaces;
    [ModSettingLabel("Position X")]
    [ModSettingRange(-0.1f, 1.1f)]
    public float PositionX = 0.96f;
    [ModSettingLabel("Position Y")]
    [ModSettingRange(-0.1f, 1.1f)]
    public float PositionY = 0.98f;
    [ModSettingLabel("Font size")]
    [ModSettingRange(1, 100)]
    public int FontSize = 35;
    [ModSettingLabel("Alignment")]
    public HudAlignment Alignment = HudAlignment.Right;

    public string Id => "ShowBPM";
    public string Name => "ShowBPM";
    public string Version => ModVersion;
    public string Author => "Flower";
    public string Description => "ADOFAI mobile BPM, real BPM, KPS and speed multiplier overlay";
    public IReadOnlyList<string> Dependencies => Array.Empty<string>();

    private static string ModVersion => ResolveModVersion();

    private static string ResolveModVersion()
    {
        string? informational = typeof(ShowBpmPlugin).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return typeof(ShowBpmPlugin).Assembly.GetName().Version?.ToString() ?? "1.2.0";

        int metadataSeparator = informational.IndexOf('+');
        return metadataSeparator < 0 ? informational : informational[..metadataSeparator];
    }

    /// <summary>速度倍率文字当前是否生效。Hook 热路径上的第一道短路判断，必须保持廉价。</summary>
    internal bool IsSpeedTextActive => ShowSpeedText && _speedTextSupported;


    public void OnLoad()
    {
        HideHud();
        _game = GameApi.Create();
        if (_game == null)
            throw new InvalidOperationException("ADOFAI IL2CPP runtime or Assembly-CSharp was not found");

        _languageCode = _game.GetLanguageCode();
        _controller = _game.GetController();
        _speedTextSupported = _game.SupportsSpeedText;
        _lastShowSpeedText = ShowSpeedText;
        _lastSpeedTextBasis = SpeedTextBasis;
        Logger.Info(
            LogTag,
            $"Runtime: backend={RuntimeManager.Backend}, controller={Pointer(_controller)}, "
                + _game.DescribeSpeedTextBindings());
        Logger.Info(
            LogTag,
            $"Speed text settings: enabled={ShowSpeedText}, basis={SpeedTextBasis}");
        Logger.Info(
            LogTag,
            $"Settings persistence members={ModInspector.GetSettingMembers(GetType()).Count}");
        if (!_speedTextSupported)
            Logger.Warn(LogTag, "Speed text runtime handles unavailable; the feature stays disabled");
        try
        {
            if (!GameHooks.Install(this, _game))
                throw new InvalidOperationException("ShowBPM game hooks could not be installed");
            TryStartFromCurrentLevel();
        }
        catch
        {
            GameHooks.Uninstall();
            _game = null;
            throw;
        }

        string modDirectory = Path.GetDirectoryName(typeof(ShowBpmPlugin).Assembly.Location)
            ?? AppContext.BaseDirectory;
        _updateService = new GitHubUpdateService(modDirectory, Version);
        _updateService.StartAutomaticCheck();
        Logger.Info(LogTag, "Loaded");
    }

    public void OnUnload()
    {
        _updateService?.Dispose();
        _updateService = null;
        ClearSpeedText();
        GameHooks.Uninstall();
        HideHud();
        _game = null;
        _controller = 0;
        _speedTextSupported = false;
        _speedTextApplied = false;
        _speedTextRefreshPending = false;
        _speedTextRetryAttempted = false;
        _speedTextDrawDiagnosticLogged = false;
        _speedTextVisibilityDiagnosticLogged = false;
        _speedTextLevelReady = false;
        _lastGameplayFloor = 0;
        Logger.Info(LogTag, "Unloaded");
    }

    public void OnForegroundGUI(ImDrawListPtr drawList)
    {
        try
        {
            _updateService?.DrawForegroundNotification();
            DrawHud(drawList);
        }
        catch (Exception exception)
        {
            if (_renderErrorLogged)
                return;
            _renderErrorLogged = true;
            Logger.Error(LogTag, $"HUD render failed: {exception}");
        }
    }

    public void OnGui()
    {
        UiText ui = UiText.FromSystemLanguage(_languageCode);

        ImGui.Checkbox(ui.TileToggle, ref ShowTileBpm);
        if (ShowTileBpm)
            ImGui.InputText($"{ui.TileTemplate}##tile-template", ref TileBpmText, 256);

        ImGui.Checkbox(ui.RealToggle, ref ShowRealBpm);
        if (ShowRealBpm)
            ImGui.InputText($"{ui.RealTemplate}##real-template", ref RealBpmText, 256);

        ImGui.Checkbox(ui.KpsToggle, ref ShowKps);
        if (ShowKps)
            ImGui.InputText($"{ui.KpsTemplate}##kps-template", ref KpsText, 256);

        if (_speedTextSupported)
        {
            ImGui.Checkbox(ui.SpeedTextToggle, ref ShowSpeedText);
            if (ShowSpeedText)
            {
                int basis = (int)SpeedTextBasis;
                string[] bases = { ui.SpeedTextOnTile, ui.SpeedTextOnReal };
                if (ImGui.Combo(ui.SpeedTextBasis, ref basis, bases, bases.Length))
                    SpeedTextBasis = (SpeedTextMode)basis;
            }
        }

        ImGui.Separator();
        ImGui.TextUnformatted(ui.Appearance);
        ImGui.Checkbox(ui.Shadow, ref UseShadow);
        ImGui.Checkbox(ui.Bold, ref UseBold);
        ImGui.Checkbox(ui.ZeroPadding, ref ZeroPadding);
        ImGui.Checkbox(ui.IgnoreMultipress, ref IgnoreMultipress);
        ImGui.SliderInt(ui.DecimalPlaces, ref DecimalPlaces, 0, 6);
        ImGui.SliderFloat(ui.PositionX, ref PositionX, -0.1f, 1.1f, "%.2f");
        ImGui.SliderFloat(ui.PositionY, ref PositionY, -0.1f, 1.1f, "%.2f");
        ImGui.SliderInt(ui.FontSize, ref FontSize, 1, 100);

        int alignment = (int)Alignment;
        string[] alignments = { ui.Left, ui.Center, ui.Right };
        if (ImGui.Combo(ui.Alignment, ref alignment, alignments, alignments.Length))
            Alignment = (HudAlignment)alignment;

        NormalizeSettings();
        _updateService?.DrawGui();
    }

    internal void HandleControllerAwake(nint controller)
    {
        // The old scene owns these child objects and will destroy them during the
        // transition. Do not call UnityEngine.Object.Destroy from Awake while the
        // replacement scene is still constructing its object graph.
        _game?.ForgetCreatedSpeedTexts();
        _controller = controller;
        _languageCode = _game?.GetLanguageCode() ?? 10;
        lock (_stateLock)
        {
            _beforeMultipress = false;
            _beforeBpm = 0d;
            _baseBpm = 0d;
            _tileBpm = 0d;
            _realBpm = 0d;
            _kps = 0;
            _hudVisible = false;
        }
        _speedTextFloorList = 0;
        _speedTextApplied = false;
        _speedTextRefreshPending = false;
        _speedTextRetryAttempted = false;
        _speedTextLevelReady = false;
        _lastGameplayFloor = 0;
    }

    internal void HandleLanguageChanged(int language)
    {
        _languageCode = language;
    }

    internal void HandleLevelStart(nint floor = 0)
    {
        GameApi? game = _game;
        if (game == null)
            return;

        Logger.Debug(LogTag, $"Level start probe: floorArg={Pointer(floor)}, controllerBefore={Pointer(_controller)}");
        nint controller = game.GetController(floor);
        if (controller != 0)
            _controller = controller;
        controller = _controller;
        if (!game.IsGameWorld(controller))
        {
            Logger.Debug(LogTag, $"Level start skipped: controller={Pointer(controller)} is not a game world");
            return;
        }

        if (floor == 0)
            floor = game.GetCurrentFloor(controller);
        nint conductor = game.GetConductor(floor);
        if (conductor == 0)
        {
            Logger.Warn(LogTag, $"Level start skipped: conductor is null for floor={Pointer(floor)}");
            return;
        }

        nint floorList = game.GetLevelFloors();
        if (floorList != 0 && floorList != _speedTextFloorList)
        {
            // A second level can reuse the same controller. The old labels are
            // already scene-owned; only discard their managed pointers here.
            game.ForgetCreatedSpeedTexts();
            _speedTextFloorList = floorList;
        }

        // 优先用 scnGame 的 LevelData.pitch × 速度试炼倍速，取不到时回退到 conductor.song.pitch。
        // 前者才会随速度试炼变化，只读后者会导致开启速度试炼后 BPM 偏低。
        double pitch = game.GetScenePitch();
        if (pitch <= 0d)
            pitch = game.GetSongPitch(conductor);
        double baseBpm = game.GetConductorBpm(conductor) * pitch;
        if (baseBpm <= 0d || double.IsNaN(baseBpm) || double.IsInfinity(baseBpm))
        {
            Logger.Warn(
                LogTag,
                $"Level start skipped: invalid BPM, conductor={Pointer(conductor)}, "
                    + $"rawBpm={game.GetConductorBpm(conductor):0.###}, pitch={pitch:0.###}");
            return;
        }

        double speed = GetTileSpeed(game, controller, floor);
        double current = baseBpm * speed;

        lock (_stateLock)
        {
            _pitch = pitch;
            _playbackSpeed = game.GetPlaybackSpeed();
            _baseBpm = baseBpm;
            _beforeMultipress = false;
            _beforeBpm = current;
            _tileBpm = current;
            _realBpm = current;
            _kps = (int)Math.Round(current / 60d);
            _hudVisible = true;
        }
        _speedTextLevelReady = true;

        _speedTextDiagnosticLogged = false;
        _speedTextRefreshPending = IsSpeedTextActive;
        _speedTextRetryAttempted = false;
        _speedTextDrawDiagnosticLogged = false;
        _speedTextVisibilityDiagnosticLogged = false;
        Logger.Info(
            LogTag,
            $"Level start: controller={Pointer(controller)}, floor={Pointer(floor)}, "
                + $"conductor={Pointer(conductor)}, baseBpm={baseBpm:0.###}, pitch={pitch:0.###}, "
                + $"speed={speed:0.###}, speedTextActive={IsSpeedTextActive}, basis={SpeedTextBasis}");
        ApplySpeedText(game, baseBpm);
    }

    internal void HandleCurrentFloorChanged(nint floor)
    {
        GameApi? game = _game;
        if (game == null || floor == 0)
            return;

        nint controller = game.GetController(floor);
        if (controller != 0)
            _controller = controller;
        controller = _controller;
        if (!game.IsGameWorld(controller))
            return;

        nint nextFloor = game.GetNextFloor(floor);
        if (nextFloor == 0)
            return;

        if (_baseBpm <= 0d)
            HandleLevelStart(floor);

        lock (_stateLock)
        {
            double controllerSpeed = game.GetSpeed(controller);
            // floor is scrPlayer.currfloor: the brick whose BPM is active now.
            // Use its own nextfloor for the interval, matching the other mobile
            // HUD implementations instead of using the previously visited tile.
            nint tileFloor = floor;
            double tileSpeed = GetTileSpeed(game, controller, floor);
            double currentBpm = GetRealBpm(game, floor, controllerSpeed) * _playbackSpeed * _pitch;
            double nextBpm = GetRealBpm(game, nextFloor, controllerSpeed) * _playbackSpeed * _pitch;
            bool isMultipress = false;

            if (IgnoreMultipress)
            {
                double angleMoved = GetAngleMoved(
                    game.GetEntryAngle(floor),
                    game.GetExitAngle(floor),
                    !game.IsCounterClockwise(floor));
                double conductorBpm = game.GetConductorBpm(game.GetConductor(floor)) * controllerSpeed;
                double time = AngleToTime(angleMoved, conductorBpm);
                bool appliesDamage = angleMoved > 1.56905098538846d
                    && time > game.GetAverageFrameTime(controller) * 2.5d
                    && time > 0.0299999993294477d;
                isMultipress = !appliesDamage && !NearlyEqual(nextBpm, currentBpm);
            }

            if (isMultipress || _beforeMultipress)
                currentBpm = _beforeBpm;

            double previousTileBpm = _tileBpm;
            _tileBpm = _baseBpm * tileSpeed;
            _realBpm = currentBpm;
            _kps = (int)Math.Round(currentBpm / 60d);
            _beforeMultipress = isMultipress;
            _beforeBpm = currentBpm;
            _hudVisible = true;

            if (!NearlyEqual(previousTileBpm, _tileBpm))
            {
                Logger.Info(
                    LogTag,
                    $"Tile BPM updated: floor={Pointer(tileFloor)}, speed={tileSpeed:0.###}, "
                        + $"tileBpm={_tileBpm:0.###}, controllerSpeed={controllerSpeed:0.###}");
            }
        }
    }

    /// <summary>
    /// 通过 scrPlayer.Update 观察实际游戏世界中的当前砖块变化。
    ///
    /// 3.3.1 的关卡选择同样使用 scrPlanet.MoveToNextFloor；ShowBPM 不再
    /// Hook 那个共享入口，避免和其它 Mod 的链式 Hook 改变球的移动逻辑。
    /// </summary>
    internal void HandleGameplayPlayerTick(nint player)
    {
        GameApi? game = _game;
        if (game == null)
            return;

        nint controller = _controller;
        if (controller == 0 || !game.IsGameWorld(controller))
        {
            controller = game.GetController();
            if (controller != 0)
                _controller = controller;
        }

        if (controller == 0 || !game.IsGameWorld(controller))
        {
            _lastGameplayFloor = 0;
            return;
        }

        // scrController.currFloor is synchronized one update later on 3.3.1.
        // scrPlayer.currfloor is the same source used by the other mobile HUD
        // ports and already points at the tile whose BPM is now active.
        nint currentFloor = game.GetPlayerCurrentFloor(player);
        if (currentFloor == 0)
            currentFloor = game.GetCurrentFloor(controller);
        if (currentFloor == 0)
            return;

        // A level-select floor can be backed by a controller whose stale state
        // still looks like a game world. A live conductor is the stronger
        // signal that this is an actual playable level.
        if (game.GetConductor(currentFloor) == 0)
            return;

        if (currentFloor == _lastGameplayFloor)
            return;

        bool hadPreviousFloor = _lastGameplayFloor != 0;
        _lastGameplayFloor = currentFloor;

        if (_baseBpm <= 0d || !hadPreviousFloor)
            HandleLevelStart(currentFloor);
        else
            HandleCurrentFloorChanged(currentFloor);
    }

    internal void HideHud()
    {
        lock (_stateLock)
            _hudVisible = false;
        _speedTextLevelReady = false;
    }

    // ── 速度倍率文字 ──

    /// <summary>
    /// 由 scrFloor.LateUpdate 每帧调用，在游戏主线程上兑现设置界面（渲染线程）的开关改动。
    /// 设置没变时只是两次字段比较，开销可忽略。
    /// </summary>
    internal void TickSpeedTextMainThread()
    {
        if (!_speedTextSupported)
            return;

        bool show = ShowSpeedText;
        SpeedTextMode basis = SpeedTextBasis;
        if (show != _lastShowSpeedText || basis != _lastSpeedTextBasis)
        {
            _lastShowSpeedText = show;
            _lastSpeedTextBasis = basis;

            if (show)
            {
                _speedTextRefreshPending = true;
                _speedTextRetryAttempted = false;
                Logger.Info(LogTag, $"Speed text settings changed: enabled={show}, basis={basis}");
                RefreshSpeedText();
            }
            else
            {
                Logger.Info(LogTag, "Speed text settings changed: enabled=false");
                ClearSpeedText();
            }

            return;
        }

        if (!show || !_speedTextRefreshPending || _speedTextRetryAttempted)
            return;

        _speedTextRetryAttempted = true;
        Logger.Info(LogTag, "Speed text refresh retry on scrFloor.LateUpdate");
        RefreshSpeedText();
    }

    /// <summary>按当前设置重新铺一遍倍率文字。基准切换或中途开启时调用。</summary>
    private void RefreshSpeedText()
    {
        GameApi? game = _game;
        if (game == null)
            return;

        double baseBpm;
        lock (_stateLock)
            baseBpm = _baseBpm;
        if (baseBpm <= 0d)
            return;

        try
        {
            ApplySpeedText(game, baseBpm);
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"Speed text refresh failed: {exception}");
        }
    }

    /// <summary>
    /// 关卡开始时给每块变速砖的下一块叠加 "x倍率" 文字，加速偏红、减速偏蓝。
    /// 对应 PC 1.2.0 中 Patch.LevelStart 里的 showSpeedText 分支。
    /// </summary>
    private enum SpeedTextApplyStatus
    {
        Applied,
        NoPreviousFloor,
        NoSpeedChange,
        InvalidRatio,
        MissingText,
        WriteFailed,
    }

    private void ApplySpeedText(GameApi game, double baseBpm)
    {
        if (!IsSpeedTextActive)
            return;

        nint floors = game.GetLevelFloors();
        if (floors == 0)
        {
            LogSpeedTextDiagnostic("scrLevelMaker.listFloors was not available");
            return;
        }

        int listCount = game.GetCollectionCount(floors);

        bool applied = false;
        int total = 0;
        int itemReadFailures = 0;
        int missingLabel = 0;
        int speedChanges = 0;
        int appliedLabels = 0;
        int missingPrevious = 0;
        int missingText = 0;
        int writeFailed = 0;
        int invalidRatio = 0;
        nint sampleFloor = 0;
        nint sampleNextFloor = 0;
        double sampleFloorSpeed = 0d;
        double sampleNextSpeed = 0d;

        for (int index = 0; index < listCount; index++)
        {
            nint floor = GameApi.GetCollectionItem(floors, index);
            if (floor == 0)
            {
                itemReadFailures++;
                continue;
            }
            total++;

            nint nextFloor = game.GetNextFloor(floor);
            if (nextFloor == 0)
                continue;

            double floorSpeed = game.GetFloorSpeed(floor);
            double nextSpeed = game.GetFloorSpeed(nextFloor);
            if (SpeedEqual(floorSpeed, nextSpeed))
                continue;
            speedChanges++;
            if (sampleFloor == 0)
            {
                sampleFloor = floor;
                sampleNextFloor = nextFloor;
                sampleFloorSpeed = floorSpeed;
                sampleNextSpeed = nextSpeed;
            }

            if (!TryApplySpeedText(game, nextFloor, baseBpm, out SpeedTextApplyStatus status))
            {
                missingLabel++;
                switch (status)
                {
                    case SpeedTextApplyStatus.NoPreviousFloor:
                        missingPrevious++;
                        break;
                    case SpeedTextApplyStatus.MissingText:
                        missingText++;
                        break;
                    case SpeedTextApplyStatus.WriteFailed:
                        writeFailed++;
                        break;
                    case SpeedTextApplyStatus.InvalidRatio:
                        invalidRatio++;
                        break;
                }
            }
            else
            {
                applied = true;
                appliedLabels++;
                if (game.IsFloorVisible(floor))
                    game.SetActive(game.GetFloorNumObject(nextFloor), true);
            }
        }

        if (applied)
            _speedTextApplied = true;
        _speedTextRefreshPending = itemReadFailures > 0
            || (speedChanges > 0 && missingLabel > 0);

        string sample = sampleFloor == 0
            ? "none"
            : $"{Pointer(sampleFloor)}->{Pointer(sampleNextFloor)} "
                + $"speed={sampleFloorSpeed:0.###}->{sampleNextSpeed:0.###}";
        Logger.Info(
            LogTag,
            $"Speed text scan: list={Pointer(floors)}, listCount={listCount}, indexed={listCount}, "
                + $"iterated={total}, itemReadFailures={itemReadFailures}, "
                + $"changes={speedChanges}, applied={appliedLabels}, missing={missingLabel}, "
                + $"missingPrevious={missingPrevious}, missingText={missingText}, "
                + $"writeFailed={writeFailed}, invalidRatio={invalidRatio}, "
                + $"pending={_speedTextRefreshPending}, sample={sample}");

        if (!applied && speedChanges > 0)
            LogSpeedTextDiagnostic(
                $"no speed label drawn: floors={total}, missing editorNumText={missingLabel}");
    }

    /// <summary>倍率文字没画出来时只报告一次，避免每关刷屏。</summary>
    private void LogSpeedTextDiagnostic(string message)
    {
        if (_speedTextDiagnosticLogged)
            return;
        _speedTextDiagnosticLogged = true;
        Logger.Warn(LogTag, $"Speed text: {message}");
    }

    /// <summary>
    /// PC 1.2.0 的 GetRealBpm：首块砖与无后继砖各有特例，其余按两块砖的进入时间差换算。
    /// 与 HUD 用的 <see cref="GetRealBpm"/> 不同，这里不依赖 _baseBpm 字段。
    /// </summary>
    private static double SpeedTextRealBpm(GameApi game, nint floor, double baseBpm)
    {
        if (floor == 0)
            return baseBpm;
        if (game.GetFloorSeqId(floor) == 0)
            return baseBpm;

        nint nextFloor = game.GetNextFloor(floor);
        if (nextFloor == 0)
            return game.GetFloorSpeed(floor) * baseBpm;

        double delta = game.GetEntryTime(nextFloor) - game.GetEntryTime(floor);
        if (delta <= 0.0000001d)
            return game.GetFloorSpeed(floor) * baseBpm;
        return 60d / delta;
    }

    /// <summary>给目标砖补写倍率文字。目标砖的上一块砖决定是否发生变速。</summary>
    private bool TryApplySpeedText(
        GameApi game,
        nint floor,
        double baseBpm,
        out SpeedTextApplyStatus status)
    {
        status = SpeedTextApplyStatus.NoPreviousFloor;
        nint previousFloor = game.GetPrevFloor(floor);
        if (previousFloor == 0)
            return false;

        double previousSpeed = game.GetFloorSpeed(previousFloor);
        double currentSpeed = game.GetFloorSpeed(floor);
        if (SpeedEqual(previousSpeed, currentSpeed))
        {
            status = SpeedTextApplyStatus.NoSpeedChange;
            return false;
        }

        double ratio;
        if (SpeedTextBasis == SpeedTextMode.Real)
        {
            double previousBpm = SpeedTextRealBpm(game, previousFloor, baseBpm);
            double divisor = Math.Abs(previousBpm);
            if (divisor <= 0.0000001d)
            {
                status = SpeedTextApplyStatus.InvalidRatio;
                return false;
            }
            ratio = SpeedTextRealBpm(game, floor, baseBpm) / divisor;
        }
        else
        {
            if (Math.Abs(previousSpeed) <= 0.0000001d)
            {
                status = SpeedTextApplyStatus.InvalidRatio;
                return false;
            }
            ratio = currentSpeed / previousSpeed;
        }

        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            status = SpeedTextApplyStatus.InvalidRatio;
            return false;
        }

        nint text = game.GetFloorNumText(floor);
        if (text == 0 && !game.TryCreateSpeedText(floor, out text))
        {
            status = SpeedTextApplyStatus.MissingText;
            return false;
        }

        // The cloned RDConstants.prefabLetterPress keeps the game's font, size,
        // alignment and shadow settings. Only the content and PC ratio color are
        // changed here, matching the PC speed-text path.
        string value = FormatRatio(ratio);
        if (game.GetTextValue(text) != value && !game.SetTextValue(text, value))
        {
            status = SpeedTextApplyStatus.WriteFailed;
            return false;
        }
        game.SetTextColor(text, RatioColor(ratio));
        status = SpeedTextApplyStatus.Applied;
        return true;
    }

    private static bool IsSpeedChange(GameApi game, nint floor)
    {
        nint previousFloor = game.GetPrevFloor(floor);
        return previousFloor != 0
            && !SpeedEqual(game.GetFloorSpeed(previousFloor), game.GetFloorSpeed(floor));
    }

    private static string FormatRatio(double ratio)
    {
        return "x" + ratio.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>倍率越高越红，越低越蓝；沿用 PC 1.2.0 的取值曲线。</summary>
    private static NativeColor RatioColor(double ratio)
    {
        float strong = (float)Math.Max(240d - ratio * 7d, 110d) / 255f;
        float weak = (float)Math.Max(96d - ratio * 5d, 0d) / 255f;
        return ratio > 1d
            ? new NativeColor(strong, weak, weak, 1f)
            : new NativeColor(weak, weak, strong, 1f);
    }

    /// <summary>
    /// 每帧每块可见砖调用一次的热路径：让倍率文字随砖块淡入淡出，并在游戏覆盖文本后补写。
    /// 正常帧只做指针读取和一次颜色写入，不分配、不加锁。
    /// </summary>
    internal void HandleFloorLateUpdate(nint floor)
    {
        GameApi? game = _game;
        if (game == null || floor == 0 || !_speedTextLevelReady)
            return;

        // 自由漫游生成的砖块在非游戏世界下不处理，与 PC 版一致。
        if (!game.IsGameWorld(_controller))
        {
            nint currentFloor = game.GetCurrentFloor(_controller);
            if (currentFloor != 0 && !game.IsFreeroamGenerated(currentFloor))
                return;
        }

        nint text = game.GetFloorNumText(floor);
        if (text == 0)
        {
            double baseBpm;
            lock (_stateLock)
                baseBpm = _baseBpm;
            if (baseBpm > 0d)
                TryApplySpeedText(game, floor, baseBpm, out _);
            text = game.GetFloorNumText(floor);
        }
        if (text == 0)
            return;

        char first = game.GetTextFirstChar(text);
        if (first != 'x')
        {
            double baseBpm;
            lock (_stateLock)
                baseBpm = _baseBpm;
            if (baseBpm > 0d)
                TryApplySpeedText(game, floor, baseBpm, out _);
            text = game.GetFloorNumText(floor);
            first = game.GetTextFirstChar(text);
        }
        if (first != 'x')
            return;

        if (game.IsFloorVisible(floor))
            game.SetActive(game.GetFloorNumObject(floor), true);
        if (game.IsFloorFading(floor))
            return;

        NativeColor color = game.GetTextColor(text);
        game.SetTextColor(text, new NativeColor(color.R, color.G, color.B, game.GetFloorOpacity(floor)));
    }

    /// <summary>
    /// 砖块进入视野时接管游戏原有逻辑，保证叠加的倍率文字不被隐藏。
    /// 对应 PC 1.2.0 中返回 false 的 scrFloor.OnBecameVisible Prefix。
    /// </summary>
    internal void HandleFloorBecameVisible(nint floor)
    {
        GameApi? game = _game;
        if (game == null || floor == 0 || !_speedTextLevelReady)
            return;

        game.SetBehaviourEnabled(floor, true);

        if (_controller != 0 && game.IsGameWorld(_controller))
        {
            nint nextFloor = game.GetNextFloor(floor);
            if (nextFloor != 0 && game.GetFloorHoldLength(nextFloor) > -1)
                game.SetBehaviourEnabled(nextFloor, true);

            nint prevFloor = game.GetPrevFloor(floor);
            if (prevFloor != 0 && game.GetFloorHoldLength(prevFloor) > -1)
                game.SetBehaviourEnabled(prevFloor, true);
        }

        bool speedChange = IsSpeedChange(game, floor);
        nint previousFloor = game.GetPrevFloor(floor);
        nint text = game.GetFloorNumText(floor);
        if (text == 0)
        {
            double labelBaseBpm;
            lock (_stateLock)
                labelBaseBpm = _baseBpm;
            SpeedTextApplyStatus labelApplyStatus = SpeedTextApplyStatus.NoSpeedChange;
            bool labelApplied = labelBaseBpm > 0d
                && TryApplySpeedText(game, floor, labelBaseBpm, out labelApplyStatus);
            text = game.GetFloorNumText(floor);
            if (text == 0 && speedChange && !_speedTextVisibilityDiagnosticLogged)
            {
                _speedTextVisibilityDiagnosticLogged = true;
                Logger.Info(
                    LogTag,
                    $"Speed text visible probe: floor={Pointer(floor)}, previous={Pointer(previousFloor)}, "
                        + $"speedChange=true, text=0x0, object=0x0, status={labelApplyStatus}, "
                        + $"applied={labelApplied}");
            }
            if (text == 0)
                return;
        }

        double baseBpm;
        lock (_stateLock)
            baseBpm = _baseBpm;
        string before = speedChange && !_speedTextVisibilityDiagnosticLogged
            ? game.GetTextValue(text)
            : string.Empty;
        SpeedTextApplyStatus applyStatus = SpeedTextApplyStatus.NoSpeedChange;
        bool speedTextApplied = false;
        if (baseBpm > 0d)
            speedTextApplied = TryApplySpeedText(game, floor, baseBpm, out applyStatus);

        if (speedChange && !_speedTextVisibilityDiagnosticLogged)
        {
            _speedTextVisibilityDiagnosticLogged = true;
            Logger.Info(
                LogTag,
                $"Speed text visible probe: floor={Pointer(floor)}, previous={Pointer(previousFloor)}, "
                    + $"text={Pointer(text)}, object={Pointer(game.GetFloorNumObject(floor))}, "
                    + $"before='{before}', after='{game.GetTextValue(text)}', "
                    + $"status={applyStatus}, applied={speedTextApplied}, baseBpm={baseBpm:0.###}, "
                    + $"floorVisible={game.IsFloorVisible(floor)}");
        }

        nint editor = game.GetEditor();
        if (editor != 0)
        {
            bool showNums = game.GetShowFloorNums(editor);
            bool playMode = game.IsEditorPlayMode(editor);
            if (!speedChange && showNums && !playMode && game.IsLevelEditor())
            {
                game.SetTextValue(text, game.GetFloorSeqId(floor).ToString(CultureInfo.InvariantCulture));
                game.SetActive(game.GetFloorNumObject(floor), true);
            }
        }

        if (game.GetTextFirstChar(text) == 'x')
            game.SetActive(game.GetFloorNumObject(floor), true);
    }

    /// <summary>
    /// 接管编辑器绘制砖号：普通砖恢复序号，变速砖保留倍率文字，再按原规则决定显隐。
    /// 对应 PC 1.2.0 中返回 false 的 scnEditor.DrawFloorNums Prefix。
    /// </summary>
    internal void HandleDrawFloorNums(nint editor)
    {
        GameApi? game = _game;
        if (game == null || editor == 0)
            return;

        nint floors = game.GetEditorFloors(editor);
        if (floors == 0)
            return;

        bool showNums = game.GetShowFloorNums(editor);
        bool playMode = game.IsEditorPlayMode(editor);
        bool visible = showNums && !playMode;

        if (!_speedTextDrawDiagnosticLogged)
        {
            int count = game.GetCollectionCount(floors);
            _speedTextDrawDiagnosticLogged = true;
            Logger.Info(
                LogTag,
                $"DrawFloorNums intercepted: editor={Pointer(editor)}, floors={Pointer(floors)}, "
                    + $"count={count}, showNums={showNums}, playMode={playMode}, visible={visible}");
        }

        int countForDraw = game.GetCollectionCount(floors);
        for (int index = 0; index < countForDraw; index++)
        {
            nint floor = GameApi.GetCollectionItem(floors, index);
            if (floor == 0)
                continue;

            nint text = game.GetFloorNumText(floor);
            if (text == 0)
                continue;

            bool speedLabel = IsSpeedChange(game, floor);
            if (!speedLabel && (game.GetTextFirstChar(text) == 'x' || game.GetTextValue(text) == "t"))
            {
                game.SetTextValue(text, game.GetFloorSeqId(floor).ToString(CultureInfo.InvariantCulture));
                game.SetTextColor(text, BasicColor);
            }

            game.SetActive(game.GetFloorNumObject(floor), visible);
        }
    }

    /// <summary>把叠加过的倍率文字还原为砖号，避免关掉开关后画面上残留 "x1.5"。</summary>
    private void ClearSpeedText()
    {
        GameApi? game = _game;
        if (game == null || !_speedTextSupported)
            return;

        bool hadAppliedText = _speedTextApplied;
        _speedTextApplied = false;

        try
        {
            if (!hadAppliedText)
                return;

            nint floors = game.GetLevelFloors();
            if (floors == 0)
                return;

            int count = game.GetCollectionCount(floors);
            for (int index = 0; index < count; index++)
            {
                nint floor = GameApi.GetCollectionItem(floors, index);
                if (floor == 0)
                    continue;

                nint text = game.GetFloorNumText(floor);
                if (text == 0 || game.GetTextFirstChar(text) != 'x')
                    continue;

                game.SetTextValue(text, game.GetFloorSeqId(floor).ToString(CultureInfo.InvariantCulture));
                game.SetTextColor(text, BasicColor);
                game.SetActive(game.GetFloorNumObject(floor), false);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(LogTag, $"Speed text cleanup failed: {exception}");
        }
        finally
        {
            game.ClearCreatedSpeedTexts();
        }
    }

    private static string Pointer(nint value) => $"0x{value.ToInt64():X}";

    private static bool SpeedEqual(double first, double second)
    {
        return Math.Abs(first - second) < 0.001d;
    }

    private void TryStartFromCurrentLevel()
    {
        GameApi? game = _game;
        if (game == null || _controller == 0 || !game.IsGameWorld(_controller))
            return;
        if (game.GetCurrentFloor(_controller) != 0)
            HandleLevelStart();
    }

    private static double GetTileSpeed(GameApi game, nint controller, nint floor)
    {
        if (floor != 0)
        {
            double floorSpeed = game.GetFloorSpeed(floor);
            if (floorSpeed > 0d && !double.IsNaN(floorSpeed) && !double.IsInfinity(floorSpeed))
                return floorSpeed;
        }

        double controllerSpeed = game.GetSpeed(controller);
        return controllerSpeed > 0d
            && !double.IsNaN(controllerSpeed)
            && !double.IsInfinity(controllerSpeed)
            ? controllerSpeed
            : 1d;
    }

    private double GetRealBpm(GameApi game, nint floor, double speed)
    {
        nint nextFloor = game.GetNextFloor(floor);
        if (nextFloor == 0)
            return speed * _baseBpm;

        double delta = game.GetEntryTime(nextFloor) - game.GetEntryTime(floor);
        if (delta <= 0.0000001d)
            return speed * _baseBpm;
        return 60d / delta;
    }

    private static double GetAngleMoved(double entryAngle, double exitAngle, bool clockwise)
    {
        double direction = clockwise ? 1d : -1d;
        return PositiveModulo((exitAngle - entryAngle) * direction, TwoPi);
    }

    private static double AngleToTime(double angle, double bpm)
    {
        if (bpm <= 0.0000001d)
            return double.MaxValue;
        return PositiveModulo(angle, TwoPi) / Pi * (60d / bpm);
    }

    private static double PositiveModulo(double value, double modulus)
    {
        double result = value % modulus;
        return result < 0d ? result + modulus : result;
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) < 0.0001d;
    }

    private void DrawHud(ImDrawListPtr drawList)
    {
        NormalizeSettings();
        HudSnapshot snapshot;
        lock (_stateLock)
        {
            if (!_hudVisible)
                return;
            snapshot = new HudSnapshot(_tileBpm, _realBpm, _kps);
        }

        string text = BuildText(snapshot);
        if (string.IsNullOrEmpty(text))
            return;

        Vector2 displaySize = ImGui.GetIO().DisplaySize;
        if (displaySize.X <= 0f || displaySize.Y <= 0f)
            return;

        ImFontPtr font = ImGui.GetFont();
        float size = FontSize;
        Vector2 textSize = font.CalcTextSizeA(size, float.MaxValue, 0f, text);
        Vector2 position = new(PositionX * displaySize.X, (1f - PositionY) * displaySize.Y);
        if (Alignment == HudAlignment.Center)
            position.X -= textSize.X * 0.5f;
        else if (Alignment == HudAlignment.Right)
            position.X -= textSize.X;

        // PC TextBehaviour uses Shadow.effectColor alpha=0.45 and effectDistance=(2,-2).
        if (UseShadow)
            drawList.AddText(font, size, position + new Vector2(2f, 2f), 0x73000000, text);
        if (UseBold)
        {
            drawList.AddText(font, size, position + new Vector2(1f, 0f), 0xFFFFFFFF, text);
            drawList.AddText(font, size, position + new Vector2(0f, 1f), 0xFFFFFFFF, text);
        }
        drawList.AddText(font, size, position, 0xFFFFFFFF, text);
    }

    private string BuildText(HudSnapshot snapshot)
    {
        List<string> lines = new(3);
        if (ShowTileBpm)
            lines.Add(ApplyTemplate(TileBpmText, Format(snapshot.TileBpm)));
        if (ShowRealBpm)
            lines.Add(ApplyTemplate(RealBpmText, Format(snapshot.RealBpm)));
        if (ShowKps)
            lines.Add(ApplyTemplate(KpsText, snapshot.Kps.ToString(CultureInfo.InvariantCulture)));
        return string.Join('\n', lines);
    }

    private string Format(double value)
    {
        int places = Math.Clamp(DecimalPlaces, 0, 6);
        if (places == 0)
            return value.ToString("0", CultureInfo.InvariantCulture);
        string decimals = new(ZeroPadding ? '0' : '#', places);
        return value.ToString($"0.{decimals}", CultureInfo.InvariantCulture);
    }

    private static string ApplyTemplate(string? template, string value)
    {
        return (template ?? "{value}").Replace("{value}", value, StringComparison.Ordinal);
    }

    private void NormalizeSettings()
    {
        DecimalPlaces = Math.Clamp(DecimalPlaces, 0, 6);
        FontSize = Math.Clamp(FontSize, 1, 100);
        PositionX = Math.Clamp(PositionX, -0.1f, 1.1f);
        PositionY = Math.Clamp(PositionY, -0.1f, 1.1f);
        if (!Enum.IsDefined(Alignment))
            Alignment = HudAlignment.Right;
        if (!Enum.IsDefined(SpeedTextBasis))
            SpeedTextBasis = SpeedTextMode.Tile;
        TileBpmText ??= "타일 BPM - {value}";
        RealBpmText ??= "체감 BPM - {value}";
        KpsText ??= "초당 클릭 수 - {value}";
    }

    private readonly record struct HudSnapshot(double TileBpm, double RealBpm, int Kps);
}
