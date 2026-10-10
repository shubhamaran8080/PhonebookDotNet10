<#
.SYNOPSIS
    Generates realistic dummy contacts in the PostgreSQL database.

.DESCRIPTION
    Runs the existing ASP.NET Core seed utility in a one-off container.
    It only INSERTS contacts - existing contacts and users are never
    updated or deleted. Phone numbers and emails are guaranteed unique
    (both against the database and within the generated batch).

.PARAMETER Count
    How many contacts to generate. Default: 1000.

.EXAMPLE
    .\scripts\seed-contacts.ps1 -Count 1000
.EXAMPLE
    .\scripts\seed-contacts.ps1 -Count 250
#>
param(
    [int]$Count = 1000
)

$ErrorActionPreference = 'Stop'

if ($Count -le 0) {
    throw "Count must be greater than 0."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

function Get-ContactCount {
    $raw = docker compose --project-directory $repoRoot --project-name phonebook-dotnet10 `
        exec -T db psql -U phonebook -d phonebook_dotnet10 -t -A -c "SELECT COUNT(*) FROM contacts;"
    return [int]($raw.Trim())
}

$before = Get-ContactCount
Write-Host "Contacts before: $before"

# Seed__Exit=true makes the container seed and then shut down instead of
# starting Kestrel, so the already-running backend is not affected.
docker compose --project-directory $repoRoot --project-name phonebook-dotnet10 `
    run --rm `
    -e "Seed__Count=$Count" `
    -e "Seed__Exit=true" `
    backend

if ($LASTEXITCODE -ne 0) {
    throw "Seed container exited with code $LASTEXITCODE"
}

$after = Get-ContactCount
Write-Host ""
Write-Host "Contacts after:  $after"
Write-Host "Inserted:        $($after - $before)"

if ($after - $before -ne $Count) {
    Write-Warning "Expected to insert $Count contacts, but the count grew by $($after - $before)."
}
