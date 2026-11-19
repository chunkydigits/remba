#Requires -Version 5.1
<#
.SYNOPSIS
  Starts the local Remba development environment.

.DESCRIPTION
  1. Starts the Azure Cosmos DB Emulator if it is not already running
  2. Starts the Angular UI on http://localhost:4600 (no SSL — required for cloudflared)
  3. Starts the .NET API (https://localhost:7257)

.EXAMPLE
  .\scripts\start-remba.ps1
#>

[CmdletBinding()]
param(
    [string]$CosmosEmulatorPath = "${env:ProgramFiles}\Azure Cosmos DB Emulator\Microsoft.Azure.Cosmos.Emulator.exe",
    [int]$CosmosPort = 8081,
    [int]$UiPort = 4600,
    [int]$CosmosReadyTimeoutSeconds = 180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$uiPath = Join-Path $repoRoot "src\ui"
$apiProject = Join-Path $repoRoot "src\api\Remba.Api\Remba.Api.csproj"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Test-PortListening {
    param(
        [string]$HostName = "localhost",
        [int]$Port
    )

    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $async = $client.BeginConnect($HostName, $Port, $null, $null)
        $waited = $async.AsyncWaitHandle.WaitOne(500)
        if (-not $waited) {
            $client.Close()
            return $false
        }

        $client.EndConnect($async)
        $client.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Get-CosmosEmulatorProcess {
    Get-Process -Name "Microsoft.Azure.Cosmos.Emulator", "CosmosDB.Emulator" -ErrorAction SilentlyContinue
}

function Start-CosmosEmulatorIfNeeded {
    Write-Step "Cosmos DB Emulator"

    if (Test-PortListening -Port $CosmosPort) {
        Write-Host "Already listening on port $CosmosPort." -ForegroundColor Green
        return
    }

    $existing = Get-CosmosEmulatorProcess
    if ($existing) {
        Write-Host "Emulator process is running; waiting for port $CosmosPort..." -ForegroundColor Yellow
    }
    else {
        if (-not (Test-Path -LiteralPath $CosmosEmulatorPath)) {
            throw "Cosmos DB Emulator not found at '$CosmosEmulatorPath'. Install it or pass -CosmosEmulatorPath."
        }

        Write-Host "Starting emulator: $CosmosEmulatorPath"
        Start-Process -FilePath $CosmosEmulatorPath | Out-Null
    }

    $deadline = (Get-Date).AddSeconds($CosmosReadyTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-PortListening -Port $CosmosPort) {
            Write-Host "Cosmos emulator is ready on port $CosmosPort." -ForegroundColor Green
            return
        }

        Start-Sleep -Seconds 2
    }

    throw "Timed out after $CosmosReadyTimeoutSeconds seconds waiting for Cosmos emulator on port $CosmosPort."
}

function Start-RembaUi {
    Write-Step "Angular UI (http://localhost:$UiPort, no SSL)"

    if (-not (Test-Path -LiteralPath $uiPath)) {
        throw "UI path not found: $uiPath"
    }

    if (Test-PortListening -Port $UiPort) {
        Write-Host "Port $UiPort is already in use — assuming UI is running." -ForegroundColor Yellow
        return
    }

    # Explicit --ssl false keeps cloudflared happy (must remain HTTP).
    $uiCommand = @"
Set-Location -LiteralPath '$uiPath'
Write-Host 'Starting Remba UI on http://localhost:$UiPort (SSL disabled)...' -ForegroundColor Cyan
npm start -- --port $UiPort --ssl false --host localhost
"@

    Start-Process -FilePath "powershell.exe" -ArgumentList @(
        "-NoExit",
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-Command", $uiCommand
    ) | Out-Null

    Write-Host "UI starting in a new window." -ForegroundColor Green
}

function Start-RembaApi {
    Write-Step ".NET API (https://localhost:7257)"

    if (-not (Test-Path -LiteralPath $apiProject)) {
        throw "API project not found: $apiProject"
    }

    if (Test-PortListening -Port 7257) {
        Write-Host "Port 7257 is already in use — assuming API is running." -ForegroundColor Yellow
        return
    }

    $apiCommand = @"
Set-Location -LiteralPath '$(Split-Path -Parent $apiProject)'
Write-Host 'Starting Remba API...' -ForegroundColor Cyan
dotnet run --project '$apiProject' --launch-profile https
"@

    Start-Process -FilePath "powershell.exe" -ArgumentList @(
        "-NoExit",
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-Command", $apiCommand
    ) | Out-Null

    Write-Host "API starting in a new window." -ForegroundColor Green
}

Write-Host "Remba local environment" -ForegroundColor White
Write-Host "Repo: $repoRoot"

Start-CosmosEmulatorIfNeeded
Start-RembaUi
Start-RembaApi

Write-Step "Done"
Write-Host "UI:  http://localhost:$UiPort"
Write-Host "API: https://localhost:7257"
Write-Host "Cosmos: https://localhost:$CosmosPort"
Write-Host ""
Write-Host "Leave the UI and API windows open while you work."
