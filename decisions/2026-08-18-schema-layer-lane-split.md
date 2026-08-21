# The schema layer: generate one lane, inherit the other

**Class: ARCHIVE.** Decided 2026-08-18. Corrections supersede in a new record.

## Context

The project's first cornerstone is **coverage by construction**: the layer that
knows what every record type looks like is generated or inherited from a
machine-complete source, never hand-written type by type. The reason is not
elegance. A hand-maintained schema is a subset that *looks* complete, and the
day it silently stops covering something is the day the tool starts giving
confident wrong answers.

Cyberpunk 2077 has two data lanes that need such a layer, and they are not in
the same position:

- **CR2W resources** — the game's serialised resource format. There is a mature
  open-source type model for it already, maintained by the ecosystem's main
  toolchain and published as libraries.
- **TweakDB** — the game's tweak database. There is no equivalent type model to
  inherit. What exists is the game's own runtime type information, which can be
  dumped from an install, plus the shipped database itself.

## The question

Where does the schema layer come from for each lane — and is it the same answer
for both?

## Options

**(a) Hand-maintain a schema for the record types that matter.** Pragmatic,
immediately productive, and the thing the cornerstone exists to forbid.

**(b) Inherit everything from the existing toolchain.** Works for CR2W. Does
not work for TweakDB, because there is nothing there to inherit.

**(c) Split the lanes.** Generate the TweakDB schema mechanically from the RTTI
dump and validate it against the shipped database. Inherit the CR2W type model,
and use the dump as an independent by-construction validator of it, with a
dependency-drift gate in CI.

## The call

**(c), the lane split.** Aaron, 2026-08-18, after a throwaway generator sketch
demonstrated that the TweakDB half is actually mechanisable rather than
merely desirable.

## Reasoning

The split is not a compromise between (a) and (b) — it is the observation that
the two lanes have genuinely different best answers, and that forcing one
answer onto both would damage whichever lane lost.

**TweakDB has to be generated, and generation turned out to be small.** The
sketch derived record fields from the dump's getter shapes mechanically, with a
handful of normalisation rules and a single exception, and arbitrated the
result against the shipped database using the game's own identifier hash. What
made this a decision rather than a hope is that the sketch was **validated
against real shipped data** rather than against its own expectations: every
field it claimed was checked for whether real data agreed, and the ones it could
not confirm were **labelled unvalidated rather than assumed correct**. That
labelling is the first cornerstone and the third one meeting in the same
artifact.

**CR2W should not be re-derived.** A large, actively maintained type model
already exists, and rebuilding it would be duplicating someone else's
continuing work in order to own a copy that drifts. Inheriting it costs a
pinned dependency; owning it costs forever.

**The dump is what makes inheriting safe.** An inherited model with no
independent check is a trust exercise. The dump comes from the user's own game,
so it can validate the inherited model against the version actually installed —
and a drift gate in CI turns "we should check that some time" into something
that fails loudly on divergence. That is the difference between inheriting a
dependency and inheriting a liability.

**Why (a) is not available even as a starting point.** A subset ships sooner and
then sets the shape of everything built on it. The tempting version of this
argument always arrives as "just the common record types, for v1" — and that
sentence is precisely the cornerstone violation, which is why the operating
manual names it verbatim as a stop-and-escalate trigger rather than a
judgement call.

## Would be wrong if

**Users cannot reliably produce a dump.** The whole TweakDB lane assumes a dump
can be generated on the user's own machine from their own install. If that
proves fragile in practice, the generation path needs a first-run experience
good enough to carry non-technical users through it — or the lane degrades. This
is a known gate on the build plan rather than an unknown; the mitigation that
already exists is that most of the engine's capability is **dump-free**, so a
dump problem is a reduced product rather than no product.

**Or if the inherited CR2W model diverges faster than the drift gate can
absorb.** Then inheriting stops being cheaper than owning, and the calculation
flips. The gate exists precisely so that this would be *observed* rather than
discovered through wrong answers.

Reversal cost is asymmetric and that asymmetry is deliberate: dropping to a
hand-written subset later is always possible and always available, while
starting from a subset and trying to become complete is the failure mode this
whole project was shaped to avoid.

## Outcome

*Not yet backfilled — the schema layer is the next build wave.*
