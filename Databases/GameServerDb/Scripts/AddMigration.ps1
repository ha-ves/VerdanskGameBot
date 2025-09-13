#!/usr/bin/env pwsh

# AddMigration.ps1 - Automated Entity Framework Migration Script
# This script generates migration names using git commit info and runs EF migrations

param(
    [string]$MigrationName,
    [string]$DatabaseProvider,
    [string]$ConnectionString,
    [switch]$NoBuild,
    [switch]$ApplyToDatabase,
    [switch]$Help
)

if ($Help) {
    Write-Host @"
AddMigration.ps1 - EF Core Migration Helper

USAGE:
    .\AddMigration.ps1 [-MigrationName <name>] [-DatabaseProvider <provider>] [-ConnectionString <connstring>] [-NoBuild]

PARAMETERS:
    -MigrationName     Optional. Custom migration name. If not provided, will be generated from git info.
    -DatabaseProvider  Optional. Database provider (sqlite, sqlserver, postgresql, mysql). If not provided, will prompt.
    -ConnectionString  Optional. Database connection string. If not provided, will use defaults or prompt.
    -NoBuild           Optional. Skip building the project before running migrations (useful when run from Visual Studio).
    -Help              Show this help message.

EXAMPLES:
    .\AddMigration.ps1
    .\AddMigration.ps1 -MigrationName "AddUserTable" -DatabaseProvider "sqlite"
    .\AddMigration.ps1 -DatabaseProvider "sqlserver" -ConnectionString "Server=localhost;Database=GameServer;Trusted_Connection=true;"
    .\AddMigration.ps1 -NoBuild -MigrationName "QuickUpdate"
"@
    exit 0
}

