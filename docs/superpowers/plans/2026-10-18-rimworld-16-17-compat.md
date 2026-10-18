# RimWorld 1.6/1.7 Compatibility Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Harmony startup binding error and validate one Talent Library DLL for RimWorld 1.6 and 1.7.

**Architecture:** Keep the existing shared assembly and gameplay behavior. Bind `Pawn_SkillTracker.Learn` arguments by position in the Harmony Prefix, build against the existing RimWorld 1.6 reference package, then load that DLL in an isolated RimWorld 1.7 profile before declaring both versions supported.

**Tech Stack:** C# 7.3, .NET Framework 4.7.2, Harmony, RimWorld Ref 1.6.4633, Python XML/Def validator, PowerShell isolated game startup regression, RimWorld 1.7.4981.

**Spec:** [../specs/2026-10-18-rimworld-16-17-compat.md](../specs/2026-10-18-rimworld-16-17-compat.md)

## Global Constraints

- Preserve existing reading, writing, skill XP, book effects, and save behavior.
- Keep one shared DLL unless integration testing proves a version-specific API is required.
- Declare both `1.6` and `1.7` supported only after the RimWorld 1.7 isolated startup passes.
- Do not change the user's normal RimWorld configuration or attribute unrelated mod errors to Talent Library.
- Do not claim a RimWorld 1.6 game launch test; this machine has RimWorld 1.7 installed.

## Review Focus

- **Target parameter names differ between game versions while order and types remain stable:** the regression harness runs the actual 1.7 game against the pre-fix DLL, then the fixed DLL.
- **Target parameter order or types differ:** compile against the 1.6 reference package and inspect the real 1.7 Harmony patch result.
- **Prefix behavior changes beyond binding:** review that the existing Intellectual, positive-XP, Reading-job, and universal-book conditions are unchanged apart from the two local argument names.
- **Harmony startup failure prevents later patches from registering:** inspect the complete isolated startup log for all Talent Library initialization and patch errors.
- **Metadata claims support for an untested artifact:** stage the built DLL by hash; only after 1.7 startup passes, update metadata and validate the exact version list.

---

### Task 1: Reproduce and fix the Harmony startup failure

**Files:**
- Create: `Source/Tests/verify_harmony_startup.ps1`
- Modify: `Source/Reading.cs` (`Patch_NoExtraIntellectual.Prefix`)
- Modify: `Assemblies/TalentLibrary.dll` (build output)

**Interfaces:**
- Consumes: the existing isolated profile `C:\Users\jizig\Documents\ChatGPT\RimWorld-TalentLibrary-isolated-test`, whose active mod list includes Core, installed DLC, Harmony, and `Wentao.TalentLibrary`.
- Produces: a real game-start regression script that fails with the existing DLL and passes with the rebuilt DLL; the script always restores the game-local DLL.

- [x] **Step 1: Write the isolated startup regression script before changing production code**

Create `Source/Tests/verify_harmony_startup.ps1`. It stages the candidate DLL and About.xml, runs the real isolated game until mod initialization, inspects the game's shared log for Talent Library errors or an incompatible-version warning, and restores the original mod files and shared logs from `finally`:

