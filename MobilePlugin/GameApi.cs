using System.Runtime.InteropServices;
using StArray.ModManager.Il2Cpp;
using StArray.ModManager.Manager;
using StArray.ModManager.Mono;
using StArray.ModManager.RuntimeAbstractions;

namespace ShowBPM.Mobile;

internal sealed unsafe class GameApi
{
    private const string LogTag = "ShowBPM";

    private readonly IAppDomain _domain;
    private readonly IRuntimeAssembly _assembly;
    private readonly IRuntimeClass _controllerClass;
    private readonly IRuntimeClass _floorClass;
    private readonly IRuntimeClass _conductorClass;
    private readonly IRuntimeClass? _adoBaseClass;
    private readonly IRuntimeClass? _editorClass;
    private readonly IRuntimeClass? _rdStringClass;
    private readonly IRuntimeClass? _levelMakerClass;
    private readonly IRuntimeClass? _letterPressClass;
    private readonly IRuntimeClass? _floorRendererClass;
    private readonly IRuntimeClass? _gameClass;
    private readonly IRuntimeClass? _levelDataClass;
    private readonly IRuntimeClass? _gcsClass;
    private readonly IRuntimeClass? _rdConstantsClass;
    private readonly IRuntimeClass? _gameObjectClass;

    private readonly IRuntimeField? _controllerInstance;
    private readonly IRuntimeField? _gameWorld;
    private readonly IRuntimeField? _speed;
    private readonly IRuntimeField? _currentSequenceId;
    private readonly IRuntimeField? _averageFrameTime;
    private readonly IRuntimeField? _firstFloor;

    private readonly IRuntimeField? _nextFloor;
    private readonly IRuntimeField? _entryTime;
    private readonly IRuntimeField? _entryAngle;
    private readonly IRuntimeField? _exitAngle;
    private readonly IRuntimeField? _isCounterClockwise;
    private readonly IRuntimeField? _floorController;
    private readonly IRuntimeField? _floorConductor;

    private readonly IRuntimeField? _floorSpeed;
    private readonly IRuntimeField? _floorSeqId;
    private readonly IRuntimeField? _floorOpacity;
    private readonly IRuntimeField? _floorIsFading;
    private readonly IRuntimeField? _floorHoldLength;
    private readonly IRuntimeField? _floorFreeroamGenerated;
    private readonly IRuntimeField? _prevFloor;
    private readonly IRuntimeField? _editorNumText;
    private readonly IRuntimeField? _floorRenderer;
    private readonly IRuntimeField? _legacyFloorSpriteRenderer;
    private readonly IRuntimeField? _letterText;
    private readonly IRuntimeField? _rendererOfFloorRenderer;

    private readonly IRuntimeField? _conductorBpm;
    private readonly IRuntimeField? _conductorSong;
    private readonly IRuntimeField? _editorPlaybackSpeed;
    private readonly IRuntimeField? _editorShowFloorNums;
    private readonly IRuntimeField? _language;
    private readonly IRuntimeField? _levelMakerInstanceField;
    private readonly IRuntimeField? _levelMakerFloors;
    private readonly IRuntimeField? _textValue;
    private readonly IRuntimeField? _gameInstance;
    private readonly IRuntimeField? _gameLevelData;
    private readonly IRuntimeField? _currentSpeedTrial;
    private readonly IRuntimeField? _prefabLetterPress;

    private readonly IRuntimeMethod? _getController;
    private readonly IRuntimeMethod? _getConductor;
    private readonly IRuntimeMethod? _getEditor;
    private readonly IRuntimeMethod? _getCurrentFloor;
    private readonly IRuntimeMethod? _getConductorInstance;
    private readonly IRuntimeMethod? _getLevelMakerInstance;
    private readonly IRuntimeMethod? _getEditorPlayMode;
    private readonly IRuntimeMethod? _getEditorFloors;
    private readonly IRuntimeMethod? _getIsLevelEditor;
    private readonly IRuntimeMethod? _getGameInstance;
    private readonly IRuntimeMethod? _getLevelDataPitch;
    private readonly IRuntimeMethod? _getGameConstants;

    private readonly SetStringDelegate? _setText;
    private readonly nint _setTextMethodInfo;
    private readonly SetColorDelegate? _setGraphicColor;
    private readonly nint _setGraphicColorMethodInfo;
    private readonly GetColorDelegate? _getGraphicColor;
    private readonly nint _getGraphicColorMethodInfo;
    private readonly SetBooleanDelegate? _setGameObjectActive;
    private readonly nint _setGameObjectActiveMethodInfo;
    private readonly GetObjectDelegate? _getComponentGameObject;
    private readonly nint _getComponentGameObjectMethodInfo;
    private readonly SetBooleanDelegate? _setBehaviourEnabled;
    private readonly nint _setBehaviourEnabledMethodInfo;
    private readonly GetBooleanDelegate? _getRendererVisible;
    private readonly nint _getRendererVisibleMethodInfo;
    private readonly IRuntimeMethod? _instantiateWithParent;
    private readonly GetComponentByNameDelegate? _getComponentByName;
    private readonly nint _getComponentByNameMethodInfo;
    private readonly GetComponentByTypeDelegate? _getComponentByType;
    private readonly nint _getComponentByTypeMethodInfo;
    private readonly GetObjectDelegate? _getComponentTransform;
    private readonly nint _getComponentTransformMethodInfo;
    private readonly DestroyObjectDelegate? _destroyObject;
    private readonly nint _destroyObjectMethodInfo;
    private readonly Dictionary<nint, CreatedSpeedText> _createdSpeedTexts = new();
    private bool _textWriteFailureLogged;
    private bool _activationFailureLogged;
    private bool _textCreationFailureLogged;

