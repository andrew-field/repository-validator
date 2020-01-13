<#
    .SYNOPSIS
    This script configures production alerts for Repository Validator

    .DESCRIPTION
    This retrieves correct function app endpoint which received the alerts
    generated by repository validator.
#>
param(
    [Parameter(Mandatory = $true)][string]$MonitoredWebAppResourceGroup,
    [Parameter(Mandatory = $true)][string]$MonitoredWebAppName = $MonitoredWebAppResourceGroup,
    [Parameter(Mandatory = $true)][string]$AlertHandlingResourceGroup,
    [Parameter(Mandatory = $true)][string]$AlertHandlingWebAppName = $AlertHandlingResourceGroup,
    [Parameter(Mandatory = $true)][string]$AlertHandlingFunction = 'AlertEndpoint',
    [Parameter(Mandatory = $true)][string]$AlertSlackChannel
)
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$webApp = Get-AzWebApp `
    -ResourceGroupName $AlertHandlingResourceGroup `
    -Name $AlertHandlingWebAppName

$addressTemplate = .\Deployment\Get-FunctionUri.ps1  `
    -WebApp $webApp `
    -FunctionName $AlertHandlingFunction

$address = $addressTemplate -Replace '{channel}', $AlertSlackChannel

.\Deployment\Set-ActionGroup.ps1 `
    -AlertUrl $address `
    -ActionGroupResourceGroup $MonitoredWebAppResourceGroup `
    -ActionGroupName 'repo-alerts'

.\Deployment\Add-Alerts.ps1 `
    -ResourceGroup $MonitoredWebAppResourceGroupe `
    -AlertTargetResourceGroup $MonitoredWebAppResourceGroup `
    -AlertTargetGroupName 'repo-alerts'