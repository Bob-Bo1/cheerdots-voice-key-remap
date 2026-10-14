param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$taskOutput=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $PSScriptRoot 'app'}
New-Item -ItemType Directory -Force "$taskOutput/temp"|Out-Null
$taskCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if(-not (Test-Path -LiteralPath $taskCompiler)){throw '需要 Windows x64 和 .NET Framework 4.8。'}
$taskOldTemp=$env:TEMP;$taskOldTmp=$env:TMP
try{
    $env:TEMP="$taskOutput/temp";$env:TMP=$env:TEMP
    $taskSources=@(Get-ChildItem "$PSScriptRoot/shared" -Filter '*.cs'|ForEach-Object FullName)+@(Get-ChildItem "$PSScriptRoot/src" -Filter '*.cs'|ForEach-Object FullName)
    $taskRefs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Runtime.Serialization.dll','/reference:System.Core.dll')
    $taskRefs+=('/win32manifest:'+(Join-Path $PSScriptRoot 'VoiceLite.manifest'))
    $taskRefs+=('/win32icon:'+(Join-Path $PSScriptRoot 'assets/voice-tool.ico'))
    $taskRefs+=('/resource:'+(Join-Path $PSScriptRoot 'assets/voice-tool.ico')+',VoiceTool.Icon')
    & $taskCompiler /nologo /warn:4 /warnaserror+ /target:exe /platform:x64 $taskRefs /main:CheerVoiceLite.LiteEntry "/out:$taskOutput/Checks.exe" $taskSources
    if($LASTEXITCODE -ne 0){throw '检查程序编译失败'}
    foreach($taskCheck in @('--check','--log-check','--dual-check','--startup-check')){& "$taskOutput/Checks.exe" $taskCheck;if($LASTEXITCODE -ne 0){throw "检查失败: $taskCheck"}}
    & $taskCompiler /nologo /warn:4 /warnaserror+ /target:winexe /platform:x64 $taskRefs /main:CheerVoiceLite.LiteEntry "/out:$taskOutput/语音改建工具.exe" $taskSources
    if($LASTEXITCODE -ne 0){throw '程序编译失败'}
    $taskPreview=Start-Process -FilePath "$taskOutput/语音改建工具.exe" -ArgumentList '--preview' -WindowStyle Hidden -Wait -PassThru
    if($taskPreview.ExitCode -ne 0){throw '窗口预览检查失败'}
    Write-Host "构建完成：$taskOutput/语音改建工具.exe"
}finally{$env:TEMP=$taskOldTemp;$env:TMP=$taskOldTmp}