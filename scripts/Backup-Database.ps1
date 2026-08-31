[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $DatabaseName,
    [Parameter(Mandatory)] [string] $SqlServerBackupDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($DatabaseName -notmatch '^[A-Za-z0-9_]+$') { throw 'DatabaseName contains unsupported characters.' }
if ($SqlServerBackupDirectory.Contains("'")) { throw 'Backup directory contains unsupported characters.' }
$connectionString = [Environment]::GetEnvironmentVariable('PORTAL_MIGRATION_DATABASE_CONNECTION_STRING', 'Process')
if ([string]::IsNullOrWhiteSpace($connectionString)) { throw 'PORTAL_MIGRATION_DATABASE_CONNECTION_STRING is missing.' }

Import-Module SqlServer -ErrorAction Stop
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss')
$separator = if ($SqlServerBackupDirectory.EndsWith('\')) { '' } else { '\' }
$backupPath = "$SqlServerBackupDirectory$separator$DatabaseName`_$stamp.bak"
$escapedPath = $backupPath.Replace("'", "''")
$query = @"
BACKUP DATABASE [$DatabaseName]
TO DISK = N'$escapedPath'
WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT;
RESTORE VERIFYONLY FROM DISK = N'$escapedPath' WITH CHECKSUM;
"@

Invoke-Sqlcmd -ConnectionString $connectionString -Query $query -QueryTimeout 0 -AbortOnError
Write-Output "Verified SQL backup created: $backupPath"