# Function to get git commit information
function Get-GitCommitInfo {
    try {
        # Try to get current tag first
        $tag = git describe --exact-match --tags HEAD 2>$null
        if ($tag) {
            return "Tag_$($tag.Replace('.', '_').Replace('-', '_'))"
        }

        # If no tag, get short commit hash
        $commitHash = git rev-parse --short HEAD 2>$null
        if ($commitHash) {
            return "Commit_$commitHash"
        }

        # If git is not available or not in a repo
        return "Manual_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    }
    catch {
        return "Manual_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    }
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

# Combined function that shows migration status and returns migration info
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
        $listArgs = @("ef", "migrations", "list", "-p", $ProjectPath, "-c", $ContextName, "--connection", $env:CONN_STRING)
        if ($NoBuild) {
            $listArgs += "--no-build"
        }
        $migrations = & dotnet $listArgs 2>$null
        
        $pendingMigrations = @()
        $appliedMigrations = @()
        
        if ($LASTEXITCODE -eq 0 -and $migrations) {
            foreach ($migration in $migrations) {
                if ($migration.Trim()) {
                    if ($migration -match '\(Pending\)') {
                        $migrationName = $migration -replace '\s*\(Pending\)', ''
                        $pendingMigrations += $migrationName.Trim()
                        
                        if ($ShowStatus) {
                            Write-Host "  ⏳ Pending $($migrationName.Trim())" -ForegroundColor Yellow
                        }
                    } else {
                        $appliedMigrations += $migration.Trim()
                        
                        if ($ShowStatus -and $ShowOutput) {
                            Write-Host "  ✅ Applied $($migration.Trim())" -ForegroundColor Green
                        }
                    }
                }
            }

            if ($ShowStatus) {
                if ($pendingMigrations.Count -eq 0) {
                    Write-Host "No pending migrations." -ForegroundColor DarkGray
                }
            }
        } else {
            if ($ShowStatus) {
                Write-Host "No migrations found or unable to retrieve migration status." -ForegroundColor Gray
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
            Write-Host "Could not retrieve migration status: $($_.Exception.Message)" -ForegroundColor Yellow
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
    
    Write-Host "dotnet $($updateArgs -join ' ')" -ForegroundColor Gray
    
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

# Function to remove specific migrations
function Remove-Migrations {
    param(
        [string]$ProjectPath,
        [string]$ContextName,
        [string[]]$MigrationsToRemove
    )
    
    Write-Host "`n🗑️  Removing pending migrations..." -ForegroundColor Yellow
    
    foreach ($migrationName in $MigrationsToRemove) {
        Write-Host "  Removing migration: $migrationName" -ForegroundColor Gray
        
        $removeArgs = @("ef", "migrations", "remove", "-p", $ProjectPath, "-c", $ContextName)
        if ($NoBuild) {
            $removeArgs += "--no-build"
        }
        
        try {
            & dotnet $removeArgs
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  ✅ Removed: $migrationName" -ForegroundColor Green
            } else {
                Write-Host "  ❌ Failed to remove: $migrationName" -ForegroundColor Red
                return $false
            }
        } catch {
            Write-Host "  ❌ Error removing $migrationName`: $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    
    Write-Host "`n✅ All pending migrations removed successfully!" -ForegroundColor Green
    return $true
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
Write-Host "=== EF Core Migration Helper ===" -ForegroundColor Cyan

$projectPath = "Databases\GameServerDb\"
Write-Host "Project Directory: $(Join-Path $(Get-Location) $projectPath)" -ForegroundColor DarkGray

# Get or generate migration name
if ([string]::IsNullOrWhiteSpace($MigrationName)) {
    Write-Host "`nGenerating migration name from git information..." -ForegroundColor Yellow
    $gitInfo = Get-GitCommitInfo
    $customName = Read-HostSafe "Enter custom migration name (or press Enter to use: $gitInfo)"
    
    if ([string]::IsNullOrWhiteSpace($customName)) {
        $MigrationName = $gitInfo
    } else {
        $MigrationName = $customName
    }
}

Write-Host "Migration name: $MigrationName" -ForegroundColor Green

# Get database provider
$selectedProvider = Get-DatabaseProvider -Provider $DatabaseProvider
Write-Host "Selected provider: $selectedProvider" -ForegroundColor Green

$contextName = "GameServerDb"
# relative to $projectPath
$outputPath = "MigrationHandler\$selectedProvider"

# Get connection string with output path
$connectionString = Get-ConnectionString -Provider $selectedProvider -ProvidedConnectionString $ConnectionString -OutputPath $outputPath
Write-Host "Connection string: $connectionString" -ForegroundColor Green

# Set environment variables early for migration status check
$env:DB_PROVIDER = $selectedProvider
$env:CONN_STRING = $connectionString

# Confirm before proceeding
Write-Host "`n=== Migration Summary ===" -ForegroundColor Cyan
Write-Host "Migration Name: $MigrationName" -ForegroundColor White
Write-Host "Database Provider: $selectedProvider" -ForegroundColor White
Write-Host "Connection String: $connectionString" -ForegroundColor White
if ($NoBuild) {
    Write-Host "Build: Skipped (--no-build)" -ForegroundColor Yellow
}

# Check for pending migrations and handle user choice
$migrationInfo = Get-MigrationInfo -ProjectPath $projectPath -ContextName $contextName

$NoBuildPrePending = $NoBuild # Store original NoBuild flag

if ($migrationInfo.HasPending) {

    $pendingMigrations = $migrationInfo.PendingMigrations
    
    Write-Host "`n⚠️  PENDING MIGRATIONS DETECTED!" -ForegroundColor Red -BackgroundColor Yellow
    Write-Host "`n📋 You have the following options:" -ForegroundColor Cyan
    Write-Host "   [C]ontinue  - Create new migration on top of pending ones" -ForegroundColor Green
    Write-Host "   [R]eplace   - Remove pending migrations and create new one" -ForegroundColor Yellow
    Write-Host "   [A]pply     - Apply pending migrations first, then create new one" -ForegroundColor Blue
    Write-Host "   [N](Cancel) - Exit without creating migration" -ForegroundColor Red
    
    do {
        $choice = Read-HostSafe "`nSelect option [C/R/A/N] (default: C)"
        
        if ([string]::IsNullOrWhiteSpace($choice)) {
            $choice = "C"
        }
        
        switch ($choice.ToUpper()) {
            "C" {
                Write-Host "`n⚠️  Proceeding with migration creation on top of pending migrations..." -ForegroundColor Yellow
                Write-Host "   This may cause conflicts. Consider applying or replacing pending migrations." -ForegroundColor Yellow
                break
            }
            "R" {
                Write-Host "`n🗑️  Replacing pending migrations..." -ForegroundColor Yellow
                Write-Host "   This will permanently remove the following migrations:" -ForegroundColor Red
                foreach ($migration in $pendingMigrations) {
                    Write-Host "   - $migration" -ForegroundColor Red
                }
                
                $confirmReplace = Read-HostSafe "`n⚠️  Are you sure you want to PERMANENTLY DELETE these migrations? [y/N]"
                if ($confirmReplace.ToLower() -eq "y" -or $confirmReplace.ToLower() -eq "yes") {
                    $NoBuild = $false # Ensure build is done for removal

                    $removeSuccess = Remove-Migrations -ProjectPath $projectPath -ContextName $contextName -MigrationsToRemove $pendingMigrations
                    
                    if ($removeSuccess) {
                        Write-Host "`n✅ Pending migrations removed. Proceeding with new migration creation..." -ForegroundColor Green
                    } else {
                        Write-Host "`n❌ Failed to remove some migrations. Exiting..." -ForegroundColor Red
                        exit 1
                    }
                } else {
                    Write-Host "Migration replacement cancelled." -ForegroundColor Yellow
                    exit 0
                }
                break
            }
            "A" {
                Write-Host "`n🔄 Applying pending migrations first..." -ForegroundColor Cyan
                $applySuccess = Invoke-DatabaseUpdate -ProjectPath $projectPath -ContextName $contextName
                
                if ($applySuccess) {
                    Write-Host "`n✅ Pending migrations applied successfully!" -ForegroundColor Green
                    $continueConfirm = Read-HostSafe "Continue with creating new migration '$MigrationName'? [Y/n]"
                    if ($continueConfirm.ToLower() -eq "n" -or $continueConfirm.ToLower() -eq "no") {
                        Write-Host "Migration creation cancelled." -ForegroundColor Yellow
                        exit 0
                    }
                } else {
                    Write-Host "`n❌ Failed to apply pending migrations." -ForegroundColor Red
                    Write-Host "This may cause conflicts with the new migration, but you can still proceed." -ForegroundColor Yellow
                    
                    $continueAnyway = Read-HostSafe "Do you want to continue creating the new migration anyway? [y/N]"
                    if ($continueAnyway.ToLower() -ne "y" -and $continueAnyway.ToLower() -ne "yes") {
                        Write-Host "Migration creation cancelled." -ForegroundColor Yellow
                        exit 1
                    }
                }
                break
            }
            "N" {
                Write-Host "Migration cancelled." -ForegroundColor Yellow
                exit 0
            }
            default {
                Write-Host "Invalid choice. Please select C, R, A, or N." -ForegroundColor Red
                continue
            }
        }
        break
    } while ($true)
} else {
    $confirm = Read-HostSafe "`nProceed with migration creation? [Y/n]"
    if ($confirm.ToLower() -eq "n" -or $confirm.ToLower() -eq "no") {
        Write-Host "Migration cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Prepare command arguments
$efArgs = @("ef", "migrations", "add", $MigrationName, "-p", $projectPath, "-c", $contextName, "-o", $outputPath)
if ($NoBuild) {
    $efArgs += "--no-build"
}

Write-Host "`nExecuting migration command..." -ForegroundColor Yellow
Write-Host "dotnet $($efArgs -join ' ')" -ForegroundColor Gray

try {
    # Execute the migration command
    & dotnet $efArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n✅ Migration '$MigrationName' created successfully!" -ForegroundColor Green
        Write-Host "📁 Migration files location: $outputPath" -ForegroundColor Cyan
        
        # Show created files
        $migrationFiles = Get-ChildItem -Path $outputPath -Filter "*$MigrationName*" -File 2>$null
        if ($migrationFiles) {
            Write-Host "`n📄 Created files:" -ForegroundColor Cyan
            foreach ($file in $migrationFiles) {
                Write-Host "  - $($file.Name)" -ForegroundColor White
            }
        }
        
        $NoBuild = $NoBuildPrePending # Restore NoBuild flag

        # Determine if we should update the database
        $shouldUpdate = $false
        if ($ApplyToDatabase) {
            $shouldUpdate = $true
            Write-Host "`n🔄 Auto-applying migration to database (--ApplyToDatabase specified)..." -ForegroundColor Cyan
        } else {
            $updateChoice = Read-HostSafe "Would you like to apply this migration to the database now? [Y/n]"
            $shouldUpdate = ($updateChoice.ToLower() -ne "n" -and $updateChoice.ToLower() -ne "no")
        }

        if ($shouldUpdate) {
            $NoBuild = $false # Ensure build is done for applying

            $updateSuccess = Invoke-DatabaseUpdate -ProjectPath $projectPath -ContextName $contextName
            
            if ($updateSuccess) {
                Write-Host "`n🎉 Complete! Migration created and applied successfully." -ForegroundColor Green
            } else {
                Write-Host "`n⚠️  Migration was created but database update failed." -ForegroundColor Yellow
                Write-Host "You can manually run: dotnet ef database update" -ForegroundColor Cyan
            }
        } else {
            Write-Host "`n📝 Next steps:" -ForegroundColor Cyan
            Write-Host "  1. Review the generated migration files" -ForegroundColor White
            Write-Host "  2. Run 'dotnet ef database update' to apply the migration" -ForegroundColor White
            Write-Host "  3. Test your changes" -ForegroundColor White
        }
    } else {
        Write-Host "`n❌ Migration creation failed!" -ForegroundColor Red
        Write-Host "Please check the error messages above." -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "`n❌ Error executing migration command: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} finally {
    # Clean up environment variables if needed
    # $env:DB_PROVIDER = $null
    # $env:CONN_STRING = $null
}
