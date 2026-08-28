# A reason a result could be wrong is declared once, with its own witness

**Class: ARCHIVE.** Decided 2026-08-28. Corrections supersede in a new record.

## Context

The script layer's reporting rested on three hand-maintained enumerations over
sets that are not closed — the reasons a result may be wrong, the words a
sentence may not use, and the places an annotation can appear without being
one. Two review rounds over one wave returned the same failure class eight
times: **the engine turns an absence or a failed read into a positive, named
claim.** The rounds were closed under `CLAUDE.md` §7's class rule and the
signal was filed rather than folded, because instances were arriving faster
than they were being closed.

`CLAUDE.md` cornerstone 1 requires coverage by construction. The schema layer
was not allowed to enumerate record types by hand; the reporting surface was
enumerating unknowns by hand.

## What the inventory found first

Before any design, every enum in `src/` was enumerated mechanically and each
classified against the script layer's mechanism. The result narrowed the
premise the engagement started from, and the narrowing is part of this
decision rather than a footnote to it.

**An honesty set**'s members name states in which the engine did not resolve or
could not read something; it is open by construction, because it grows every
time the engine meets an input it cannot read. **A domain set**'s members name
measured facts drawn from a set bounded outside us — a file format, a
contribution route, a decision rule that was measured. A closed hand-list is
only a defect over the first kind.

By that criterion the mechanism rhymes on **four enums clearly**
(`ScriptResolutionLimit`, `ArchiveFailureKind`, `UnaddressableReason`,
`ValidationState`), **three partly** (`GenerationState`, `WrappedCallReading`,
`FlatAddressing`), and **not at all on the remaining thirteen**. Putting a
domain set on this pattern would add a witness and a sentence to an enum with
no defect to answer.

One measurement decided the shape of the answer. The tier (i) guard policing
the emitted prose held **one of the five** limit sentences against its
vocabulary; the other four arose from no fixture it ran and from nothing on the
one real layer either. The guard's population was whatever a single synthetic
layer happened to produce.

## The question

What replaces a hand-maintained honesty enumeration, and how far does the
answer reach?

## Options

**(a) Grow the lists and check them harder.** Cheapest, and it is the move that
does not scale: the sets are open, so a longer list is a list that is wrong
later rather than one that is wrong now.

**(b) Adopt analysis machinery.** A Roslyn analyzer or source generator
policing that every hand-written production of a kind is present and correct.
This is the shape the reference implementation reached for, where its guard had
to parse a language it did not own.

**(c) Declare each kind once, with everything it needs, and derive the set from
the declarations.** The predicate, the sentence and a witness that provokes it
all become constructor arguments, so a kind that cannot be provoked cannot be
written down, and the set every result is built from is read back from the
declarations rather than assembled beside them.

## Decision

**(c), on the free and cheap tiers only — no new dependency.** Ruled by the
advisor lane on Aaron's gate, 2026-08-28.

The reason (b) is declined is not cost. **It would police a shape this change
deletes.** An analyzer checking that every `if` producing a kind is correct is
only needed while there are if-chains producing kinds, and there are none left.
The precedent that reached this project used analysis machinery because its
guard's subject was a language it did not own; here the subject is our own type
in our own assembly, and in-box reflection reads it exactly. **The two pins
carry over — a guard parses rather than pattern-matches, and its population is
derived from the artifact — while the machinery does not.**

What enforces what:

| Tier | Mechanism | Catches |
|---|---|---|
| Free | required constructor arguments | a kind with no test, no sentence or no witness — at build time |
| Free | no discard arm, so `TreatWarningsAsErrors` lets CS8509 bite | a kind a surviving switch does not answer for — at build time |
| Cheap | reflection over the declaring type | a kind that reaches no result — at check time |

The derivation refuses an empty reading rather than returning one, because
every question asked of a kind set is a completeness question and an empty set
answers all of them affirmatively. Two readings are kept — reflected, and what
the members recorded of themselves as they were built — and compared by
identity, so a member written in a shape reflection does not reach sits in one
and not the other.

**Deletion came before derivation on the prose.** The assembled description had
thirteen call sites and every one of them was a check; no production code called
it, and every fact it carried was already a member of the result. It reported a
method uncontested and, in the same sentence, that a gated annotation which
would contest it may in fact apply. It is deleted. What a limit means survives
as an invariant property of the limit, naming no mod, no method and no count.

**Two things the sentence carried had to survive it.** The order the annotation
lists are given in is the one thing about them a caller cannot recover from the
data, and the unmeasured reading — an execution nesting — is the one a reader
supplies unaided. So every annotation list states its order in its own name,
which a call site has to spell, rather than in prose a caller may never print.

## What this does not close, stated because it is real

- **The reader's model of the language does not close by derivation, and
  claiming otherwise would be an instance of the class this record ends.**
  Reflection enumerates the categories we declared; it cannot discover the one
  nobody thought of, which is exactly how string interpolation was missed. The
  set that would settle it is the compiler's own grammar. What stands in its
  place is a measurement against the compiler — [#45](https://github.com/Avick3110/ripperdoc/issues/45),
  open — and the direction the pass fails in, which is toward *unresolved* and
  never toward a live carrier taking a method.
- **A witness proves a kind is reachable through the engine, not that the engine
  produces it when it should.** That second thing is what the per-behaviour
  checks do, one per kind, and it is not derivable from anything.
- **Reflection over static fields is lost to trimming.** The same hazard
  `BUILD_PLAN_v2` §6 rule 6 names for the schema layer, with the same answer:
  publish with trimming off.
- **Which surfaces get the pattern is a scoping call, not a rule the code
  enforces.** A guard — no new plain enum in a result-carrying position — could
  exist and has **not** earned one under `CLAUDE.md` §5 #10: one incident, one
  layer. It stays a convention, and the criterion above is the convention.
  **Pre-stated promotion condition: a second incident of this class in another
  layer promotes the guard.**

## Consequences

- `ScriptResolutionLimit` stops being an `enum` and becomes a sealed class with
  static members of the same names. Reference equality keeps existing use
  working; a caller's `switch` over the type would not compile, and there are
  none.
- The limits a result carries are ordered by name. Nothing reads meaning from
  the sequence, and a reported order has to be the same on every reading.
- The wording guard's coverage goes from one declared sentence to all of them,
  by construction rather than by adding fixtures.
- The earlier layers are **not** migrated here. Their migrations are priced and
  filed, one issue per surface, on the maintenance milestone.