    private GameApi(IAppDomain domain, IRuntimeAssembly assembly)
    {
        _domain = domain;
        _assembly = assembly;
        _controllerClass = RequireClass("scrController");
        _floorClass = RequireClass("scrFloor");
        _conductorClass = RequireClass("scrConductor");
        _adoBaseClass = FindClass("ADOBase");
        _editorClass = FindClass("scnEditor");
        _rdStringClass = FindClass("RDString");
        _levelMakerClass = FindClass("scrLevelMaker");
        _letterPressClass = FindClass("scrLetterPress");
        _floorRendererClass = FindClass("FloorRenderer");
        _gameClass = FindClass("scnGame");
        _levelDataClass = _assembly.GetClass("ADOFAI", "LevelData");
        _gcsClass = FindClass("GCS");
        _rdConstantsClass = FindClass("RDConstants");

        _controllerInstance = FindField(_controllerClass, "_instance", "instance");
        _gameWorld = FindField(_controllerClass, "gameworld", "isGameWorld", "isgameworld", "GameWorld");
        _speed = FindField(_controllerClass, "d_speed", "speed", "_speed", "currentSpeed", "_currentSpeed");
        _currentSequenceId = FindField(_controllerClass, "currentSeqID", "currentSequenceId");
        _averageFrameTime = FindField(_controllerClass, "averageFrameTime");
        _firstFloor = FindField(_controllerClass, "firstFloor");

        _nextFloor = FindField(_floorClass, "nextfloor", "nextFloor");
        _entryTime = FindField(_floorClass, "entryTime");
        _entryAngle = FindField(_floorClass, "entryangle", "entryAngle");
        _exitAngle = FindField(_floorClass, "exitangle", "exitAngle");
        _isCounterClockwise = FindField(_floorClass, "isCCW", "isCounterClockwise");
        _floorController = FindField(_floorClass, "controller");
        _floorConductor = FindField(_floorClass, "conductor");

        _floorSpeed = FindField(_floorClass, "speed");
        _floorSeqId = FindField(_floorClass, "seqID", "seqId");
        _floorOpacity = FindField(_floorClass, "opacity");
        _floorIsFading = FindField(_floorClass, "isFading");
        _floorHoldLength = FindField(_floorClass, "holdLength");
        _floorFreeroamGenerated = FindField(_floorClass, "freeroamGenerated");
        _prevFloor = FindField(_floorClass, "prevfloor", "prevFloor");
        _editorNumText = FindField(_floorClass, "editorNumText");
        _floorRenderer = FindField(_floorClass, "floorRenderer");
        _legacyFloorSpriteRenderer = FindField(_floorClass, "legacyFloorSpriteRenderer");
        _letterText = FindField(_letterPressClass, "letterText");
        _rendererOfFloorRenderer = FindField(_floorRendererClass, "renderer");

        _conductorBpm = FindField(_conductorClass, "bpm");
        _conductorSong = FindField(_conductorClass, "song");
        _editorPlaybackSpeed = FindField(_editorClass, "playbackSpeed");
        _editorShowFloorNums = FindField(_editorClass, "showFloorNums");
        _language = FindField(_rdStringClass, "language");
        _levelMakerInstanceField = FindField(_levelMakerClass, "_instance", "instance");
        _levelMakerFloors = FindField(_levelMakerClass, "listFloors");
        _gameInstance = FindField(_gameClass, "_instance", "instance");
        _gameLevelData = FindField(_gameClass, "levelData");
        _currentSpeedTrial = FindField(_gcsClass, "currentSpeedTrial");
        _prefabLetterPress = FindField(_rdConstantsClass, "prefabLetterPress");

        _getController = _adoBaseClass?.GetMethod("get_controller", 0)
            ?? _controllerClass.GetMethod("get_instance", 0);
        _getConductor = _adoBaseClass?.GetMethod("get_conductor", 0);
        _getEditor = _adoBaseClass?.GetMethod("get_editor", 0);
        _getCurrentFloor = _controllerClass.GetMethod("get_currFloor", 0);
        _getConductorInstance = _conductorClass.GetMethod("get_instance", 0);
        _getLevelMakerInstance = _levelMakerClass?.GetMethod("get_instance", 0);
        _getEditorPlayMode = _editorClass?.GetMethod("get_playMode", 0);
        _getEditorFloors = _editorClass?.GetMethod("get_floors", 0);
        _getIsLevelEditor = _adoBaseClass?.GetMethod("get_isLevelEditor", 0);
        _getGameInstance = _gameClass?.GetMethod("get_instance", 0);
        _getLevelDataPitch = _levelDataClass?.GetMethod("get_pitch", 0);
        _getGameConstants = _adoBaseClass?.GetMethod("get_gc", 0)
            ?? _rdConstantsClass?.GetMethod("get_data", 0);

        IRuntimeClass? textClass = FindClassInDomain("UnityEngine.UI", "Text");
        IRuntimeClass? graphicClass = FindClassInDomain("UnityEngine.UI", "Graphic");
        IRuntimeClass? gameObjectClass = FindClassInDomain("UnityEngine", "GameObject");
        IRuntimeClass? componentClass = FindClassInDomain("UnityEngine", "Component");
        IRuntimeClass? behaviourClass = FindClassInDomain("UnityEngine", "Behaviour");
        IRuntimeClass? rendererClass = FindClassInDomain("UnityEngine", "Renderer");
        IRuntimeClass? objectClass = FindClassInDomain("UnityEngine", "Object");
        _gameObjectClass = gameObjectClass;
        _textValue = FindField(textClass, "m_Text");

        _setText = Bind<SetStringDelegate>(textClass, "set_text",
            new[] { "System.String" }, out _setTextMethodInfo);
        _setGraphicColor = Bind<SetColorDelegate>(graphicClass, "set_color",
            new[] { "UnityEngine.Color" }, out _setGraphicColorMethodInfo);
        _getGraphicColor = Bind<GetColorDelegate>(graphicClass, "get_color",
            Array.Empty<string>(), out _getGraphicColorMethodInfo);
        _setGameObjectActive = Bind<SetBooleanDelegate>(gameObjectClass, "SetActive",
            new[] { "System.Boolean" }, out _setGameObjectActiveMethodInfo);
        _getComponentGameObject = Bind<GetObjectDelegate>(componentClass, "get_gameObject",
            Array.Empty<string>(), out _getComponentGameObjectMethodInfo);
        _setBehaviourEnabled = Bind<SetBooleanDelegate>(behaviourClass, "set_enabled",
            new[] { "System.Boolean" }, out _setBehaviourEnabledMethodInfo);
        _getRendererVisible = Bind<GetBooleanDelegate>(rendererClass, "get_isVisible",
            Array.Empty<string>(), out _getRendererVisibleMethodInfo);
        _instantiateWithParent = objectClass?.GetMethod("Instantiate",
            "UnityEngine.Object", "UnityEngine.Transform");
        _getComponentByName = Bind<GetComponentByNameDelegate>(gameObjectClass, "GetComponent",
            new[] { "System.String" }, out _getComponentByNameMethodInfo)
            ?? Bind<GetComponentByNameDelegate>(gameObjectClass, "GetComponent",
                new[] { "String" }, out _getComponentByNameMethodInfo);
        _getComponentByType = Bind<GetComponentByTypeDelegate>(gameObjectClass, "GetComponent",
            new[] { "System.Type" }, out _getComponentByTypeMethodInfo)
            ?? Bind<GetComponentByTypeDelegate>(gameObjectClass, "GetComponent",
                new[] { "Type" }, out _getComponentByTypeMethodInfo);
        _getComponentTransform = Bind<GetObjectDelegate>(componentClass, "get_transform",
            Array.Empty<string>(), out _getComponentTransformMethodInfo);
        _destroyObject = Bind<DestroyObjectDelegate>(objectClass, "Destroy",
            new[] { "UnityEngine.Object" }, out _destroyObjectMethodInfo);
    }

