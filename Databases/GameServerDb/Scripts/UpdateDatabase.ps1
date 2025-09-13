#!/usr/bin/env pwsh

# UpdateDatabase.ps1 - Entity Framework Database Update Script
# This script applies pending migrations to the database

param(
    [string]$DatabaseProvider,
    [string]$ConnectionString,
    [switch]$NoBuild,
    [switch]$Force,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
UpdateDatabase.ps1 - EF Core Database Update Helper

USAGE:
    .\UpdateDatabase.ps1 [-DatabaseProvider <provider>] [-ConnectionString <connstring>] [-NoBuild] [-Force]

PARAMETERS:
    -DatabaseProvider  Optional. Database provider (sqlite, sqlserver, postgresql, mysql). If not provided, will prompt.
    -ConnectionString  Optional. Database connection string. If not provided, will use defaults or prompt.
    -NoBuild           Optional. Skip building the project before running migrations (useful when run from Visual Studio).
    -Force             Optional. Skip confirmation prompts and apply migrations immediately.
    -Help              Show this help message.

EXAMPLES:
    .\UpdateDatabase.ps1
    .\UpdateDatabase.ps1 -DatabaseProvider "sqlite"
    .\UpdateDatabase.ps1 -DatabaseProvider "sqlserver" -ConnectionString "Server=localhost;Database=GameServer;Trusted_Connection=true;"
    .\UpdateDatabase.ps1 -NoBuild -Force
"@
    exit 0
}

# Function to validate database provider
function Get-DatabaseProvider {
    param([string]$Provider)
    
    $validProviders = @('sqlite', 'sqlserver', 'postgresql', 'mysql')
    
    if ($Provider -and $Provider.ToLower() -in $validProviders) {
        return $Provider.ToLower()
    }
    
    Write-Host "`nAvailable database providers:" -ForegroundColor Cyan
    Write-Host "1. sqlite      (Default - File-based database)" -ForegroundColor Green
    Write-Host "2. sqlserver   (Microsoft SQL Server)" -ForegroundColor Yellow
    Write-Host "3. postgresql  (PostgreSQL)" -ForegroundColor Blue
    Write-Host "4. mysql       (MySQL/MariaDB)" -ForegroundColor Magenta
    
    do {
        $choice = Read-HostSafe "`nSelect database provider [1-4] or type name directly (default: sqlite)"
        
        if ([string]::IsNullOrWhiteSpace($choice) -or $choice -eq "1") {
            return "sqlite"
        }
        
        switch ($choice.ToLower()) {
            "2" { return "sqlserver" }
            "3" { return "postgresql" }
            "4" { return "mysql" }
            "sqlite" { return "sqlite" }
            "sqlserver" { return "sqlserver" }
            "postgresql" { return "postgresql" }
            "mysql" { return "mysql" }
            default {
                Write-Host "Invalid choice. Please select 1-4 or type a valid provider name." -ForegroundColor Red
            }
        }
    } while ($true)
}

# Function to get connection string based on provider
function Get-ConnectionString {
    param(
        [string]$Provider,
        [string]$ProvidedConnectionString,
        [string]$OutputPath
    )
    
    if ($ProvidedConnectionString) {
        return $ProvidedConnectionString
    }
    
    # Default connection strings based on provider
    switch ($Provider.ToLower()) {
        "sqlite" {
            $defaultConn = "Data Source=$(Join-Path $OutputPath 'gameservers.db')"
            $useDefault = Read-HostSafe "Use default SQLite connection? ($defaultConn) [Y/n]"
            if ($useDefault.ToLower() -eq "n" -or $useDefault.ToLower() -eq "no") {
                return Read-HostSafe "Enter SQLite connection string"
            }
            return $defaultConn
        }
        "sqlserver" {
            $defaultConn = "Server=localhost;Database=GameServerDb;Trusted_Connection=true;"
            $useDefault = Read-HostSafe "Use default SQL Server connection? ($defaultConn) [Y/n]"
            if ($useDefault.ToLower() -eq "n" -or $useDefault.ToLower() -eq "no") {
                return Read-HostSafe "Enter SQL Server connection string"
            }
            return $defaultConn
        }
        "postgresql" {
            $defaultConn = "Host=localhost;Database=gameserverdb;Username=postgres;Password=postgres;"
            $useDefault = Read-HostSafe "Use default PostgreSQL connection? ($defaultConn) [Y/n]"
            if ($useDefault.ToLower() -eq "n" -or $useDefault.ToLower() -eq "no") {
                return Read-HostSafe "Enter PostgreSQL connection string"
            }
            return $defaultConn
        }
        "mysql" {
            $defaultConn = "Server=localhost;Database=gameserverdb;Uid=root;Pwd=root;"
            $useDefault = Read-HostSafe "Use default MySQL connection? ($defaultConn) [Y/n]"
            if ($useDefault.ToLower() -eq "n" -or $useDefault.ToLower() -eq "no") {
                return Read-HostSafe "Enter MySQL connection string"
            }
            return $defaultConn
        }
        default {
            return Read-HostSafe "Enter connection string for $Provider"
        }
    }
}