```powershell
param(
    [Parameter(Mandatory=$true)][string]$GameExe,
    [Parameter(Mandatory=$true)][string]$SavedataFolder,
    [Parameter(Mandatory=$true)][string]$GameModFolder,
    [Parameter(Mandatory=$true)][string]$CandidateDll,
    [Parameter(Mandatory=$true)][string]$CandidateAboutXml,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$gameExePath = [System.IO.Path]::GetFullPath($GameExe)
$savedataPath = [System.IO.Path]::GetFullPath($SavedataFolder)
$gameModPath = [System.IO.Path]::GetFullPath($GameModFolder)
$candidatePath = [System.IO.Path]::GetFullPath($CandidateDll)
$candidateAboutPath = [System.IO.Path]::GetFullPath($CandidateAboutXml)
$modsConfigPath = Join-Path $savedataPath 'Config\ModsConfig.xml'
$gameModDll = Join-Path $gameModPath 'Assemblies\TalentLibrary.dll'
$gameModAbout = Join-Path $gameModPath 'About\About.xml'
$backupFolder = Join-Path $savedataPath 'staged-original'
$backupDll = Join-Path $backupFolder 'TalentLibrary.dll'
$backupAbout = Join-Path $backupFolder 'About.xml'
$testLog = Join-Path $savedataPath 'Player.log'
$sharedLogFolder = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs'
$sharedLog = Join-Path $sharedLogFolder 'Player.log'
$sharedPreviousLog = Join-Path $sharedLogFolder 'Player-prev.log'
$backupSharedLog = Join-Path $backupFolder 'Player.log.before-test'
$backupSharedPreviousLog = Join-Path $backupFolder 'Player-prev.log.before-test'

foreach ($path in @($gameExePath, $modsConfigPath, $gameModDll, $gameModAbout, $candidatePath, $candidateAboutPath, $sharedLogFolder)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required test path not found: $path" }
}

[xml]$modsConfig = Get-Content -LiteralPath $modsConfigPath -Raw
$activeMods = @($modsConfig.SelectNodes('/ModsConfigData/activeMods/li') | ForEach-Object { $_.InnerText })
if ($activeMods -notcontains 'Wentao.TalentLibrary') { throw 'Isolated profile does not activate Wentao.TalentLibrary' }
if (Get-CimInstance Win32_Process -Filter "Name = 'RimWorld.exe'") {
    throw 'Close all RimWorld processes before testing; this game writes to the shared Player.log'
}

New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
$hadSharedLog = Test-Path -LiteralPath $sharedLog
$hadSharedPreviousLog = Test-Path -LiteralPath $sharedPreviousLog
Copy-Item -LiteralPath $gameModDll -Destination $backupDll -Force
Copy-Item -LiteralPath $gameModAbout -Destination $backupAbout -Force
if ($hadSharedLog) { Copy-Item -LiteralPath $sharedLog -Destination $backupSharedLog -Force }
if ($hadSharedPreviousLog) { Copy-Item -LiteralPath $sharedPreviousLog -Destination $backupSharedPreviousLog -Force }
$process = $null

try {
    Copy-Item -LiteralPath $candidatePath -Destination $gameModDll -Force
    if ((Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $gameModDll -Algorithm SHA256).Hash) {
        throw 'Staged game DLL does not match the candidate DLL'
    }
    Copy-Item -LiteralPath $candidateAboutPath -Destination $gameModAbout -Force
    if ((Get-FileHash -LiteralPath $candidateAboutPath -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $gameModAbout -Algorithm SHA256).Hash) {
        throw 'Staged game About.xml does not match the candidate About.xml'
    }
    foreach ($logPath in @($sharedLog, $sharedPreviousLog, $testLog)) {
        if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }
    }

    $arguments = @('-quicktest', "-savedatafolder=`"$savedataPath`"")
    $process = Start-Process -FilePath $gameExePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $ready = $false
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $sharedLog -PathType Leaf) {
            $log = Get-Content -LiteralPath $sharedLog -Raw
            if ($log -match 'Initializing new game with mods:') { $ready = $true; break }
        }
        if ($process.HasExited) { break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) { throw "RimWorld did not reach mod initialization within $TimeoutSeconds seconds" }
    if ($log -notmatch '1\.7\.4981') { throw 'Shared log does not identify RimWorld 1.7.4981' }

    $errors = @($log -split '\r?\n' | Where-Object {
        $_ -match '(?i)(error|exception|fatal)' -and
        $_ -match '(?i)(TalentLibrary|Talent Library|Patch_NoExtraIntellectual)'
    })
    if ($errors.Count -gt 0) {
        $errors | Write-Output
        throw 'Talent Library errors were found during isolated mod initialization'
    }
    if ($log -match 'Wentao\.TalentLibrary \(incompatible version\)') {
        throw 'Isolated game reports Talent Library as incompatible with RimWorld 1.7'
    }
    Write-Output 'PASS RimWorld 1.7.4981 Talent Library startup'
}
finally {
    try {
        if ($process -and -not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit()
        }
        if (Test-Path -LiteralPath $sharedLog) { Copy-Item -LiteralPath $sharedLog -Destination $testLog -Force }
        if (Test-Path -LiteralPath $sharedPreviousLog) {
            Copy-Item -LiteralPath $sharedPreviousLog -Destination (Join-Path $savedataPath 'Player-prev.after-test.log') -Force
        }
    }
    finally {
        try {
            if ($hadSharedLog) {
                Copy-Item -LiteralPath $backupSharedLog -Destination $sharedLog -Force
            }
            elseif (Test-Path -LiteralPath $sharedLog) {
                Remove-Item -LiteralPath $sharedLog -Force
            }
            if ($hadSharedPreviousLog) {
                Copy-Item -LiteralPath $backupSharedPreviousLog -Destination $sharedPreviousLog -Force
            }
            elseif (Test-Path -LiteralPath $sharedPreviousLog) {
                Remove-Item -LiteralPath $sharedPreviousLog -Force
            }
        }
        finally {
            try {
                Copy-Item -LiteralPath $backupDll -Destination $gameModDll -Force
            }
            finally {
                Copy-Item -LiteralPath $backupAbout -Destination $gameModAbout -Force
            }
        }
    }
}
```

- [x] **Step 2: Run it against the unchanged DLL and verify the failure is the reported bug**

Run:

```powershell
& Source\Tests\verify_harmony_startup.ps1 `
  -GameExe 'D:\steam\steamapps\common\RimWorld\RimWorld.exe' `
  -SavedataFolder 'C:\Users\jizig\Documents\ChatGPT\RimWorld-TalentLibrary-isolated-test' `
  -GameModFolder 'D:\steam\steamapps\common\RimWorld\Mods\TalentLibrary' `
  -CandidateDll (Resolve-Path Assemblies\TalentLibrary.dll).Path `
  -CandidateAboutXml (Resolve-Path About\About.xml).Path
```

