# 発行スクリプト
# 使い方: .\publish.ps1 [-SelfContained]
param([switch]$SelfContained)

$dotnet = "$env:USERPROFILE\dotnet-sdk\8\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

$out = if ($SelfContained) { "publish-sc" } else { "publish" }
$args = @("publish", "-c", "Release", "-o", $out)
if ($SelfContained) { $args += "-p:SelfContained=true" }

& $dotnet @args
if ($LASTEXITCODE -eq 0) { Write-Host "OK: $out\TVClockModern.exe" -ForegroundColor Green }
