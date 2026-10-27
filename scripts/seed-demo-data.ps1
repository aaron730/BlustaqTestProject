<#
.SYNOPSIS
    Seeds demo teams and notes through the running API. PowerShell counterpart of seed-demo-data.sh.

.DESCRIPTION
    Works with Windows PowerShell 5.1 and PowerShell 7+ (Windows, macOS, Linux).
    Every run creates new teams and notes, so running it twice produces duplicates;
    delete src/NoteService.Api/notes.db (with the app stopped) to start fresh.

.EXAMPLE
    ./scripts/seed-demo-data.ps1

.EXAMPLE
    ./scripts/seed-demo-data.ps1 -BaseUrl http://localhost:8080

.EXAMPLE
    # Windows, if script execution is disabled by policy:
    powershell -ExecutionPolicy Bypass -File scripts\seed-demo-data.ps1
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = $(if ($env:BASE_URL) { $env:BASE_URL } else { 'http://localhost:5012' })
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

# Sends a request as the given user and returns the parsed response; exits on any non-2xx status.
function Invoke-Api {
    param(
        [string]$User,
        [string]$Method,
        [string]$Path,
        [object]$Body,
        [hashtable]$Headers = @{}
    )

    $request = @{
        Method  = $Method
        Uri     = "$BaseUrl$Path"
        Headers = @{ 'X-User-Id' = $User } + $Headers
    }
    if ($null -ne $Body) {
        $json = ConvertTo-Json -InputObject $Body -Depth 5 -Compress
        $request.ContentType = 'application/json; charset=utf-8'
        $request.Body = [System.Text.Encoding]::UTF8.GetBytes($json)
    }

    try {
        Invoke-RestMethod @request
    }
    catch {
        $failure = $_
        $response = $failure.Exception.PSObject.Properties['Response']
        $status = if ($response -and $response.Value) { [int]$response.Value.StatusCode } else { 'no response' }
        $detail = if ($failure.ErrorDetails) { $failure.ErrorDetails.Message } else { $failure.Exception.Message }
        Write-Host "FAILED: $Method $Path as $User returned HTTP ${status}: $detail" -ForegroundColor Red
        exit 1
    }
}

function New-Team([string]$Owner, [string]$Name) {
    (Invoke-Api -User $Owner -Method Post -Path '/api/teams' -Body @{ name = $Name }).id
}

function Add-Member([string]$Owner, [int]$TeamId, [string]$User) {
    Invoke-Api -User $Owner -Method Post -Path "/api/teams/$TeamId/members" -Body @{ userId = $User } | Out-Null
}

# Pass $null as TeamId for a personal note.
function New-Note([string]$Author, $TeamId, [string]$Title, [string]$Content, [string[]]$Tags) {
    $body = @{ title = $Title; content = $Content; teamId = $TeamId; tags = $Tags }
    (Invoke-Api -User $Author -Method Post -Path '/api/notes' -Body $body).id
}

function Update-Note([string]$User, [int]$NoteId, [int]$Version, [string]$Title, [string]$Content, [string[]]$Tags) {
    $body = @{ title = $Title; content = $Content; tags = $Tags }
    Invoke-Api -User $User -Method Put -Path "/api/notes/$NoteId" -Body $body -Headers @{ 'If-Match' = "`"$Version`"" } |
        Out-Null
}

try {
    Invoke-RestMethod -Uri "$BaseUrl/health" | Out-Null
}
catch {
    Write-Host "FAILED: The API is not reachable at $BaseUrl. Start it with: dotnet run --project src/NoteService.Api" -ForegroundColor Red
    exit 1
}

Write-Host "Seeding demo data into $BaseUrl ..."

# Teams: alice owns Platform (bob, carol); dave owns Design (erin, alice).
$platform = New-Team alice 'Platform'
Add-Member alice $platform bob
Add-Member alice $platform carol

$design = New-Team dave 'Design'
Add-Member dave $design erin
Add-Member dave $design alice
Write-Host "OK  Teams: Platform ($platform), Design ($design)" -ForegroundColor Green

# Platform team notes.
$planning = New-Note alice $platform 'Sprint 14 planning' `
    "## Goals`n- Ship note sharing`n- Harden concurrency handling`n`n## Risks`n- SQLite file locking under load" `
    @('planning', 'sprint-14')
New-Note bob $platform 'Incident review: API latency spike' `
    "## Summary`nP95 latency rose to 2s for 15 minutes.`n`n## Root cause`nMissing index on Notes.TeamId.`n`n## Actions`n- [x] Add index`n- [ ] Add latency alert" `
    @('incident', 'postmortem') | Out-Null
New-Note carol $platform 'On-call runbook' `
    "1. Check the health endpoint`n2. Review recent deploys`n3. Escalate to the team owner if unresolved after 30 minutes" `
    @('runbook', 'on-call') | Out-Null
New-Note bob $platform 'Architecture decision: optimistic concurrency' `
    "We use a version column exposed as an ETag. Clients must send If-Match on PUT.`n`nRejected alternative: last write wins." `
    @('adr', 'architecture') | Out-Null

# bob edits alice's planning note, so it shows version 2 and a newer updatedAt.
Update-Note bob $planning 1 'Sprint 14 planning' `
    "## Goals`n- Ship note sharing`n- Harden concurrency handling`n- Write README examples`n`n## Risks`n- SQLite file locking under load" `
    @('planning', 'sprint-14')

# Design team notes.
New-Note dave $design 'Brand refresh moodboard' `
    "Palette ideas: deep navy, warm sand, signal orange.`n`nTypefaces to evaluate: Inter, IBM Plex Sans." `
    @('branding', 'ideas') | Out-Null
New-Note erin $design 'Usability test findings' `
    "## Top issues`n1. Tag filter is hard to discover`n2. No indication when a note was edited by someone else" `
    @('research', 'ux') | Out-Null
New-Note alice $design 'Design review agenda' `
    "- Review onboarding flow`n- Decide on empty-state illustrations" `
    @('planning', 'meeting') | Out-Null
Write-Host 'OK  Team notes: 4 in Platform (1 edited by bob), 3 in Design' -ForegroundColor Green

# Personal notes, visible only to their authors.
New-Note alice $null 'Personal todo' "- [ ] Renew passport`n- [ ] Book dentist" @('personal', 'todo') | Out-Null
New-Note alice $null 'Reading list' "- Designing Data-Intensive Applications`n- The Pragmatic Programmer" @('reading') | Out-Null
New-Note bob $null '1:1 notes with alice' 'Discussed growth goals and conference budget.' @('1-1') | Out-Null
New-Note erin $null 'Interview prep' 'Questions to ask about team process and design tooling.' @('career') | Out-Null
Write-Host 'OK  Personal notes: 2 for alice, 1 each for bob and erin' -ForegroundColor Green

Write-Host @"

Done. Users to try in the X-User-Id header:
  alice    owner of Platform, member of Design (sees both teams + her personal notes)
  bob      member of Platform
  carol    member of Platform
  dave     owner of Design
  erin     member of Design
  mallory  not in any team (sees nothing)

Try:
  Invoke-RestMethod '$BaseUrl/api/notes?tag=planning' -Headers @{ 'X-User-Id' = 'alice' }
  Invoke-RestMethod '$BaseUrl/api/notes?teamId=$platform&search=incident' -Headers @{ 'X-User-Id' = 'bob' }
"@