Expected: nonzero exit with `Talent Library errors were found during isolated mod initialization`, and the isolated log includes the `TalentLibrary.Startup` Harmony argument binding error (`Parameter "s" not found`). The installed game DLL must be restored by the script's `finally` block.

- [x] **Step 3: Change only the Prefix binding names and their references**

Use this exact signature and preserve the existing return conditions and comments:

```csharp
public static bool Prefix(Pawn_SkillTracker __instance, SkillDef __0, float __1)
{
    // Vanilla's recreational Reading adds Intellectual XP on every tick.
    // Our direct SkillRecord awards never pass through this tracker method.
    var pawn = __instance.GetSkill(SkillDefOf.Intellectual).Pawn;
    return __0 != SkillDefOf.Intellectual || __1 <= 0f || pawn.CurJobDef != JobDefOf.Reading ||
        pawn.CurJob.targetA.Thing == null || pawn.CurJob.targetA.Thing.TryGetComp<CompUniversalBook>() == null;
}
```

- [x] **Step 4: Install the .NET 8 SDK locally if the system SDK remains unavailable, then build**

Install the SDK only under `$env:TEMP`:

```powershell
$installer = Join-Path $env:TEMP 'dotnet-install-talentlibrary.ps1'
$sdkRoot = Join-Path $env:TEMP 'TalentLibrary-dotnet-sdk'
Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
& $installer -Channel 8.0 -InstallDir $sdkRoot
& "$sdkRoot\dotnet.exe" build Source\TalentLibrary.csproj --configuration Release
```

