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

成品DLL在本环境以Mono编译器编译，通过同版本RimRef与Harmony引用。项目文件提供.NET SDK重新构建入口，该入口未在本环境执行（未安装.NET SDK）。
