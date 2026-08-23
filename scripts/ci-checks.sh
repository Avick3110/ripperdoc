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

# Tier (iii) reads type information generated from the user's own game install.
# It is the input the dependency-drift audit needs, and no runner has one - the
# dump is derived from the publisher's binary and is no more this project's to
# carry than the database is. The audit's result travels instead, as a committed
# receipt carrying counts and fingerprints and nothing the game declares, and
# the tier (i) checks hold that receipt against the type model this build uses.
# So a runner with no dump still says something true about drift, and says only
# that.
rtti_dump_variable="RIPPERDOC_RTTI_DUMP_PATH"

# That tier decides one thing about itself this script cannot see from outside.
# Reflecting the pinned type model does not give the same answer in every
# process - a few properties come back with a foreign stored type, a different
# one each time - and which answer a process got is settled for that process's
# life. So a probe run from here would report on itself and not on the run that
# matters, and the run has to say which it did. It writes that into this file,
# beside its own binaries, and the block at the bottom reads it back and
# announces a skip by name. The spelling is the one DriftReceipt declares; a
# check holds the two together.
audit_status_name="drift-audit-status.txt"

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
run "tests"                  dotnet test  ripperdoc.sln --nologo -v minimal --filter "Tier!=ShippedDatabase&Tier!=InstalledTweakLayer&Tier!=RttiDump" -- RunConfiguration.TreatNoTestsAsError=true

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

# The tweak-layer tier's own subject is a directory rather than a fingerprintable
# file: its checks assert what holds of any layer and report the numbers instead
# of asserting them, which is what lets them run against an install whose mods
# change without turning that into a red run.
#
# It reads the shipped database as well, and unlike the tier above it does not
# pin which build that database is. What it asks of the database is whether a
# record is there - true or false of any build, rather than a count measured
# against one - so pinning it would refuse inputs these checks are perfectly
# good for.
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

rtti_dump_path="$(printenv "$rtti_dump_variable" || true)"

if [ -z "$rtti_dump_path" ]; then
  skip "RTTI-dump checks" "needs type information generated from the user's own install - tier (iii), local only; set $rtti_dump_variable to a dump's json directory to run it. The drift audit's accepted result is checked against this build's type model either way, by the tier (i) checks"
elif [ ! -d "$rtti_dump_path" ]; then
  skip "RTTI-dump checks" "$rtti_dump_variable names a path with no directory at it - tier (iii) has nothing to read"
elif [ ! -d "$rtti_dump_path/classes" ]; then
  skip "RTTI-dump checks" "$rtti_dump_variable names a directory with no classes/ in it, so it is not a dump's json output - tier (iii) has nothing to read"
elif [ -z "$tweakdb_path" ]; then
  skip "RTTI-dump checks" "needs the shipped database as well, to arbitrate the generated schema against real values - set $tweakdb_variable to one to run this tier. A schema checked against nothing is not a schema anything confirmed"
elif [ ! -f "$tweakdb_path" ]; then
  skip "RTTI-dump checks" "$tweakdb_variable names a path with no file at it, and this tier needs the database's values to arbitrate the generated schema"
elif [ "${actual_sha:-}" != "$measured_sha" ]; then
  skip "RTTI-dump checks" "the database at $tweakdb_variable could not be shown to be the build these counts were measured against, so the generated schema's coverage cannot be reproduced from it - see the shipped-database line above for which of the two it was"
else
  # Cleared first, so a file left behind by an earlier run cannot be read as
  # this one's report.
  rm -f tests/ripperdoc-core-tests/bin/*/net8.0/"$audit_status_name"

  run "RTTI-dump checks" dotnet test ripperdoc.sln --nologo -v minimal --filter "Tier=RttiDump" -- RunConfiguration.TreatNoTestsAsError=true

  audit_status_file="$(ls -1 tests/ripperdoc-core-tests/bin/*/net8.0/"$audit_status_name" 2>/dev/null | head -1)"
  audit_status=""
  if [ -n "$audit_status_file" ]; then audit_status="$(cat "$audit_status_file")"; fi

  if [ "$audit_status" = "ran" ]; then
    : # The comparison happened, and the tier's own result covers it.
  elif [ -n "$audit_status" ]; then
    skip "drift audit comparison" "$audit_status"
  elif printf '%s\n' "${failed[@]}" | grep -qxF "RTTI-dump checks"; then
    : # The tier is already red and never got far enough to report.
  else
    # The report is how a comparison that did not run is told from one that
    # passed. Missing it after a green tier, the summary would say the drift
    # audit was fine when nothing here knows whether it ran - so this is a red
    # and not a shrug.
    failed+=("drift audit comparison")
    echo "--- FAILED: drift audit comparison - the RTTI-dump checks passed without leaving $audit_status_name, so nothing here can say whether the comparison ran"
  fi
fi

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