Expected: build succeeds and updates `Assemblies/TalentLibrary.dll`, using the existing `Krafs.Rimworld.Ref` 1.6.4633 package.

- [x] **Step 5: Re-run the real 1.7 startup regression with the rebuilt DLL**

Run the Step 2 command again. Expected: exit code 0 and `PASS RimWorld 1.7.4981 Talent Library startup`; no Talent Library load error appears in the isolated log. The script restores the game-local DLL after both successful and failed runs.

- [x] **Step 6: Run the existing XML/Def and core-rule checks**

Run:

```powershell
& 'C:\Users\jizig\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' Source\Tests\validate.py
$ruleExe = Join-Path $env:TEMP 'TalentLibraryRulesTests.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' "/out:$ruleExe" Source\LearningRules.cs Source\Tests\RulesTests.cs
& $ruleExe
```

Expected: XML/Def validation passes and the executable reports `PASS 34 rule assertions`.

- [x] **Step 7: Commit Task 1 on the isolated branch**

```powershell
git add Source/Tests/verify_harmony_startup.ps1 Source/Reading.cs Assemblies/TalentLibrary.dll
git commit -m "fix: bind Talent Library Harmony args positionally"
```

---

### Task 2: Declare and document dual-version support

**Files:**
- Modify: `Source/Tests/validate.py`
- Modify: `About/About.xml` (`supportedVersions`)
- Modify: `Source/AssemblyInfo.cs` (assembly description)
- Modify: `Source/README.md` (build and game-support note)

**Interfaces:**
- Consumes: the passing shared DLL and isolated 1.7 startup from Task 1.
- Produces: exact 1.6/1.7 support metadata, matching documentation, and final validation.

- [x] **Step 1: Write the supported-version assertion and verify it fails first**

Replace the current loop that requires every version to equal `1.6` in `Source/Tests/validate.py` with:

```python
supported = [n.text for n in E.parse(root/'About/About.xml').getroot().find('supportedVersions')]
assert supported == ['1.6', '1.7'], 'Mod must declare RimWorld 1.6 and 1.7 support'
```

Run the validator. Expected: the new assertion fails because metadata still lists only `1.6`.

- [x] **Step 2: Add 1.7 to About metadata after the passing game test**

Set the supported version children to exactly `<li>1.6</li>` followed by `<li>1.7</li>`; retain all dependency and load-order entries.

- [x] **Step 3: Update the assembly and build documentation**

Set the attribute in `Source/AssemblyInfo.cs` to:

```csharp
[assembly: AssemblyDescription("Passion-directed reading, knowledge-based writing and manual special books for RimWorld 1.6 and 1.7")]
```

Add this sentence to `Source/README.md` without removing its existing build or test commands:

```text
兼容性：单份 DLL 按 RimWorld 1.6.4633 参考程序集构建，Mod 声明并支持 RimWorld 1.6 与 1.7。
```

- [x] **Step 4: Run final automated verification**

Run the validator, 34-rule executable, and `git diff --check`. Expected: XML/Def/source metadata validation passes, `PASS 34 rule assertions`, and no whitespace errors. Verify the game-local test DLL was restored to its original SHA256.

- [x] **Step 5: Commit Task 2 on the isolated branch**

```powershell
git add Source/Tests/validate.py About/About.xml Source/AssemblyInfo.cs Source/README.md Assemblies/TalentLibrary.dll
git commit -m "docs: declare Talent Library RimWorld 1.6 and 1.7 support"
```

#### Final review refinement: stage the candidate About.xml

The first final-DLL startup log contained `Wentao.TalentLibrary (incompatible version)` because the harness staged only the DLL while the installed mod folder still had its old 1.6-only metadata. The harness now stages and restores the worktree's candidate `About/About.xml` as well, checks its hash, and fails if RimWorld still reports the mod as incompatible. The new incompatibility assertion failed against the stale installed metadata, then the isolated 1.7.4981 launch passed after About.xml staging.
