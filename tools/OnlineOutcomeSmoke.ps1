param([string]$OutputPath = 'artifacts/audit-20261005/온라인실패검증.json')
$ErrorActionPreference = 'Stop'
$qaConfig = Get-Content -Raw (Join-Path $PSScriptRoot '../src/ThirteenthBell/Assets/online-config.json') | ConvertFrom-Json
$qaHeaders = @{ apikey = $qaConfig.publishableKey }
$qaPrefix = 'qa' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$qaClaims = @()
$qaChecks = 0
function Send-OutcomeRequest($Payload) {
    return Invoke-RestMethod -Uri $qaConfig.functionUrl -Method Post -Headers $qaHeaders -ContentType 'application/json' -Body ($Payload | ConvertTo-Json -Compress) -TimeoutSec 20
}
function Assert-Outcome($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:qaChecks++
}
try {
    foreach ($qaSuffix in @('Clear', 'Fail')) {
        $qaHasher = [Security.Cryptography.SHA256]::Create()
        try {
            $qaHash = [BitConverter]::ToString($qaHasher.ComputeHash([Text.Encoding]::UTF8.GetBytes([Guid]::NewGuid().ToString()))).Replace('-', '').ToLowerInvariant()
        } finally { $qaHasher.Dispose() }
        $qaClaims += $qaHash
        $qaReserved = Send-OutcomeRequest @{ action = 'reserve'; nickname = $qaPrefix + $qaSuffix; claimTokenHash = $qaHash }
        Assert-Outcome ($qaReserved.result -eq 'reserved') ('reserve ' + $qaSuffix)
    }
    $qaClear = Send-OutcomeRequest @{ action = 'submit'; claimTokenHash = $qaClaims[0]; ending = 'deliver_gift' }
    Assert-Outcome ($qaClear.result -eq 'saved' -and $qaClear.clearTimeMs -gt 0) 'clear saved'
    $qaFailed = Send-OutcomeRequest @{ action = 'fail'; claimTokenHash = $qaClaims[1] }
    Assert-Outcome ($qaFailed.result -eq 'saved' -and $qaFailed.failed) 'failure saved'
    $qaRetry = Send-OutcomeRequest @{ action = 'fail'; claimTokenHash = $qaClaims[1] }
    Assert-Outcome ($qaRetry.rank -eq $qaFailed.rank) 'failure retry unchanged'
    $qaLateFailure = Send-OutcomeRequest @{ action = 'fail'; claimTokenHash = $qaClaims[0] }
    Assert-Outcome (-not $qaLateFailure.failed) 'successful clear protected'
    $qaList = Invoke-RestMethod -Uri $qaConfig.functionUrl -Headers $qaHeaders -TimeoutSec 20
    $qaFound = @($qaList.entries | Where-Object nickname -Like ($qaPrefix + '*'))
    Assert-Outcome ($qaFound.Count -eq 2 -and $qaFound[0].nickname -eq ($qaPrefix + 'Clear') -and $qaFound[1].failed -and $null -eq $qaFound[1].clear_time_ms) 'public ranking success then failure'
    Assert-Outcome ($qaFailed.rank -eq $qaList.totalCount) 'failed record is last rank'
    $qaRestUri = ($qaConfig.functionUrl -split '/functions/')[0] + '/rest/v1/thirteenth_bell_leaderboard?select=nickname,clear_time_ms,failed&completed_at=not.is.null&order=failed.asc,clear_time_ms.asc.nullslast,completed_at.asc,nickname.asc'
    $qaPublic = Invoke-RestMethod -Uri $qaRestUri -Headers $qaHeaders -TimeoutSec 20
    Assert-Outcome (@($qaPublic | Where-Object nickname -eq ($qaPrefix + 'Fail')).Count -eq 1) 'public RLS exposes failed result'
    $qaResult = @{ passed = $true; checks = $qaChecks; fixturePrefix = $qaPrefix; fixtureClaimHashes = $qaClaims; cleanupRequired = $true; serverVersion = 4 }
} catch {
    $qaResult = @{ passed = $false; checks = $qaChecks; fixturePrefix = $qaPrefix; fixtureClaimHashes = $qaClaims; cleanupRequired = $true; error = $_.Exception.Message }
    throw
} finally {
    $qaResult | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
}
$qaResult | ConvertTo-Json -Compress
