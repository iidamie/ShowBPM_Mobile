# Compile-time references

- `StArray.ModManager.dll`、`StArray.ModManager.Analyzer.dll` 和 `ImGui.NET.dll` 来自当前 StArray.ModManager 源码树（新版尚未发布 DLL，与 Replay 手机版使用同一套引用）。
- `StArray.ModManager.Analyzer.dll` 是编译期 Source Generator，用于生成 `[UnmanagedHook]` 的安装/卸载代码，不会打包进 Mod。

这些都是编译期引用，不要放进 ShowBPM 的 Mod 目录，手机端管理器已经提供。
