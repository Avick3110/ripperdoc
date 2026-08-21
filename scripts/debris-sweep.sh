#!/usr/bin/env bash
# Debris sweep - runs over every tracked text file.
#
# Four classes of damage that are invisible on a rendered page and survive
# review because nobody is looking for them:
#
#   1. A dropped leading letter in the brand name.
#   2. A dropped leading letter in "bench".
#   3. Control bytes (anything below 0x20 that is not tab or newline).
#   4. Carriage returns.
#
# Nothing typo-shaped is written out anywhere in this file. Both patterns AND
# the samples the self-test feeds them are derived from the two words at run
# time, so the sweep covers itself instead of needing an exclusion - and an
# exclusion is exactly the hole a sweep like this gets quietly retired through.
#
# Both patterns are case-blind on the letter before the gap: the brand appears
# capitalised in code identifiers, and that is a convention, not a defect.
#
# The sweep reads each file's INDEX BLOB, not the working copy - the bytes that
# will actually land in a commit. Line endings are normalised on the way in
# (.gitattributes), so a working copy with CRLF is not a defect while a blob
# with CR is.
#
# The carriage-return check does NOT use grep. On a Windows shell, grep strips
# CR before matching, so the obvious `grep $'\r'` reports clean on a file full
# of them - a check that cannot fail, which is the failure this project refuses
# by name. `tr -d` compares bytes and does not care about line-ending policy.
#
#   --self-test   feed every pattern a string it must flag and one it must not,
#                 then exit. Run it before trusting a clean sweep.
#
# Exit 0 = clean. Exit 1 = findings, all of them listed.

set -uo pipefail
cd "$(dirname "$0")/.." || exit 2

brand="ripperdoc"
word="bench"

# "the word with its first letter missing", case-blind on the letter before
# the gap.
gap_pattern() {
  local head="${1:0:1}" rest="${1:1}" r1
  r1="${rest:0:1}"
  printf '(?<![%s%s])[%s%s]%s' "$head" "${head^^}" "$r1" "${r1^^}" "${rest:1}"
}

pat_brand="$(gap_pattern "$brand")"
pat_word="$(gap_pattern "$word")-"
pat_control='[\x00-\x08\x0b\x0c\x0e-\x1f]'

has_cr() { ! tr -d '\r' < "$1" | cmp -s - "$1"; }

if [ "${1:-}" = "--self-test" ]; then
  fail=0
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' EXIT

  brand_gap="${brand:1}"
  word_gap="${word:1}"

  expect() { # label pattern want_hit sample
    if printf '%s' "$4" | grep -qP -- "$2"; then hit=1; else hit=0; fi
    if [ "$hit" != "$3" ]; then
      echo "SELF-TEST FAIL  $1 (wanted hit=$3, got $hit)"
      fail=1
    fi
  }
  expect "brand gap fires"        "$pat_brand"   1 "the ${brand_gap} engine"
  expect "brand gap fires capped" "$pat_brand"   1 "the ${brand_gap^} engine"
  expect "brand quiet"            "$pat_brand"   0 "the ${brand} engine"
  expect "brand quiet in code"    "$pat_brand"   0 "namespace ${brand^}.Core;"
  expect "word gap fires"         "$pat_word"    1 "a ${word_gap}-mark"
  expect "word quiet"             "$pat_word"    0 "a ${word}-mark"
  expect "word quiet capped"      "$pat_word"    0 "a ${word^}-mark"
  expect "control byte fires"     "$pat_control" 1 "$(printf 'a\002b')"
  expect "control byte quiet"     "$pat_control" 0 "plain text"

  printf 'line\r\n' > "$tmp/dirty"
  printf 'line\n'   > "$tmp/clean"
  has_cr "$tmp/dirty" || { echo "SELF-TEST FAIL  carriage return fires"; fail=1; }
  has_cr "$tmp/clean" && { echo "SELF-TEST FAIL  carriage return quiet"; fail=1; }

  [ "$fail" -eq 0 ] && echo "debris sweep self-test: every pattern fires"
  exit "$fail"
fi

status=0
report() { status=1; printf '%s\n' "$1"; }

blob="$(mktemp)"
trap 'rm -f "$blob"' EXIT

while IFS= read -r file; do
  git show ":$file" > "$blob" 2>/dev/null || continue
  # Skip anything git considers binary.
  grep -Iq . -- "$blob" 2>/dev/null || continue

  if hits=$(grep -nP -- "$pat_brand" "$blob" 2>/dev/null); then
    report "DEBRIS  truncated brand name  $file"
    printf '%s\n' "$hits" | sed 's/^/          /'
  fi

  if hits=$(grep -nP -- "$pat_word" "$blob" 2>/dev/null); then
    report "DEBRIS  truncated word        $file"
    printf '%s\n' "$hits" | sed 's/^/          /'
  fi

  if grep -qP -- "$pat_control" "$blob" 2>/dev/null; then
    report "DEBRIS  control bytes         $file"
  fi

  if has_cr "$blob"; then
    report "DEBRIS  carriage return       $file"
  fi
done < <(git ls-files)

[ "$status" -eq 0 ] && echo "debris sweep: clean"
exit "$status"
