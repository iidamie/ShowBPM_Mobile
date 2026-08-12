# ShowBPM 手机版

这是 `ADOFAI_ShowBPM` 针对 `StArray.ModManager` Android/IL2CPP 环境的移植。

## 功能

- 轨道 BPM、体感 BPM、KPS
- 变速砖（蜗牛 / 兔子）上叠加显示 `x倍率` 文字，加速偏红、减速偏蓝，可选按轨道 BPM 或体感 BPM 计算（对应 PC 版 1.2.0 的 `showSpeedText`）
- 忽略同打
- 阴影、粗体、小数位、零占位
- 位置、字号和左/中/右对齐
- 根据游戏语言显示中文、英文或韩文设置界面
- 设置由 ModManager 保存到 `ShowBPM/settings.json`
- 内置 GitHub 更新：发现新版本后可在 Mod 设置页下载并安装，重启游戏后生效

BPM 会跟随速度试炼倍速（`GCS.currentSpeedTrial`）变化。

## 构建

需要 .NET 10 SDK：

```bash
dotnet build MobilePlugin/ShowBPM.csproj -c Release
python3 package_mod.py
```

编译引用来自当前 StArray.ModManager 源码树（见 `References/README.md`），游戏 Hook 由
`StArray.ModManager.Analyzer` 这个 Source Generator 从 `[UnmanagedHook]` 标注生成。
最终 Mod 包不会包含这些公共依赖。

## 自动构建与发布

推送 `MobilePlugin`、`VERSION.txt`、打包脚本或 Release Workflow 的变更到 `main` 后，
GitHub Actions 会自动构建 Mod、校验安装包、保存 Actions Artifact，并发布
`v{VERSION.txt}` GitHub Release。

发布新版本前先提高 `VERSION.txt` 中的版本号。每个版本标签固定对应一个提交，避免
Release 的源码与二进制不一致。

## 安装结构

解压到手机 ModManager 的 `mods` 目录：

```text
mods/
└── ShowBPM/
    └── ShowBPM.dll
```

此版本不使用 UnityModManager，因此不需要 `Info.json`，入口由 `IModPlugin` 自动发现。

## 内置更新

ShowBPM 启动时会检查 [iidamie/ShowBPM_Mobile](https://github.com/iidamie/ShowBPM_Mobile)
的最新 GitHub Release。发现新版本后，可在 Mod 设置页下载对应的
`ShowBPM-版本.zip`。下载包会验证程序集并事务替换 `ShowBPM.dll`；`settings.json`
和其它用户文件不会被覆盖。安装完成后重启游戏即可生效。

## 下载

预编译安装包发布在 [GitHub Releases](https://github.com/iidamie/ShowBPM_Mobile/releases)。