    private bool HasPrefabSpeedTextPath =>
        _prefabLetterPress != null
        && _letterText != null
        && _letterPressClass != null
        && (_getComponentByName != null || _getComponentByType != null)
        && _getComponentTransform != null
        && _instantiateWithParent != null;

    /// <summary>速度倍率文字需要的所有运行时句柄都解析成功时为真。</summary>
    internal bool SupportsSpeedText =>
        _levelMakerFloors != null
        && _letterText != null
        && _floorSpeed != null
        && _textValue != null
        && _setText != null
        && _setGraphicColor != null
        && _setGameObjectActive != null
        && _getComponentGameObject != null
        && (_editorNumText != null || HasPrefabSpeedTextPath);

    internal string DescribeSpeedTextBindings()
    {
        return $"supported={SupportsSpeedText}; "
            + $"levelMakerClass={Present(_levelMakerClass)}, "
            + $"levelMakerInstance={Present(_getLevelMakerInstance) || Present(_levelMakerInstanceField)}, "
            + $"listFloors={Present(_levelMakerFloors)}, "
            + $"floorSpeed={Present(_floorSpeed)}, prevFloor={Present(_prevFloor)}, "
            + $"editorNumText={Present(_editorNumText)}, letterText={Present(_letterText)}, "
            + $"getGameConstants={Present(_getGameConstants)}, prefabLetterPress={Present(_prefabLetterPress)}, "
            + $"instantiateWithParent={Present(_instantiateWithParent)}, "
            + $"getComponentByName={Present(_getComponentByName)}, getComponentByType={Present(_getComponentByType)}, "
            + $"letterPressClass={Present(_letterPressClass)}, "
            + $"getTransform={Present(_getComponentTransform)}, destroy={Present(_destroyObject)}, "
            + $"textValue={Present(_textValue)}, setText={Present(_setText)}, "
            + $"setColor={Present(_setGraphicColor)}, getColor={Present(_getGraphicColor)}, "
            + $"setActive={Present(_setGameObjectActive)}, getGameObject={Present(_getComponentGameObject)}, "
            + $"setBehaviour={Present(_setBehaviourEnabled)}, rendererVisible={Present(_getRendererVisible)}";
    }

