# Install the v17 CMS MCP locally and exercise one read-only tool.
[CmdletBinding()]
param([string]$TunnelClient, [switch]$EnableWrites)

$ErrorActionPreference = 'Stop'
if ($EnableWrites -and -not $TunnelClient) { throw '-EnableWrites requires -TunnelClient.' }
$npmCli = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\18\Community\MSBuild\Microsoft\VisualStudio\NodeJs\node_modules\npm\bin\npm-cli.js'
$runtime = Get-AppxPackage -Name '*CodexPrimaryRuntime*' -ErrorAction SilentlyContinue |
    Sort-Object Version -Descending | Select-Object -First 1
$candidates = @((Get-Command node.exe -ErrorAction SilentlyContinue).Source)
if ($runtime) { $candidates += Join-Path $runtime.InstallLocation 'dependencies\node\bin\node.exe' }
$candidates += 'C:\Program Files\WindowsApps\OpenAI.CodexPrimaryRuntime.v26-1112-611-0_26.1112.611.0_x64__3k8sg7r9htsxt\dependencies\node\bin\node.exe'
$node = $null
foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path -LiteralPath $candidate)) {
        $version = [version]((& $candidate --version).TrimStart('v'))
        if ($version.Major -eq 24 -and $version -ge [version]'24.22.1') {
            $node = $candidate
            break
        }
    }
}
if (-not $node) { throw 'Install a current Node.js 24 LTS release (24.22.1 or later), open a new PowerShell window and retry.' }
$folder = Join-Path $env:LOCALAPPDATA 'UmbracoMariaDbLab\mcp'
if ($TunnelClient) {
    $TunnelClient = (Resolve-Path -LiteralPath $TunnelClient).Path
    if (-not $env:CONTROL_PLANE_API_KEY -or -not $env:CONTROL_PLANE_TUNNEL_ID) {
        throw 'Run from the PowerShell window where the tunnel lookup succeeded.'
    }
}
else {
    Write-Host "Installing Umbraco CMS Developer MCP v17 with Node $version..."
    & $node $npmCli install --prefix $folder --no-fund --no-audit '@umbraco-cms/mcp-dev@17'
    if ($LASTEXITCODE -ne 0) { throw 'MCP package install failed.' }
}
$packageDir = Join-Path $folder 'node_modules\@umbraco-cms\mcp-dev'
$package = Get-Content -Raw (Join-Path $packageDir 'package.json') | ConvertFrom-Json
$bin = if ($package.bin -is [string]) { $package.bin } else { ($package.bin.PSObject.Properties | Select-Object -First 1).Value }
$entry = Join-Path $packageDir $bin
$env:UMBRACO_READONLY = if ($EnableWrites) { 'false' } else { 'true' }
Write-Host "Using Umbraco MCP $($package.version) with UMBRACO_READONLY=$env:UMBRACO_READONLY."
$env:UMBRACO_BASE_URL = 'http://127.0.0.1:5080'
$env:UMBRACO_CLIENT_ID = (Read-Host 'Full Umbraco API client ID').Trim()
$secret = Read-Host 'Umbraco API client secret' -AsSecureString
try {
    $env:UMBRACO_CLIENT_SECRET = [System.Net.NetworkCredential]::new('', $secret).Password
    if ($TunnelClient) {
        $previousPath = $env:Path
        Push-Location -LiteralPath (Split-Path $entry)
        try {
            $env:Path = "$(Split-Path $node);$previousPath"
            $command = 'command=node.exe ' + (Split-Path $entry -Leaf) + ',channel=main'
            & $TunnelClient run --mcp.command $command --health.listen-addr '127.0.0.1:8080'
            if ($LASTEXITCODE -ne 0) { throw "Tunnel client exited with code $LASTEXITCODE." }
        }
        finally {
            $env:Path = $previousPath
            Pop-Location
        }
        return
    }
    & $node $entry --umbraco-readonly --call get-document-root --call-args '{}'
    if ($LASTEXITCODE -ne 0) { throw 'The MCP content-root read failed.' }
}
finally {
    Remove-Item Env:UMBRACO_CLIENT_SECRET -ErrorAction SilentlyContinue
    $secret.Dispose()
}
