# 天赋图书馆 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task by task.

**Goal:** 交付带编译DLL的RimWorld 1.6图书系统安装包。
**Architecture:** 可独立测试的LearningRules核心；原版Book/书柜整合；Harmony用于作者品质与永久工作解锁。Reading用自定义任务避免原版附加智识XP。
**Tech Stack:** C#、.NET Framework、RimRef 1.6.4633、Harmony、XML。
**Spec:** ../specs/2026-10-05-talent-library.md

## Global Constraints
- 一种写书台，一种2×1书柜，一种通用技能书，三个消耗书。
- 天赋阶段18/18/10；每60tick；20/15/15–20；品质0.5至2倍。
- 不额外叠天赋倍率；有存档标记；不能误用特殊书。

## Review Focus
- 达到上限前的最后脉冲不可超限。
- 无法学习的双火不可阻塞其余技能。
- 书柜中的书必须能拿出、预留、读后搬回。
- 特性/身份/技能禁用缓存读档后刷新。
- 消耗书目标中途变更、失效或已有状态时不可消耗。

### Task 1: 学习规则和品质
Files: Source/LearningRules.cs, Source/Tests/RulesTests.cs.
Interface: LearningRules.SelectStage, Allocate, QualityCap, Knowledge.
- [x] 写并运行C#失败测试：阶段优先、双火随机、无火平均、禁用技能、品质边界、上限精确截断。
- [x] 实现纯C#规则，运行同一测试至通过。
- [x] Commit。

### Task 2: 游戏整合
Files: Source/Reading.cs, Writing.cs, SpecialBooks.cs, UnlockWork.cs; Defs/*; Textures/*.
Interface: Reading调用Task1；Writing使用知识品质；SpecialBooks调用解锁标记。
- [x] 编写Def集成检查并先确认缺少定义失败。
- [x] 实现Book效果、阅读任务、工作类型、配方、书柜与手动消耗书；编译对照真实1.6接口。
- [x] 执行完整规则测试和Def校验；Commit。

### Task 3: 交付
Files: README、CHANGELOG、检查记录、安装zip。
- [x] 全量编译；规则测试；XML继承/Def引用/贴图/程序集依赖检查。
- [x] 自检阅读循环、保存缓存、作者品质、特殊书使用入口。
- [x] 记录未进行游戏实测；打包并保存交付。
