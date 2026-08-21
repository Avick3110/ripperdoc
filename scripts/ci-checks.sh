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
if [ -n "$(printenv "$tweakdb_variable" || true)" ]; then
  run "shipped-database checks" dotnet test ripperdoc.sln --nologo -v minimal --filter "Tier=ShippedDatabase" -- RunConfiguration.TreatNoTestsAsError=true
else
  skip "shipped-database checks"     "needs the user's own installed game data - tier (ii), local only; set $tweakdb_variable to a shipped tweak database to run it"
fi

skip "RTTI-dump checks"        "needs a dump generated from the user's own install - tier (iii), local only"

echo ""
echo "================ gate summary ================"
for label in "${passed[@]}"; do echo "  PASS  $label"; done
for label in "${failed[@]}"; do echo "  FAIL  $label"; done

if [ "${#failed[@]}" -gt 0 ]; then
  echo ""
  echo "gate: RED (${#failed[@]} of $(( ${#passed[@]} + ${#failed[@]} )) checks failed)"
  exit 1
fi

echo ""
echo "gate: GREEN (${#passed[@]} checks)"