# Function to check migration status and return migration info
function Get-MigrationInfo {
    param(
        [string]$ProjectPath,
        [string]$ContextName,
        [switch]$ShowStatus = $true
    )
    
    if ($ShowStatus) {
        Write-Host "`n📋 Checking Migration Status..." -ForegroundColor Cyan
    }
    
    try {
        # Get all migrations with their status
        $listArgs = @("ef", "migrations", "list", "--prefix-output", "-p", $ProjectPath, "-c", $ContextName, "--connection", $env:CONN_STRING)
        if ($NoBuild) {
            $listArgs += "--no-build"
        }
        $migrations = & dotnet $listArgs 2>$null
        
        $pendingMigrations = @()
        $appliedMigrations = @()
        
        if ($LASTEXITCODE -eq 0 -and $migrations) {
            foreach ($migration in $migrations) {
                # Only process lines that start with "data:" prefix
                if ($migration -match '^data:\s*(.+)$') {
                    $migrationData = $matches[1].Trim()
                    
                    if ($migrationData) {
                        if ($migrationData -match '(.+)\s+\(Pending\)$') {
                            # Extract migration name without (Pending) suffix
                            $migrationName = $matches[1].Trim()
                            $pendingMigrations += $migrationName
                            
                            if ($ShowStatus) {
                                Write-Host "  ⏳ Pending: $migrationName" -ForegroundColor Yellow
                            }
                        } else {
                            # Applied migration
                            $appliedMigrations += $migrationData
                            
                            if ($ShowStatus) {
                                Write-Host "  ✅ Applied: $migrationData" -ForegroundColor Green
                            }
                        }
                    }
                }
            }

            if ($ShowStatus) {
                if ($pendingMigrations.Count -eq 0) {
                    Write-Host "✅ No pending migrations found - database is up to date!" -ForegroundColor Green
                } else {
                    Write-Host "`n📊 Migration Summary:" -ForegroundColor Cyan
                    Write-Host "  - Applied migrations: $($appliedMigrations.Count)" -ForegroundColor Green
                    Write-Host "  - Pending migrations: $($pendingMigrations.Count)" -ForegroundColor Yellow
                }
            }
        } else {
            if ($ShowStatus) {
                Write-Host "⚠️  No migrations found or unable to retrieve migration status." -ForegroundColor Yellow
            }
        }
        
        # Return a hashtable with all the information
        return @{
            HasPending = ($pendingMigrations.Count -gt 0)
            PendingMigrations = $pendingMigrations
            AppliedMigrations = $appliedMigrations
            TotalMigrations = $pendingMigrations.Count + $appliedMigrations.Count
        }
    } catch {
        if ($ShowStatus) {
            Write-Host "❌ Could not retrieve migration status: $($_.Exception.Message)" -ForegroundColor Red
        }
        return @{
            HasPending = $false
            PendingMigrations = @()
            AppliedMigrations = @()
            TotalMigrations = 0
        }
    }
}

