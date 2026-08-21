#!/usr/bin/env bash
# The gate. One command, one source of truth for what "green" means.
#
# CI runs exactly this script, and so can you:  bash scripts/ci-checks.sh
# Adding a check here adds it everywhere; adding one to the workflow file
# instead creates a second source of truth, which is how the local gate and
# the CI gate quietly stop agreeing.
#
# Two rules this script exists to keep:
#
#   1. EVERY CHECK RUNS, even after one fails. A red run should tell you
#      everything that is broken, not just the first thing.
#   2. A CHECK THAT CANNOT RUN SAYS SO. Checks needing the game, a generated
#      RTTI dump, or a real install cannot run on a bare runner - so they are
#      announced as SKIPPED, by name. An absent capability never reads as a
#      pass, because a skipped check reported as green is the same lie as a
#      wrong answer.
#
# The tier (ii) checks read a shipped tweak database, which is the game
# publisher's file and lives only on a machine with the game installed. They
# run when the environment names one and are announced as skipped when it does
# not. The variable name below is the one the tier (ii) fixture derives from
# the brand constant; if the two ever disagree the symptom is loud - the checks
# either run and fail to find a database, or are announced as skipped when they
# could have run.
tweakdb_variable="RIPPERDOC_TWEAKDB_PATH"

set -uo pipefail
cd "$(dirname "$0")/.." || exit 2

failed=()
passed=()
skipped=()

run() { # label command...
  local label="$1"; shift
  echo ""
  echo "=== $label ==="
  if "$@"; then
    passed+=("$label")
  else
    failed+=("$label")
    echo "--- FAILED: $label"
  fi
}

skip() { # label reason
  echo ""
  echo "=== $1 ==="
  echo "SKIPPED - $2"
  skipped+=("$1 - $2")
}

run "debris sweep self-test" bash scripts/debris-sweep.sh --self-test
run "debris sweep"           bash scripts/debris-sweep.sh

# Cleaned before building because part of what is under test is a schema this
# engine derives, and a build-then-run that serves a stale binary would be
# testing the previous answer while reporting on this one.
run "clean"                  dotnet clean ripperdoc.sln --nologo -v minimal
run "build"                  dotnet build ripperdoc.sln --nologo -v minimal
# A filter that matches nothing exits 0, so without the last flag a mistyped
# filter would print PASS having run no checks at all - the failure mode where
# verification machinery fails toward green.
run "tests"                  dotnet test  ripperdoc.sln --nologo -v minimal --filter "Tier!=ShippedDatabase" -- RunConfiguration.TreatNoTestsAsError=true

# Tiers (ii) and (iii): see tests/fixtures/README.md. Named here rather than
# left silent, so the gate's coverage is legible from its own output.
tweakdb_path="$(printenv "$tweakdb_variable" || true)"
measured_sha="$(tr -d '[:space:]' < tests/measured-database.sha256)"

if [ -z "$tweakdb_path" ]; then
  skip "shipped-database checks" "needs the user's own installed game data - tier (ii), local only; set $tweakdb_variable to a shipped tweak database to run it"
elif [ ! -f "$tweakdb_path" ]; then
  skip "shipped-database checks" "$tweakdb_variable names a path with no file at it - tier (ii) has nothing to read"
else
  # These checks reproduce counts measured against one game build. Any other
  # database is a different input rather than a defect in the engine, so the
  # tier is announced as unrunnable instead of run and failed. The gate decides
  # this, because deciding it inside the checks can only ever produce a red run.
  actual_sha="$(sha256sum "$tweakdb_path" | cut -d' ' -f1)"
  if [ "$actual_sha" = "$measured_sha" ]; then
    run "shipped-database checks" dotnet test ripperdoc.sln --nologo -v minimal --filter "Tier=ShippedDatabase" -- RunConfiguration.TreatNoTestsAsError=true
  else
    skip "shipped-database checks" "the database at $tweakdb_variable is sha256 $actual_sha, and these counts were measured against $measured_sha - a different game build, so they do not apply to it"
  fi
fi

skip "RTTI-dump checks"        "needs a dump generated from the user's own install - tier (iii), local only"

echo ""
echo "================ gate summary ================"
for label in "${passed[@]}"; do echo "  PASS  $label"; done
for label in "${failed[@]}"; do echo "  FAIL  $label"; done
# Skips belong in the summary, not only in the log above it. The summary is
# where a reader decides what a run actually covered, and a tier missing from it
# reads as a tier that passed.
for label in "${skipped[@]}"; do echo "  SKIP  $label"; done

if [ "${#failed[@]}" -gt 0 ]; then
  echo ""
  echo "gate: RED (${#failed[@]} of $(( ${#passed[@]} + ${#failed[@]} )) checks failed, ${#skipped[@]} skipped)"
  exit 1
fi

echo ""
echo "gate: GREEN (${#passed[@]} checks, ${#skipped[@]} skipped)"
