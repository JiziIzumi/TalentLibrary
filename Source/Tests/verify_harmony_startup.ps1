param(
    [Parameter(Mandatory=$true)][string]$GameExe,
    [Parameter(Mandatory=$true)][string]$SavedataFolder,
    [Parameter(Mandatory=$true)][string]$GameModFolder,
    [Parameter(Mandatory=$true)][string]$CandidateDll,
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$gameExePath = [System.IO.Path]::GetFullPath($GameExe)
$savedataPath = [System.IO.Path]::GetFullPath($SavedataFolder)
$gameModPath = [System.IO.Path]::GetFullPath($GameModFolder)
$candidatePath = [System.IO.Path]::GetFullPath($CandidateDll)
$modsConfigPath = Join-Path $savedataPath 'Config\ModsConfig.xml'
$gameModDll = Join-Path $gameModPath 'Assemblies\TalentLibrary.dll'
$backupFolder = Join-Path $savedataPath 'staged-original'
$backupDll = Join-Path $backupFolder 'TalentLibrary.dll'
$testLog = Join-Path $savedataPath 'Player.log'
$sharedLogFolder = Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Logs'
$sharedLog = Join-Path $sharedLogFolder 'Player.log'
$sharedPreviousLog = Join-Path $sharedLogFolder 'Player-prev.log'
$backupSharedLog = Join-Path $backupFolder 'Player.log.before-test'
$backupSharedPreviousLog = Join-Path $backupFolder 'Player-prev.log.before-test'

foreach ($path in @($gameExePath, $modsConfigPath, $gameModDll, $candidatePath, $sharedLogFolder)) {
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
if ($hadSharedLog) { Copy-Item -LiteralPath $sharedLog -Destination $backupSharedLog -Force }
if ($hadSharedPreviousLog) { Copy-Item -LiteralPath $sharedPreviousLog -Destination $backupSharedPreviousLog -Force }
$process = $null

try {
    Copy-Item -LiteralPath $candidatePath -Destination $gameModDll -Force
    if ((Get-FileHash -LiteralPath $candidatePath -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $gameModDll -Algorithm SHA256).Hash) {
        throw 'Staged game DLL does not match the candidate DLL'
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
            Copy-Item -LiteralPath $backupDll -Destination $gameModDll -Force
        }
    }
}