    internal static GameApi? Create()
    {
        IAppDomain? domain = RuntimeManager.GetDomain();
        if (domain == null)
            return null;

        IRuntimeAssembly? assembly = domain.OpenAssembly("Assembly-CSharp.dll")
            ?? domain.OpenAssembly("Assembly-CSharp");
        return assembly == null ? null : new GameApi(domain, assembly);
    }

    internal IRuntimeMethod? ResolveMethod(string className, string methodName, int parameterCount)
    {
        return FindClass(className)?.GetMethod(methodName, parameterCount);
    }

    internal nint GetController(nint floor = 0)
    {
        nint controller = InvokeStaticObject(_getController);
        if (controller != 0)
            return controller;
        controller = Read(_controllerInstance, 0, nint.Zero);
        return controller != 0 ? controller : Read(_floorController, floor, nint.Zero);
    }

    internal nint GetConductor(nint floor = 0)
    {
        nint conductor = InvokeStaticObject(_getConductor);
        if (conductor != 0)
            return conductor;
        conductor = InvokeStaticObject(_getConductorInstance);
        return conductor != 0 ? conductor : Read(_floorConductor, floor, nint.Zero);
    }

    internal nint GetCurrentFloor(nint controller)
    {
        if (controller == 0)
            return 0;
        try
        {
            nint floor = _getCurrentFloor?.Invoke(controller) ?? 0;
            return floor != 0 ? floor : Read(_firstFloor, controller, nint.Zero);
        }
        catch
        {
            return Read(_firstFloor, controller, nint.Zero);
        }
    }

    internal bool IsGameWorld(nint controller)
    {
        if (controller == 0)
            return false;
        return _gameWorld == null || Read(_gameWorld, controller, (byte)1) != 0;
    }

    internal float GetSpeed(nint controller)
    {
        float speed = Read(_speed, controller, 1f);
        return speed > 0f ? speed : 1f;
    }

    internal int GetCurrentSequenceId(nint controller)
    {
        return Read(_currentSequenceId, controller, 0);
    }

    internal float GetAverageFrameTime(nint controller)
    {
        float value = Read(_averageFrameTime, controller, 1f / 60f);
        return value > 0f ? value : 1f / 60f;
    }

    internal nint GetNextFloor(nint floor)
    {
        return Read(_nextFloor, floor, nint.Zero);
    }

    internal double GetEntryTime(nint floor)
    {
        return Read(_entryTime, floor, 0d);
    }

    internal double GetEntryAngle(nint floor)
    {
        return Read(_entryAngle, floor, 0d);
    }

    internal double GetExitAngle(nint floor)
    {
        return Read(_exitAngle, floor, 0d);
    }

    internal bool IsCounterClockwise(nint floor)
    {
        return Read(_isCounterClockwise, floor, (byte)0) != 0;
    }

    internal float GetConductorBpm(nint conductor)
    {
        return Read(_conductorBpm, conductor, 0f);
    }

    internal float GetSongPitch(nint conductor)
    {
        nint song = Read(_conductorSong, conductor, nint.Zero);
        if (song == 0)
            return 1f;

        try
        {
            float pitch = new RuntimeObject(song).InvokeUnbox<float>("get_pitch", 0);
            return Math.Abs(pitch) > 0.0001f ? pitch : 1f;
        }
        catch
        {
            return 1f;
        }
    }

    internal float GetPlaybackSpeed()
    {
        nint editor = InvokeStaticObject(_getEditor);
        if (editor == 0)
            return 1f;
        float speed = Read(_editorPlaybackSpeed, editor, 1f);
        return speed > 0f ? speed : 1f;
    }

    internal int GetLanguageCode()
    {
        return Read(_language, 0, 10);
    }

    // ── 速度倍率文字 ──

