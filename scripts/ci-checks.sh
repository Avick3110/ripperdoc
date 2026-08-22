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

# The tier (ii) tweak-layer checks read a real install's tweak directory. That
# is a different input from the database above - a machine can have one and not
# the other - so it gets its own variable and its own skip, rather than being
# folded into a tier whose name would then be half true. The layer is other
# people's mod content and no more this project's to carry than the database is.
tweaks_variable="RIPPERDOC_TWEAKS_PATH"

# The same tier also reads the framework's own metadata, which declares the
# properties it adds to record types beyond the ones the type model has. Without
# it a property the game accepts reads as one the schema lacks - and the
# additions land on exactly the record types mods edit most, so the error is not
# small. The tier needs both inputs or it does not run.
extra_flats_variable="RIPPERDOC_EXTRA_FLATS_PATH"

# And its inheritance metadata, which records which shipped records were copied
# from which. A value written to a source record moves onto those copies, and
# without this the movement is invisible - so the tier reports the route as not
# walked rather than reporting that nothing moved. Deciding whether a copy still
# follows its source also needs the shipped database's own values, so this tier
# needs that too.
inheritance_variable="RIPPERDOC_INHERITANCE_PATH"

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
run "tests"                  dotnet test  ripperdoc.sln --nologo -v minimal --filter "Tier!=ShippedDatabase&Tier!=InstalledTweakLayer" -- RunConfiguration.TreatNoTestsAsError=true

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
  #
  # The hash tool is resolved rather than assumed. sha256sum is coreutils and a
  # stock macOS ships neither it nor a coreutils prefix on PATH, so on one of
  # the machines this gate is actually run from the command is simply absent -
  # and with the shell not set to stop on error, an absent command leaves the
  # fingerprint empty and the comparison below then announces the correct
  # database as a different game build. A skip given a false reason is worse
  # than a skip given none: it names something to investigate that is not there
  # and hides the thing that is. So each way the fingerprint can fail to be
  # taken gets the reason that actually applies.
  hasher=""
  if command -v sha256sum > /dev/null 2>&1; then hasher="sha256sum"; elif command -v shasum > /dev/null 2>&1; then hasher="shasum -a 256"; fi

  if [ -z "$hasher" ]; then
    skip "shipped-database checks" "no sha256 tool on this machine - neither sha256sum nor shasum is on PATH - so the database at $tweakdb_variable cannot be told apart from the build these counts were measured on"
  else
    actual_sha="$($hasher "$tweakdb_path" | cut -d' ' -f1)"
    if [ -z "$actual_sha" ]; then
      skip "shipped-database checks" "$hasher could not fingerprint the database at $tweakdb_variable, so it cannot be told apart from the build these counts were measured on"
    elif [ "$actual_sha" = "$measured_sha" ]; then
      run "shipped-database checks" dotnet test ripperdoc.sln --nologo -v minimal --filter "Tier=ShippedDatabase" -- RunConfiguration.TreatNoTestsAsError=true
    else
      skip "shipped-database checks" "the database at $tweakdb_variable is sha256 $actual_sha, and these counts were measured against $measured_sha - a different game build, so they do not apply to it"
    fi
  fi
fi

# The tweak-layer tier reads a directory rather than a fingerprintable file, so
# there is no build to tell apart here: the checks assert what holds of any
# layer and report the numbers instead of asserting them, which is what lets
# them run against an install whose mods change without turning that into a red
# run.
tweaks_path="$(printenv "$tweaks_variable" || true)"
extra_flats_path="$(printenv "$extra_flats_variable" || true)"
inheritance_path="$(printenv "$inheritance_variable" || true)"

if [ -z "$tweaks_path" ]; then
  skip "installed-tweak-layer checks" "needs a real install's tweak directory - tier (ii), local only; set $tweaks_variable to one to run it"
elif [ ! -d "$tweaks_path" ]; then
  skip "installed-tweak-layer checks" "$tweaks_variable names a path with no directory at it - tier (ii) has nothing to read"
elif [ -z "$extra_flats_path" ]; then
  skip "installed-tweak-layer checks" "needs the framework's own metadata as well as the layer - tier (ii), local only; set $extra_flats_variable to it to run this tier"
elif [ ! -f "$extra_flats_path" ]; then
  skip "installed-tweak-layer checks" "$extra_flats_variable names a path with no file at it - the property set cannot be resolved without the framework's metadata"
elif [ -z "$inheritance_path" ]; then
  skip "installed-tweak-layer checks" "needs the framework's inheritance metadata as well - tier (ii), local only; set $inheritance_variable to it to run this tier"
elif [ ! -f "$inheritance_path" ]; then
  skip "installed-tweak-layer checks" "$inheritance_variable names a path with no file at it - a value moving from a shipped record to its copies cannot be replayed without it"
elif [ -z "$tweakdb_path" ]; then
  skip "installed-tweak-layer checks" "needs the shipped database as well, to decide whether a copy still follows what it was copied from - set $tweakdb_variable to one to run this tier"
elif [ ! -f "$tweakdb_path" ]; then
  skip "installed-tweak-layer checks" "$tweakdb_variable names a path with no file at it, and this tier needs the database's values to decide whether a copy still follows what it was copied from"
else
  run "installed-tweak-layer checks" dotnet test ripperdoc.sln --nologo -v minimal --filter "Tier=InstalledTweakLayer" -- RunConfiguration.TreatNoTestsAsError=true
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
