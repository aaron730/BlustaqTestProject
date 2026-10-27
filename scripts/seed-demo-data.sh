#!/usr/bin/env bash
# Seeds demo teams and notes through the running API.
#
# Usage:
#   ./scripts/seed-demo-data.sh                    # against http://localhost:5012
#   BASE_URL=http://localhost:8080 ./scripts/seed-demo-data.sh
#
# Requires only bash and curl. Every run creates new teams and notes, so running it
# twice produces duplicates; delete src/NoteService.Api/notes.db (with the app stopped) to start fresh.

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5012}"

# Sends a request as the given user and prints the response body; exits on any non-2xx status.
#   api <user> <method> <path> [json-body] [extra curl args...]
api() {
  local user=$1 method=$2 path=$3 body=${4:-}
  shift 4 2>/dev/null || shift $#

  local args=(-sS -X "$method" "$BASE_URL$path" -H "X-User-Id: $user" -w $'\n%{http_code}')
  if [[ -n "$body" ]]; then
    args+=(-H 'Content-Type: application/json' -d "$body")
  fi

  local response status
  response=$(curl "${args[@]}" "$@")
  status=${response##*$'\n'}
  response=${response%$'\n'*}

  if [[ "$status" != 2* ]]; then
    echo "✗ $method $path as $user failed with HTTP $status: $response" >&2
    exit 1
  fi

  printf '%s' "$response"
}

# Prints the leading numeric "id" field of a JSON response.
id_of() {
  sed -n 's/^{"id":\([0-9]*\),.*/\1/p'
}

create_team() {
  local owner=$1 name=$2
  api "$owner" POST /api/teams "{\"name\":\"$name\"}" | id_of
}

add_member() {
  local owner=$1 team_id=$2 user=$3
  api "$owner" POST "/api/teams/$team_id/members" "{\"userId\":\"$user\"}" >/dev/null
}

# create_note <author> <team-id or ""> <title> <json-escaped content> <tags as JSON array>
create_note() {
  local author=$1 team_id=$2 title=$3 content=$4 tags=$5
  local team_json=null
  [[ -n "$team_id" ]] && team_json=$team_id

  api "$author" POST /api/notes \
    "{\"title\":\"$title\",\"content\":\"$content\",\"teamId\":$team_json,\"tags\":$tags}" | id_of
}

# update_note <user> <note-id> <version> <title> <json-escaped content> <tags as JSON array>
update_note() {
  local user=$1 note_id=$2 version=$3 title=$4 content=$5 tags=$6
  api "$user" PUT "/api/notes/$note_id" \
    "{\"title\":\"$title\",\"content\":\"$content\",\"tags\":$tags}" \
    -H "If-Match: \"$version\"" >/dev/null
}

if ! curl -sf -o /dev/null "$BASE_URL/health"; then
  echo "✗ The API is not reachable at $BASE_URL. Start it with: dotnet run --project src/NoteService.Api" >&2
  exit 1
fi

echo "Seeding demo data into $BASE_URL ..."

# Teams: alice owns Platform (bob, carol); dave owns Design (erin, alice).
platform=$(create_team alice "Platform")
add_member alice "$platform" bob
add_member alice "$platform" carol

design=$(create_team dave "Design")
add_member dave "$design" erin
add_member dave "$design" alice
echo "✓ Teams: Platform ($platform), Design ($design)"

# Platform team notes.
planning=$(create_note alice "$platform" "Sprint 14 planning" \
  "## Goals\n- Ship note sharing\n- Harden concurrency handling\n\n## Risks\n- SQLite file locking under load" \
  '["planning","sprint-14"]')
create_note bob "$platform" "Incident review: API latency spike" \
  "## Summary\nP95 latency rose to 2s for 15 minutes.\n\n## Root cause\nMissing index on Notes.TeamId.\n\n## Actions\n- [x] Add index\n- [ ] Add latency alert" \
  '["incident","postmortem"]' >/dev/null
create_note carol "$platform" "On-call runbook" \
  "1. Check the health endpoint\n2. Review recent deploys\n3. Escalate to the team owner if unresolved after 30 minutes" \
  '["runbook","on-call"]' >/dev/null
create_note bob "$platform" "Architecture decision: optimistic concurrency" \
  "We use a version column exposed as an ETag. Clients must send If-Match on PUT.\n\nRejected alternative: last write wins." \
  '["adr","architecture"]' >/dev/null

# bob edits alice's planning note, so it shows version 2 and a newer updatedAt.
update_note bob "$planning" 1 "Sprint 14 planning" \
  "## Goals\n- Ship note sharing\n- Harden concurrency handling\n- Write README examples\n\n## Risks\n- SQLite file locking under load" \
  '["planning","sprint-14"]'

# Design team notes.
create_note dave "$design" "Brand refresh moodboard" \
  "Palette ideas: deep navy, warm sand, signal orange.\n\nTypefaces to evaluate: Inter, IBM Plex Sans." \
  '["branding","ideas"]' >/dev/null
create_note erin "$design" "Usability test findings" \
  "## Top issues\n1. Tag filter is hard to discover\n2. No indication when a note was edited by someone else" \
  '["research","ux"]' >/dev/null
create_note alice "$design" "Design review agenda" \
  "- Review onboarding flow\n- Decide on empty-state illustrations" \
  '["planning","meeting"]' >/dev/null
echo "✓ Team notes: 4 in Platform (1 edited by bob), 3 in Design"

# Personal notes, visible only to their authors.
create_note alice "" "Personal todo" "- [ ] Renew passport\n- [ ] Book dentist" '["personal","todo"]' >/dev/null
create_note alice "" "Reading list" "- Designing Data-Intensive Applications\n- The Pragmatic Programmer" '["reading"]' >/dev/null
create_note bob "" "1:1 notes with alice" "Discussed growth goals and conference budget." '["1-1"]' >/dev/null
create_note erin "" "Interview prep" "Questions to ask about team process and design tooling." '["career"]' >/dev/null
echo "✓ Personal notes: 2 for alice, 1 each for bob and erin"

cat <<EOF

Done. Users to try in the X-User-Id header:
  alice  owner of Platform, member of Design (sees both teams + her personal notes)
  bob    member of Platform
  carol  member of Platform
  dave   owner of Design
  erin   member of Design
  mallory  not in any team (sees nothing)

Try:
  curl '$BASE_URL/api/notes?tag=planning' -H 'X-User-Id: alice'
  curl '$BASE_URL/api/notes?teamId=$platform&search=incident' -H 'X-User-Id: bob'
EOF