    /// <summary>scrLevelMaker.instance.listFloors，取不到时返回 0。</summary>
    internal nint GetLevelFloors()
    {
        nint levelMaker = InvokeStaticObject(_getLevelMakerInstance);
        if (levelMaker == 0)
            levelMaker = Read(_levelMakerInstanceField, 0, nint.Zero);
        return levelMaker == 0 ? 0 : Read(_levelMakerFloors, levelMaker, nint.Zero);
    }

    /// <summary>
    /// 读取实时维护的托管集合。不能使用 GetEnumerator，因为关卡加载和播放时
    /// listFloors 可能同时发生变化，枚举器会因版本号变化直接抛异常。
    /// </summary>
    internal int GetCollectionCount(nint collection)
    {
        if (collection == 0)
            return 0;
        try
        {
            return Math.Max(0, new RuntimeObject(collection).InvokeUnbox<int>("get_Count", 0));
        }
        catch
        {
            return 0;
        }
    }

    internal static unsafe nint GetCollectionItem(nint collection, int index)
    {
        if (collection == 0 || index < 0)
            return 0;
        try
        {
            int value = index;
            return new RuntimeObject(collection).Invoke("get_Item", 1, new[] { (nint)(&value) });
        }
        catch
        {
            // The collection can shrink between get_Count and get_Item.
            return 0;
        }
    }

    internal float GetFloorSpeed(nint floor)
    {
        return Read(_floorSpeed, floor, 1f);
    }

    internal int GetFloorSeqId(nint floor)
    {
        return Read(_floorSeqId, floor, 0);
    }

    internal float GetFloorOpacity(nint floor)
    {
        return Read(_floorOpacity, floor, 1f);
    }

    internal bool IsFloorFading(nint floor)
    {
        return Read(_floorIsFading, floor, (byte)0) != 0;
    }

    internal int GetFloorHoldLength(nint floor)
    {
        return Read(_floorHoldLength, floor, 0);
    }

    internal bool IsFreeroamGenerated(nint floor)
    {
        return Read(_floorFreeroamGenerated, floor, (byte)0) != 0;
    }

    internal nint GetPrevFloor(nint floor)
    {
        return Read(_prevFloor, floor, nint.Zero);
    }

    /// <summary>scrFloor.editorNumText.letterText（UnityEngine.UI.Text），取不到时返回 0。</summary>
    internal nint GetFloorNumText(nint floor)
    {
        nint letterPress = Read(_editorNumText, floor, nint.Zero);
        nint text = letterPress == 0 ? 0 : Read(_letterText, letterPress, nint.Zero);
        if (text != 0)
            return text;
        return _createdSpeedTexts.TryGetValue(floor, out CreatedSpeedText created)
            ? created.Text
            : 0;
    }

    internal nint GetFloorNumObject(nint floor)
    {
        nint letterPress = Read(_editorNumText, floor, nint.Zero);
        nint gameObject = letterPress == 0 ? 0 : GetGameObject(letterPress);
        if (gameObject != 0)
            return gameObject;
        return _createdSpeedTexts.TryGetValue(floor, out CreatedSpeedText created)
            ? created.GameObject
            : 0;
    }

    /// <summary>
    /// 游戏中的 scrFloor 通常没有 editorNumText；沿用游戏 typing mode 使用的
    /// RDConstants.prefabLetterPress，在砖块位置创建一个独立的世界空间文字对象。
    /// </summary>
    internal bool TryCreateSpeedText(nint floor, out nint text)
    {
        text = GetFloorNumText(floor);
        if (text != 0 || floor == 0)
            return text != 0;

        if (!HasPrefabSpeedTextPath)
        {
            LogTextCreationFailure("speed label prefab/runtime bindings are unavailable");
            return false;
        }

        nint prefab = GetSpeedTextPrefab();
        if (prefab == 0)
        {
            LogTextCreationFailure("RDConstants.prefabLetterPress is null");
            return false;
        }

        nint floorTransform = GetComponentTransform(floor);
        if (floorTransform == 0)
        {
            LogTextCreationFailure($"scrFloor transform is null for floor={Pointer(floor)}");
            return false;
        }

        nint clone;
        try
        {
            // RuntimeInvoke marshals Unity value/reference types correctly. Calling the
            // position/rotation overload through a raw delegate corrupted Unity state on
            // ARM64 during scene reconstruction.
            clone = _instantiateWithParent!.InvokeStatic(new[] { prefab, floorTransform });
        }
        catch (Exception exception)
        {
            LogTextCreationFailure($"Instantiate(Object, Transform) threw: {exception.Message}");
            return false;
        }

        if (clone == 0)
        {
            LogTextCreationFailure("Instantiate(Object, Transform) returned null");
            return false;
        }

        nint gameObject;
        nint letterPress;
        if (IsInstanceOf(clone, _letterPressClass))
        {
            letterPress = clone;
            gameObject = GetGameObject(letterPress);
        }
        else if (IsInstanceOf(clone, _gameObjectClass))
        {
            gameObject = clone;
            letterPress = GetComponentByName(gameObject, "scrLetterPress");
        }
        else
        {
            DestroyObject(clone);
            LogTextCreationFailure("instantiated prefab is neither scrLetterPress nor GameObject");
            return false;
        }

        nint labelText = letterPress == 0 ? 0 : Read(_letterText, letterPress, nint.Zero);
        if (gameObject == 0 || labelText == 0)
        {
            DestroyObject(clone);
            LogTextCreationFailure("instantiated prefab has no scrLetterPress.letterText");
            return false;
        }

        _createdSpeedTexts[floor] = new CreatedSpeedText(gameObject, labelText);
        SetActive(gameObject, true);
        text = labelText;
        Logger.Info(
            LogTag,
            $"Speed text label created: floor={Pointer(floor)}, object={Pointer(gameObject)}, "
                + $"text={Pointer(labelText)}, prefab={Pointer(prefab)}");
        return true;
    }

