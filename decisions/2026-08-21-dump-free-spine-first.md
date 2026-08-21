# The dump-free spine ships before the dump-bound work

**Class: ARCHIVE.** Decided 2026-08-21. Corrections supersede in a new record.

## Context

The schema-layer wave was planned as a single unit whose exit criterion was a
re-derivation of the earlier sketch's numbers **on a generated RTTI dump**.

A dependency analysis then found a seam running through the middle of that
wave. Some of its deliverables need a dump; some do not. And the seam runs
**exactly where the first vertical slice's real dependencies stop** — the slice
that proves the product works does not need the dump-bound half.

Separately, a product decision had landed: the **no-setup mode is the default
surface**. What a user gets without generating anything is the primary
experience, not a degraded fallback.

## The question

Does the first end-to-end proof wait for the dump-bound deliverables, or does
the dump-free spine ship first and the proof arrive earlier?

## Options

**(a) Keep the wave whole.** Build all seven deliverables, then prove. One
coherent unit, one exit criterion, no restructuring.

**(b) Split at the seam.** Ship the dump-free spine, take the proof earlier and
cheaper, then follow with the dump-bound trio — the typed reference graph, the
drift gate, and the first-run generation experience.

## The call

**(b), dump-free spine first.** Aaron, 2026-08-21 — *"if getting to proof
something works faster and cheaper... then that seems sensible"* — confirming
directional input he had given earlier the same day.

## Reasoning

**The proof is the thing worth pulling forward.** The first vertical slice is
where the project stops being a plausible plan and becomes a tool that produced
a real answer about a real install. Everything after that point is informed by
having done it once; everything before it is informed by expecting to. Moving
that boundary earlier improves every decision that comes after, and the
dependency analysis says it can move without anything being faked or stubbed.

**The default surface should be proven first.** Once the no-setup mode became
the primary experience rather than a fallback, sequencing the dump-bound work
ahead of it meant proving the *secondary* path first. That ordering was
inherited from a plan written before that product decision existed — it was
never argued for, and once the premise changed it stopped being defensible by
default.

**The split costs almost nothing structurally**, because the seam is a real
dependency boundary rather than a line drawn for convenience. Neither half has
to be built differently to accommodate it. This is the whole reason the seam
was worth acting on: a split along a natural boundary is bookkeeping, while a
split across one is technical debt.

**What the dump-bound trio loses by going second:** the drift gate arrives
later, so for a period the inherited type model is not being independently
validated against a real install's runtime information. That is a real gap and
it is named rather than glossed — the mitigation is that the spine's own
validation runs against shipped data, so the period is one of *reduced*
cross-checking rather than none.

## Would be wrong if

**A dump-bound deliverable turns out to be load-bearing for the proof after
all** — if the slice cannot honestly be demonstrated without, say, the typed
reference graph. The dependency analysis says otherwise, but the analysis was
done on the plan rather than on working code, and the plan is the thing least
likely to know where its own hidden coupling is. If it happens, the restructure
reverts: the trio moves back ahead of the proof, at the cost of the resequencing
work already done.

**Or if the delay to the drift gate lets an inherited-model divergence go
unnoticed and be built upon.** Priced as low, because the dependency is pinned
exactly and cannot move without someone changing a pin — but the risk is real
and it is the cost this ordering pays.

## Outcome

*Not yet backfilled — the wave has not run.*