# Function to apply pending migrations to database
function Invoke-DatabaseUpdate {
    param(
        [string]$ProjectPath,
        [string]$ContextName
    )
    
    Write-Host "`n🔄 Applying migrations to database..." -ForegroundColor Cyan
    
    # Prepare update command arguments
    $updateArgs = @("ef", "database", "update", "-p", $ProjectPath, "-c", $ContextName, "--connection", $env:CONN_STRING)
    if ($NoBuild) {
        $updateArgs += "--no-build"
    }
    
    Write-Host "Command: dotnet $($updateArgs -join ' ')" -ForegroundColor Gray
    
    try {
        & dotnet $updateArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`n✅ Database updated successfully!" -ForegroundColor Green
            return $true
        } else {
            Write-Host "`n❌ Database update failed!" -ForegroundColor Red
            Write-Host "Please check the error messages above." -ForegroundColor Yellow
            return $false
        }
    } catch {
        Write-Host "`n❌ Error executing database update: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# Helper function to clear input buffer and read user input safely
function Read-HostSafe {
    param(
        [string]$Prompt
    )
    
    # Clear any buffered input from the console
    while ([Console]::KeyAvailable) {
        [Console]::ReadKey($true) | Out-Null
    }
    
    # Now safely read the input
    return Read-Host $Prompt
}

# Main script execution
Write-Host "=== EF Core Database Update Helper ===" -ForegroundColor Cyan

$projectPath = "Databases\GameServerDb\"
Write-Host "Project Directory: $(Join-Path $(Get-Location) $projectPath)" -ForegroundColor DarkGray

# Get database provider
$selectedProvider = Get-DatabaseProvider -Provider $DatabaseProvider
Write-Host "Selected provider: $selectedProvider" -ForegroundColor Green

$contextName = "GameServerDb"
# relative to $projectPath
$outputPath = "MigrationHandler\$selectedProvider"

# Get connection string with output path
$connectionString = Get-ConnectionString -Provider $selectedProvider -ProvidedConnectionString $ConnectionString -OutputPath $outputPath
Write-Host "Connection string: $connectionString" -ForegroundColor Green

# Set environment variables for migration commands
$env:DB_PROVIDER = $selectedProvider
$env:CONN_STRING = $connectionString

# Check for pending migrations
$migrationInfo = Get-MigrationInfo -ProjectPath $projectPath -ContextName $contextName

if (-not $migrationInfo.HasPending) {
    Write-Host "`n🎉 Database is already up to date!" -ForegroundColor Green
    Write-Host "No migrations need to be applied." -ForegroundColor Cyan
    exit 0
}

# Display summary and confirm
Write-Host "`n=== Update Summary ===" -ForegroundColor Cyan
Write-Host "Database Provider: $selectedProvider" -ForegroundColor White
Write-Host "Connection String: $connectionString" -ForegroundColor White
Write-Host "Pending Migrations: $($migrationInfo.PendingMigrations.Count)" -ForegroundColor Yellow

if ($NoBuild) {
    Write-Host "Build: Skipped (--no-build)" -ForegroundColor Yellow
}

Write-Host "`n📋 Migrations to be applied:" -ForegroundColor Cyan
foreach ($migration in $migrationInfo.PendingMigrations) {
    Write-Host "  ⏳ $migration" -ForegroundColor Yellow
}

# Confirm before proceeding (unless Force is specified)
$shouldUpdate = $Force
if (-not $Force) {
    $confirm = Read-HostSafe "`nProceed with database update? [Y/n]"
    $shouldUpdate = ($confirm.ToLower() -ne "n" -and $confirm.ToLower() -ne "no")
}

if (-not $shouldUpdate) {
    Write-Host "Database update cancelled." -ForegroundColor Yellow
    exit 0
}

# Apply the migrations
$updateSuccess = Invoke-DatabaseUpdate -ProjectPath $projectPath -ContextName $contextName

if ($updateSuccess) {
    Write-Host "`n🎉 Database update completed successfully!" -ForegroundColor Green
    
    # Show final status
    Write-Host "`n📋 Final Migration Status:" -ForegroundColor Cyan
    $finalInfo = Get-MigrationInfo -ProjectPath $projectPath -ContextName $contextName -ShowStatus:$false
    Write-Host "  ✅ Total applied migrations: $($finalInfo.AppliedMigrations.Count)" -ForegroundColor Green
    Write-Host "  ⏳ Remaining pending migrations: $($finalInfo.PendingMigrations.Count)" -ForegroundColor Yellow
    
    exit 0
} else {
    Write-Host "`n❌ Database update failed!" -ForegroundColor Red
    Write-Host "`n📝 Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Check the error messages above" -ForegroundColor White
    Write-Host "  2. Verify your connection string and database availability" -ForegroundColor White
    Write-Host "  3. Try running with --no-build if building is causing issues" -ForegroundColor White
    Write-Host "  4. Check migration files for syntax errors" -ForegroundColor White
    
    exit 1
}