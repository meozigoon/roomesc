param([string]$OutputPath = 'artifacts/audit-20261005/gameplay-online.json')
$ErrorActionPreference = 'Stop'
$qaConfig = Get-Content -Raw (Join-Path $PSScriptRoot '../src/ThirteenthBell/Assets/online-config.json') | ConvertFrom-Json
$qaHeaders = @{ apikey = $qaConfig.publishableKey }
$qaPrefix = 'qa' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$qaClaims = @()
$qaChecks = 0
function Send-StatisticsRequest($Payload) {
    return Invoke-RestMethod -Uri $qaConfig.functionUrl -Method Post -Headers $qaHeaders -ContentType 'application/json' -Body ($Payload | ConvertTo-Json -Compress) -TimeoutSec 20
}
function Assert-Statistics($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:qaChecks++
}
try {
    foreach ($qaSuffix in @('High', 'Low', 'Fail')) {
        $qaHasher = [Security.Cryptography.SHA256]::Create()
        try {
            $qaHash = [BitConverter]::ToString($qaHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString()))).Replace('-', '').ToLowerInvariant()
        } finally { $qaHasher.Dispose() }
        $qaClaims += $qaHash
        $qaReserved = Send-StatisticsRequest @{ action = 'reserve'; nickname = $qaPrefix + $qaSuffix; claimTokenHash = $qaHash }
        Assert-Statistics ($qaReserved.result -eq 'reserved') ('reserve ' + $qaSuffix)
    }
    $qaHigh = Send-StatisticsRequest @{ action = 'submit'; claimTokenHash = $qaClaims[0]; ending = 'deliver_gift'; elapsedMilliseconds = 1000; failedAttempts = 3; hintCount = 2 }
    Assert-Statistics ($qaHigh.result -eq 'saved' -and $qaHigh.clearTimeMs -eq 1000) 'active time stored exactly'
    $qaLow = Send-StatisticsRequest @{ action = 'submit'; claimTokenHash = $qaClaims[1]; ending = 'deliver_gift'; elapsedMilliseconds = 1000; failedAttempts = 1; hintCount = 1 }
    Assert-Statistics ($qaLow.result -eq 'saved' -and $qaLow.clearTimeMs -eq 1000) 'equal-time lower sum saved'
    $qaRetry = Send-StatisticsRequest @{ action = 'submit'; claimTokenHash = $qaClaims[0]; ending = 'feed_clock'; elapsedMilliseconds = 1; failedAttempts = 0; hintCount = 0 }
    Assert-Statistics ($qaRetry.clearTimeMs -eq 1000 -and $qaRetry.rank -eq ($qaLow.rank + 1)) 'completion retry immutable and lower sum wins'
    $qaFailed = Send-StatisticsRequest @{ action = 'fail'; claimTokenHash = $qaClaims[2] }
    Assert-Statistics ($qaFailed.result -eq 'saved' -and $qaFailed.failed) 'failed result preserved'
    $qaList = Invoke-RestMethod -Uri $qaConfig.functionUrl -Headers $qaHeaders -TimeoutSec 20
    $qaFound = @($qaList.entries | Where-Object nickname -Like ($qaPrefix + '*'))
    Assert-Statistics ($qaFound.Count -eq 3 -and $qaFound[0].nickname -eq ($qaPrefix + 'Low') -and $qaFound[1].nickname -eq ($qaPrefix + 'High') -and $qaFound[2].failed) 'public top list uses time and penalty then failure'
    Assert-Statistics ($qaFailed.rank -eq $qaList.totalCount) 'failure remains last rank'
    $qaRestUri = ($qaConfig.functionUrl -split '/functions/')[0] + '/rest/v1/thirteenth_bell_leaderboard?select=nickname,clear_time_ms,failed,failed_attempts,hint_count,ranking_penalty&completed_at=not.is.null&order=failed.asc,clear_time_ms.asc.nullslast,ranking_penalty.asc,completed_at.asc,nickname.asc'
    $qaPublic = Invoke-RestMethod -Uri $qaRestUri -Headers $qaHeaders -TimeoutSec 20
    $qaHighRow = @($qaPublic | Where-Object nickname -eq ($qaPrefix + 'High'))[0]
    Assert-Statistics ($qaHighRow.failed_attempts -eq 3 -and $qaHighRow.hint_count -eq 2 -and $qaHighRow.ranking_penalty -eq 5) 'public statistics unchanged on retry'
    $qaOrder = @($qaPublic | Where-Object nickname -Like ($qaPrefix + '*'))
    Assert-Statistics ($qaOrder[0].nickname -eq ($qaPrefix + 'Low') -and $qaOrder[1].nickname -eq ($qaPrefix + 'High')) 'full list uses same penalty order'
    $qaResult = @{ passed = $true; checks = $qaChecks; fixturePrefix = $qaPrefix; fixtureClaimHashes = $qaClaims; cleanupRequired = $true; serverVersion = 5 }
} catch {
    $qaResult = @{ passed = $false; checks = $qaChecks; fixturePrefix = $qaPrefix; fixtureClaimHashes = $qaClaims; cleanupRequired = $true; error = $_.Exception.Message }
    throw
} finally {
    $qaResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}
$qaResult | ConvertTo-Json -Compress
