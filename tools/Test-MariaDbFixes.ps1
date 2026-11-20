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

# Building Umbraco runs npm (package schema and backoffice), so make sure Node is on PATH for this session.
function Find-NodeDirectory {
    $onPath = Get-Command npm.cmd -ErrorAction SilentlyContinue
    if ($onPath -and (Get-Command node.exe -ErrorAction SilentlyContinue)) { return $null }

    $candidates = @(Get-Item "$env:ProgramFiles\Microsoft Visual Studio\*\*\MSBuild\Microsoft\VisualStudio\NodeJs\npm.cmd" -ErrorAction SilentlyContinue)
    $runtime = Get-AppxPackage -Name '*CodexPrimaryRuntime*' -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1
    if ($runtime) { $candidates += @(Get-Item (Join-Path $runtime.InstallLocation 'dependencies\node\bin\npm.cmd') -ErrorAction SilentlyContinue) }

    $found = $candidates | Where-Object { Test-Path (Join-Path $_.DirectoryName 'node.exe') } | Select-Object -First 1
    if (-not $found) { throw 'npm was not found. Install Node.js 24 LTS from nodejs.org, open a new PowerShell window and rerun this script.' }
    return $found.DirectoryName
}

$previousPath = $env:Path
$nodeDirectory = Find-NodeDirectory
if ($nodeDirectory) { $env:Path = "$nodeDirectory;$previousPath" }
Write-Host "Using Node $(node --version) and npm $(npm --version)"

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
    $env:Path = $previousPath
    foreach ($name in $variables) { Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue }
    if ($password) { $password.Dispose() }
    Write-Host "`n== Summary" -ForegroundColor Cyan
    $results.GetEnumerator() | ForEach-Object { Write-Host ('{0,-6} {1}' -f $_.Value, $_.Key) }
}