    internal void ClearCreatedSpeedTexts()
    {
        foreach (CreatedSpeedText created in _createdSpeedTexts.Values)
            DestroyObject(created.GameObject);
        _createdSpeedTexts.Clear();
    }

    /// <summary>
    /// 场景重建时 Unity 会自行销毁作为砖块子对象的标签。这里只清空旧指针，不能在
    /// 控制器 Awake 的对象构造阶段再次调用 Destroy。
    /// </summary>
    internal void ForgetCreatedSpeedTexts() => _createdSpeedTexts.Clear();

    /// <summary>砖块的主渲染器或旧版精灵渲染器任意一个可见即为真。</summary>
    internal bool IsFloorVisible(nint floor)
    {
        nint floorRenderer = Read(_floorRenderer, floor, nint.Zero);
        if (floorRenderer != 0 && IsRendererVisible(Read(_rendererOfFloorRenderer, floorRenderer, nint.Zero)))
            return true;
        return IsRendererVisible(Read(_legacyFloorSpriteRenderer, floor, nint.Zero));
    }

    private bool IsRendererVisible(nint renderer)
    {
        if (renderer == 0 || _getRendererVisible == null)
            return false;
        try
        {
            return _getRendererVisible(renderer, _getRendererVisibleMethodInfo) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取 Text 的首字符；空串或读取失败时返回 '\0'。</summary>
    internal char GetTextFirstChar(nint text)
    {
        nint value = Read(_textValue, text, nint.Zero);
        if (value == 0)
            return '\0';
        try
        {
            RuntimeString runtimeString = new(value);
            return runtimeString.Length <= 0 ? '\0' : runtimeString.ToString()[0];
        }
        catch
        {
            return '\0';
        }
    }

    /// <summary>读取 Text 的完整内容；失败时返回空串。</summary>
    internal string GetTextValue(nint text)
    {
        nint value = Read(_textValue, text, nint.Zero);
        if (value == 0)
            return string.Empty;
        try
        {
            return new RuntimeString(value).ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>写 Text 内容。同时调用 setter 并回写 m_Text，避免 IL2CPP 下 setter 被内联导致不生效。</summary>
    internal bool SetTextValue(nint text, string value)
    {
        if (text == 0)
            return false;
        if (_setText == null)
        {
            LogTextWriteFailure("UnityEngine.UI.Text.set_text is unavailable");
            return false;
        }
        try
        {
            RuntimeString runtimeString = RuntimeString.New(_domain, value);
            if (!runtimeString.IsValid)
            {
                LogTextWriteFailure("RuntimeString.New returned a null pointer");
                return false;
            }
            _setText(text, runtimeString.Ptr, _setTextMethodInfo);
            Write(_textValue, text, runtimeString.Ptr);
            return true;
        }
        catch (Exception exception)
        {
            LogTextWriteFailure($"set_text threw: {exception.Message}");
            return false;
        }
    }

    internal void SetTextColor(nint text, NativeColor color)
    {
        if (text == 0 || _setGraphicColor == null)
            return;
        try
        {
            _setGraphicColor(text, color, _setGraphicColorMethodInfo);
        }
        catch
        {
        }
    }

    internal NativeColor GetTextColor(nint text)
    {
        if (text == 0 || _getGraphicColor == null)
            return new NativeColor(1f, 1f, 1f, 1f);
        try
        {
            return _getGraphicColor(text, _getGraphicColorMethodInfo);
        }
        catch
        {
            return new NativeColor(1f, 1f, 1f, 1f);
        }
    }

    internal void SetActive(nint gameObject, bool active)
    {
        if (gameObject == 0)
            return;
        if (_setGameObjectActive == null)
        {
            LogActivationFailure("UnityEngine.GameObject.SetActive is unavailable");
            return;
        }
        try
        {
            _setGameObjectActive(gameObject, active ? (byte)1 : (byte)0, _setGameObjectActiveMethodInfo);
        }
        catch (Exception exception)
        {
            LogActivationFailure($"SetActive threw: {exception.Message}");
        }
    }

    internal void SetBehaviourEnabled(nint behaviour, bool enabled)
    {
        if (behaviour == 0 || _setBehaviourEnabled == null)
            return;
        try
        {
            _setBehaviourEnabled(behaviour, enabled ? (byte)1 : (byte)0, _setBehaviourEnabledMethodInfo);
        }
        catch
        {
        }
    }

    private nint GetGameObject(nint component)
    {
        if (component == 0 || _getComponentGameObject == null)
            return 0;
        try
        {
            return _getComponentGameObject(component, _getComponentGameObjectMethodInfo);
        }
        catch
        {
            return 0;
        }
    }

    private nint GetSpeedTextPrefab()
    {
        nint constants = InvokeStaticObject(_getGameConstants);
        return Read(_prefabLetterPress, constants, nint.Zero);
    }

    private nint GetComponentTransform(nint component)
    {
        if (component == 0 || _getComponentTransform == null)
            return 0;
        try
        {
            return _getComponentTransform(component, _getComponentTransformMethodInfo);
        }
        catch
        {
            return 0;
        }
    }

    private nint GetComponentByName(nint gameObject, string typeName)
    {
        if (gameObject == 0)
            return 0;

        if (_getComponentByName != null)
        {
            try
            {
                RuntimeString value = RuntimeString.New(_domain, typeName);
                if (value.IsValid)
                {
                    nint component = _getComponentByName(gameObject, value.Ptr, _getComponentByNameMethodInfo);
                    if (component != 0)
                        return component;
                }
            }
            catch
            {
            }
        }

        return typeName == "scrLetterPress"
            ? GetComponentByType(gameObject, _letterPressClass)
            : 0;
    }

    private nint GetComponentByType(nint gameObject, IRuntimeClass? componentClass)
    {
        if (gameObject == 0 || componentClass == null || _getComponentByType == null)
            return 0;

        try
        {
            nint typeObject = GetRuntimeTypeObject(componentClass);
            return typeObject == 0
                ? 0
                : _getComponentByType(gameObject, typeObject, _getComponentByTypeMethodInfo);
        }
        catch
        {
            return 0;
        }
    }

    private nint GetRuntimeTypeObject(IRuntimeClass? runtimeClass)
    {
        if (runtimeClass == null)
            return 0;

        if (RuntimeManager.IsIl2Cpp)
        {
            nint type = Il2CppFunctions.il2cpp_class_get_type(runtimeClass.Ptr);
            return type == 0 ? 0 : Il2CppFunctions.il2cpp_type_get_object(type);
        }

        if (RuntimeManager.IsMono)
        {
            nint type = MonoFunctions.MonoClassGetType(runtimeClass.Ptr);
            return type == 0
                ? 0
                : (nint)Methods.mono_type_get_object(
                    (_MonoDomain*)_domain.Ptr,
                    (_MonoType*)type);
        }

        return 0;
    }

    private static bool IsInstanceOf(nint instance, IRuntimeClass? expectedClass)
    {
        if (instance == 0 || expectedClass == null)
            return false;

        try
        {
            if (RuntimeManager.IsIl2Cpp)
            {
                nint actualClass = Il2CppFunctions.il2cpp_object_get_class(instance);
                return actualClass != 0
                    && Il2CppFunctions.il2cpp_class_is_assignable_from(expectedClass.Ptr, actualClass);
            }

            // The mobile target uses IL2CPP. Keeping the exact-class fallback prevents
            // Mono builds from passing an arbitrary pointer to GameObject.GetComponent.
            return RuntimeManager.IsMono && MonoFunctions.MonoObjectGetClass(instance) == expectedClass.Ptr;
        }
        catch
        {
            return false;
        }
    }

    private void DestroyObject(nint gameObject)
    {
        if (gameObject == 0 || _destroyObject == null)
            return;
        try
        {
            _destroyObject(gameObject, _destroyObjectMethodInfo);
        }
        catch
        {
        }
    }

    // ── 编辑器 ──

    internal nint GetEditor()
    {
        return InvokeStaticObject(_getEditor);
    }

    internal nint GetEditorFloors(nint editor)
    {
        if (editor == 0 || _getEditorFloors == null)
            return 0;
        try
        {
            return _getEditorFloors.Invoke(editor);
        }
        catch
        {
            return 0;
        }
    }

    internal bool GetShowFloorNums(nint editor)
    {
        return Read(_editorShowFloorNums, editor, (byte)0) != 0;
    }

    internal bool IsEditorPlayMode(nint editor)
    {
        if (editor == 0 || _getEditorPlayMode == null)
            return false;
        try
        {
            return _getEditorPlayMode.InvokeUnbox<byte>(editor) != 0;
        }
        catch
        {
            return false;
        }
    }

    internal bool IsLevelEditor()
    {
        if (_getIsLevelEditor == null)
            return false;
        try
        {
            return _getIsLevelEditor.InvokeStaticUnbox<byte>() != 0;
        }
        catch
        {
            return false;
        }
    }

    // ── 速度试炼倍速 ──

    /// <summary>
    /// scnGame 场景下的 pitch：LevelData.pitch / 100 * GCS.currentSpeedTrial。
    /// 取不到 scnGame 或 LevelData 时返回 0，调用方回退到 conductor.song.pitch。
    /// </summary>
    internal float GetScenePitch()
    {
        nint game = InvokeStaticObject(_getGameInstance);
        if (game == 0)
            game = Read(_gameInstance, 0, nint.Zero);
        if (game == 0 || _getLevelDataPitch == null)
            return 0f;

        nint levelData = Read(_gameLevelData, game, nint.Zero);
        if (levelData == 0)
            return 0f;

        try
        {
            int pitch = _getLevelDataPitch.InvokeUnbox<int>(levelData);
            if (pitch <= 0)
                return 0f;
            return pitch / 100f * GetSpeedTrial();
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>GCS.currentSpeedTrial；字段缺失或非正数时回退为 1。</summary>
    internal float GetSpeedTrial()
    {
        float speed = Read(_currentSpeedTrial, 0, 1f);
        return speed > 0f ? speed : 1f;
    }

    private IRuntimeClass RequireClass(string name)
    {
        return FindClass(name) ?? throw new InvalidOperationException($"Game class not found: {name}");
    }

    private IRuntimeClass? FindClass(string name)
    {
        return _assembly.GetClass(string.Empty, name);
    }

    /// <summary>跨程序集查类，用于 UnityEngine / UnityEngine.UI 等非 Assembly-CSharp 的类型。</summary>
    private IRuntimeClass? FindClassInDomain(string namespaze, string name)
    {
        foreach (IRuntimeAssembly assembly in _domain.GetAssemblies())
        {
            try
            {
                IRuntimeClass? type = assembly.GetClass(namespaze, name);
                if (type != null)
                    return type;
            }
            catch
            {
            }
        }
        return null;
    }

    private static TDelegate? Bind<TDelegate>(
        IRuntimeClass? type,
        string methodName,
        string[] parameterTypes,
        out nint methodInfo) where TDelegate : Delegate
    {
        methodInfo = 0;
        IRuntimeMethod? method = type?.GetMethod(methodName, parameterTypes);
        nint function = method?.FunctionPtr ?? 0;
        if (method == null || function == 0)
            return null;
        methodInfo = method.Ptr;
        return Marshal.GetDelegateForFunctionPointer<TDelegate>(function);
    }

    private static IRuntimeField? FindField(IRuntimeClass? type, params string[] names)
    {
        if (type == null)
            return null;
        foreach (string name in names)
        {
            IRuntimeField? field = type.GetField(name);
            if (field != null)
                return field;
        }
        return null;
    }

    private static nint InvokeStaticObject(IRuntimeMethod? method)
    {
        try
        {
            return method?.InvokeStatic() ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static T Read<T>(IRuntimeField? field, nint instance, T fallback) where T : unmanaged
    {
        if (field == null || (!field.IsStatic && instance == 0))
            return fallback;
        try
        {
            return field.GetValue<T>(instance);
        }
        catch
        {
            return fallback;
        }
    }

    private static void Write<T>(IRuntimeField? field, nint instance, T value) where T : unmanaged
    {
        if (field == null || (!field.IsStatic && instance == 0))
            return;
        try
        {
            field.SetValue(instance, value);
        }
        catch
        {
        }
    }

    private void LogTextWriteFailure(string message)
    {
        if (_textWriteFailureLogged)
            return;
        _textWriteFailureLogged = true;
        Logger.Warn(LogTag, $"Speed text write failed: {message}");
    }

    private void LogActivationFailure(string message)
    {
        if (_activationFailureLogged)
            return;
        _activationFailureLogged = true;
        Logger.Warn(LogTag, $"Speed text activation failed: {message}");
    }

    private void LogTextCreationFailure(string message)
    {
        if (_textCreationFailureLogged)
            return;
        _textCreationFailureLogged = true;
        Logger.Warn(LogTag, $"Speed text label creation failed: {message}");
    }

    private static bool Present(object? value) => value != null;

    private static string Pointer(nint value) => $"0x{value.ToInt64():X}";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetStringDelegate(nint instance, nint value, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetColorDelegate(nint instance, NativeColor value, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate NativeColor GetColorDelegate(nint instance, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetBooleanDelegate(nint instance, byte value, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte GetBooleanDelegate(nint instance, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetObjectDelegate(nint instance, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetComponentByNameDelegate(nint instance, nint typeName, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint GetComponentByTypeDelegate(nint instance, nint typeObject, nint methodInfo);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void DestroyObjectDelegate(nint instance, nint methodInfo);

    private readonly struct CreatedSpeedText(nint gameObject, nint text)
    {
        internal readonly nint GameObject = gameObject;
        internal readonly nint Text = text;
    }
}

/// <summary>与 UnityEngine.Color 布局一致，按值传给 Graphic.set_color。</summary>
internal readonly struct NativeColor(float r, float g, float b, float a)
{
    internal readonly float R = r;
    internal readonly float G = g;
    internal readonly float B = b;
    internal readonly float A = a;
}
