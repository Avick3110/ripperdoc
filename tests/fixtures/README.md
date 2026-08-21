# Fixtures

**Class: LIVING.** Updated in the same commit as the discipline it describes.

Empty on purpose. The rules exist before the fixtures do, because the first
fixture authored under no rule is the one that sets the precedent.

## The one hard rule

**Zero game-derived bytes in this repository, ever.** Not a trimmed record, not
a "small excerpt", not a single packed entry. The game's data files belong to
CDPR and are not ours to redistribute at any size.

Everything in here is therefore **synthetic**: constructed in memory by the
test that uses it, or checked in as a small hand-authored file whose every byte
was written by this project.

## Why that is possible at all

The pinned library can build TweakDB base states in memory without touching a
shipped database. That is what makes the largest part of the engine testable on
a bare runner - replay ordering, collision detection, provenance, contributor
chains, budget arithmetic - with nothing of CDPR's anywhere near it.

## The three tiers, and which one a check belongs to

| Tier | Needs | Runs |
|---|---|---|
| **(i) synthetic** | nothing but this repository | CI, and locally |
| **(ii) local, shipped database** | the user's own installed game data | the developer's machine only |
| **(iii) local, RTTI dump** | a dump generated from the user's own install | the developer's machine only |

A check that cannot run on the runner **self-skips and says so**. It never
passes quietly, because a skipped check reported as green is the same lie as a
wrong answer.

**How a tier is marked, and who acts on it.** A check above tier (i) carries an
xUnit trait naming its tier. The gate script owns the decision: it runs the
default set with the higher tiers filtered out, then either runs a higher tier
because the environment gave it what that tier needs, or announces that tier as
skipped by name in its own summary. Run such a check directly, outside the
gate, with nothing to run it against, and it fails - loudly, saying what it
wanted. There is no path on which it passes without having run.

Tier (ii) exists rather than folding into tier (i) for a measured reason: in the
pinned library version, a written TweakDB file cannot be read back by the same
library's reader. The binary parse therefore cannot be synthesised, and must be
exercised against a real shipped database or not at all. This blocks nothing -
the write lane never emits binary - but it does fix where that one check lives.

## Traceability

**A check that reproduces a measured number states the number, not the code's
current output.** Where a check asserts a count taken from a measurement, that
count is written into the check and a divergence is investigated as a defect.
Moving the expected number to whatever the code now produces turns a check into
a record of the bug.

**Every ordering rule the engine implements is traceable to a measurement.**
A fixture that encodes an ordering expectation names the finding it came from,
in the fixture, so a later reader can tell a measured law from someone's
recollection of one. Where a rule is assumed rather than measured, the fixture
says *assumed* and the assumption is written down somewhere it can be found
again.

The measurements this project has published are in [`findings/`](../../findings/).
