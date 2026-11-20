# Build the MariaDB providers, run their unit tests, then run the validation probes against the local MariaDB server.
[CmdletBinding()]
param(
    # A disposable database the probes may create and drop tables in.
    [string]$ProbeDatabase = 'umbracomariadblab',
    [string]$ProbeUser = 'umbraco_lab',
    # Optional: further databases to check read-only, such as a restored copy of the live site.
    [string[]]$PreflightDatabase = @()
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$probe = Join-Path $root 'tools\MariaDbValidation'
$results = [ordered]@{}

function Invoke-Step([string]$Name, [scriptblock]$Command, [switch]$WarnOnly) {
    Write-Host "`n== $Name" -ForegroundColor Cyan
    & $Command
    $ok = $LASTEXITCODE -eq 0
    $results[$Name] = if ($ok) { 'PASS' } elseif ($WarnOnly) { 'CHECK' } else { 'FAIL' }
    if (-not $ok -and -not $WarnOnly) { throw "$Name failed with exit code $LASTEXITCODE." }
}

$variables = 'MARIADB_PROBE_PASSWORD', 'MARIADB_PROBE_USER', 'MARIADB_PROBE_DATABASE'
try {
    Invoke-Step 'Build MariaDB providers and probe' { dotnet build $probe -m:1 }
    Invoke-Step 'MariaDB unit tests' {
        dotnet test (Join-Path $root 'tests\Umbraco.Tests.UnitTests') --filter 'FullyQualifiedName~Umbraco.Cms.Persistence.MariaDb'
    }

    $password = Read-Host "MariaDB password for $ProbeUser" -AsSecureString
    $env:MARIADB_PROBE_PASSWORD = [System.Net.NetworkCredential]::new('', $password).Password
    $env:MARIADB_PROBE_USER = $ProbeUser
    $env:MARIADB_PROBE_DATABASE = $ProbeDatabase

    Invoke-Step 'Locking, transactions and bulk insert' { dotnet run --no-build --project $probe }
    Invoke-Step 'OpenIddict cleanup' { dotnet run --no-build --project $probe -- --cleanup }
    Invoke-Step "Preflight $ProbeDatabase" { dotnet run --no-build --project $probe -- --preflight } -WarnOnly

    foreach ($database in $PreflightDatabase) {
        $env:MARIADB_PROBE_DATABASE = $database
        Invoke-Step "Preflight $database" { dotnet run --no-build --project $probe -- --preflight } -WarnOnly
    }
}
finally {
    foreach ($name in $variables) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
    if ($password) { $password.Dispose() }
    Write-Host "`n== Summary" -ForegroundColor Cyan
    $results.GetEnumerator() | ForEach-Object { Write-Host ('{0,-6} {1}' -f $_.Value, $_.Key) }
}
