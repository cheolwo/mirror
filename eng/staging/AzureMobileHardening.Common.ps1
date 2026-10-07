function Assert-RecoveryDatabaseTargets {
    param([string]$Source, [string]$Restore, [string]$Engine)
    if ($Source -notmatch '^[A-Za-z0-9_]+$' -or $Restore -notmatch '^[A-Za-z0-9_]+_restore_verify$' -or
        $Source.Length -gt 64 -or $Restore.Length -gt 64) {
        throw "$Engine recovery requires a valid source and an isolated *_restore_verify destination."
    }
    if ([string]::Equals($Source, $Restore, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Engine source and recovery destination must be different databases."
    }
}

function Get-StagingMigrationContextNames {
    @('SsalddelContext', 'TraditionalMarketDbContext', 'AgriculturalFisheriesDbContext', 'PublicDataIngestionDbContext')
}

function Assert-StagingMySqlMigrationTarget {
    param([string]$ConnectionString, [string]$Database, [string]$Server, [string]$Port, [string]$User)
    # Pomelo 9 uses __{databaseName}_EFMigrationsLock (20 fixed characters).
    # MySQL GET_LOCK names have a 64-character limit, independently of the
    # database identifier limit. Check before backups or any external command.
    if ($Database -notmatch '^[A-Za-z0-9_]+$' -or $Database.Length -gt 44) {
        throw 'Staging MySQL migration source database must be a valid identifier of at most 44 characters for the current provider lock.'
    }
    try {
        $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
        # PowerShell treats this type as IDictionary; property assignment would
        # insert a key named ConnectionString instead of calling the CLR setter.
        $builder.set_ConnectionString($ConnectionString)
        $values = @{}
        foreach ($key in $builder.get_Keys()) { $values[[string]$key] = [string]$builder[$key] }
        $connectionDatabase = @('Database', 'Initial Catalog') | Where-Object { $values.ContainsKey($_) } | Select-Object -First 1
        $connectionServer = @('Server', 'Host', 'Data Source', 'Address', 'Addr', 'Network Address') | Where-Object { $values.ContainsKey($_) } | Select-Object -First 1
        $connectionUser = @('User ID', 'User', 'UID', 'User Name', 'Username') | Where-Object { $values.ContainsKey($_) } | Select-Object -First 1
        $connectionPort = if ($values.ContainsKey('Port')) { [int]$values['Port'] } else { 3306 }
        if (-not $connectionDatabase -or -not $connectionServer -or -not $connectionUser -or
            -not [string]::Equals($values[$connectionDatabase], $Database, [StringComparison]::Ordinal) -or
            -not [string]::Equals($values[$connectionServer], $Server, [StringComparison]::OrdinalIgnoreCase) -or
            $connectionPort -ne [int]$Port -or $values[$connectionUser] -ne $User) { throw 'mismatch' }
    } catch {
        # Never echo the connection string, provider error, or password.
        throw 'Staging migration connection must match the backup source database, server, port and user.'
    }
}
