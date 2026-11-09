param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$versionMatch = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
if (-not $versionMatch) { throw 'Unity Editor version was not found in ProjectVersion.txt.' }
$editorVersion = $versionMatch.Matches[0].Groups[1].Value
$unityExe = Join-Path 'C:\Program Files\Unity\Hub\Editor' "$editorVersion\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $unityExe)) { throw "Unity $editorVersion is not installed at $unityExe" }

$androidDir = Join-Path $projectRoot 'Builds\Android'
$logsDir = Join-Path $projectRoot 'Logs'
$resultPath = Join-Path $androidDir 'build-result.txt'
$requestPath = Join-Path $androidDir 'build-request.txt'
$apkPath = Join-Path $androidDir 'StackyWarriors.apk'
New-Item -ItemType Directory -Force -Path $androidDir, $logsDir | Out-Null

function Read-FreshResult([datetime]$startedUtc) {
    if (-not (Test-Path -LiteralPath $resultPath)) { return $null }
    $file = Get-Item -LiteralPath $resultPath
    if ($file.LastWriteTimeUtc -lt $startedUtc) { return $null }
    return (Get-Content -LiteralPath $resultPath -Raw)
}

# A second Unity process cannot open this project. The running Editor's own
# Tools/Build menu uses the same AndroidBuildAutomation recipe.
$editorRunning = $false
try {
    $processes = Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'"
    foreach ($process in $processes) {
        if ([string]$process.ExecutablePath -ine $unityExe) { continue }
        $line = [string]$process.CommandLine
        if ($line.IndexOf($projectRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
            $line -notmatch '(?i)-batchmode|AssetImportWorker') {
            $editorRunning = $true
            break
        }
    }
} catch {
    throw 'Cannot check whether this Unity project is already open. Use Tools > Build > Build Android APK (Automatic) in Unity.'
}

if ($editorRunning) {
    $startedUtc = [datetime]::UtcNow
    Set-Content -LiteralPath $requestPath -Value $startedUtc.ToString('o')
    Write-Host 'Unity is open. Requested the same build from its Tools/Build menu.'
    $deadline = (Get-Date).AddMinutes(45)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $result = Read-FreshResult $startedUtc
        if ($result) {
            Write-Host $result.TrimEnd()
            if ($result.StartsWith('Succeeded')) { Write-Host "APK: $apkPath"; exit 0 }
            exit 1
        }
    }
    throw "Unity did not finish the build within 45 minutes. Check $requestPath and the Unity Console."
}

# Same headless command and build settings used for the successful Android APK.
# Retry Unity startup when licensing or another early failure prevents the
# build method from writing a new result. The build method retries build errors.
for ($attempt = 1; $attempt -le 3; $attempt++) {
    $startedUtc = [datetime]::UtcNow
    $stamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
    $logPath = Join-Path $logsDir "android-build-$stamp-attempt$attempt.log"
    Write-Host "Android build startup $attempt/3. Log: $logPath"
    $unityArgs = @(
        '-batchmode', '-quit',
        '-projectPath', ('"{0}"' -f $projectRoot),
        '-buildTarget', 'Android',
        '-executeMethod', 'AndroidBuildAutomation.BuildFromCommandLine',
        '-logFile', ('"{0}"' -f $logPath)
    )
    $unityProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -Wait -WindowStyle Hidden
    $result = Read-FreshResult $startedUtc
    if ($result) {
        Write-Host $result.TrimEnd()
        if ($result.StartsWith('Succeeded')) { Write-Host "APK: $apkPath"; exit 0 }
        Write-Error "Android build failed. Read $logPath"
        exit 1
    }

    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
        if ($logText -match 'error CS\d+|Compilation failed') {
            Write-Error "Unity compilation failed. Read $logPath"
            exit 1
        }
    }
    Write-Warning "Unity exited before writing a new build result (exit code $($unityProcess.ExitCode))."
}

throw "Unity could not start the Android build after 3 attempts. See logs in $logsDir"
