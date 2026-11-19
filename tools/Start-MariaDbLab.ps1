# Build and launch the development host against Owen's local clean-install database.
[CmdletBinding()]
param(
    [ValidatePattern('^umbracomariadbinstall[0-9]*$')]
    [string]$Database = 'umbracomariadbinstall'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\Umbraco.Web.UI\Umbraco.Web.UI.csproj'
$previousPath = $env:Path
try {
    if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue) -or
        -not (Get-Command node.exe -ErrorAction SilentlyContinue)) {
        $vsNode = Get-Item "$env:ProgramFiles\Microsoft Visual Studio\*\*\MSBuild\Microsoft\VisualStudio\NodeJs\npm.cmd" -ErrorAction SilentlyContinue |
            Where-Object { Test-Path (Join-Path $_.DirectoryName 'node.exe') } |
            Select-Object -First 1
        if (-not $vsNode) { throw 'Install Node.js 24 with npm, open a new PowerShell window, and rerun this launcher.' }
        $env:Path = "$($vsNode.DirectoryName);$previousPath"
    }
    dotnet build $project -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Umbraco build failed.' }
}
finally {
    $env:Path = $previousPath
}

$settings = @{
    ConnectionStrings__umbracoDbDSN_ProviderName = 'MySqlConnector'
    ASPNETCORE_ENVIRONMENT = 'Development'
    Umbraco__CMS__Unattended__InstallUnattended = 'true'
    Umbraco__CMS__Unattended__UnattendedUserName = 'Lab Administrator'
    Umbraco__CMS__Unattended__UnattendedUserEmail = (Read-Host 'Local backoffice administrator email')
}
$previous = @{}
try {
    $dbPassword = Read-Host 'MariaDB password for umbraco_lab' -AsSecureString
    $adminPassword = Read-Host 'Backoffice administrator password (10 or more characters)' -AsSecureString
    if ($adminPassword.Length -lt 10) { throw 'The backoffice password needs at least 10 characters.' }
    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    $builder.set_ConnectionString('Server=127.0.0.1;Port=3306;Database=umbracomariadbinstall;User ID=umbraco_lab;GuidFormat=Char36')
    $builder['Database'] = $Database
    $builder['Password'] = [System.Net.NetworkCredential]::new('', $dbPassword).Password
    $settings['ConnectionStrings__umbracoDbDSN'] = $builder.get_ConnectionString()
    $settings['Umbraco__CMS__Unattended__UnattendedUserPassword'] = [System.Net.NetworkCredential]::new('', $adminPassword).Password
    foreach ($key in $settings.Keys) {
        $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }

    dotnet run --project $project --no-build --no-launch-profile -- --urls http://127.0.0.1:5080
    if ($LASTEXITCODE -ne 0) { throw "Umbraco exited with code $LASTEXITCODE." }
}
finally {
    foreach ($key in $previous.Keys) {
        if ($null -eq $previous[$key]) {
            Remove-Item -LiteralPath "Env:$key" -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process')
        }
    }
    if ($dbPassword) { $dbPassword.Dispose() }
    if ($adminPassword) { $adminPassword.Dispose() }
}
