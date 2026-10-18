# 构建

安装 .NET SDK 后，在本目录运行：

```sh
dotnet build -c Release
```

项目使用固定版本的公开参考程序集，不需要分发或复制 RimWorld 游戏 DLL。输出只需保留 `../Assemblies/TalentLibrary.dll`，不要把编译参考程序集或 Harmony 重复带进游戏。

独立规则测试（Mono）：

```sh
mcs -out:/tmp/TalentLibraryRulesTests.exe LearningRules.cs Tests/RulesTests.cs
mono /tmp/TalentLibraryRulesTests.exe
python3 Tests/validate.py
```

项目支持 .NET SDK 重新构建，目标为 net472，并按固定版本的 RimWorld 参考程序集编译。本次修改已使用 .NET SDK 8.0 构建。

兼容性：单份 DLL 按 RimWorld 1.6.4633 参考程序集构建，Mod 声明并支持 RimWorld 1.6 与 1.7。
