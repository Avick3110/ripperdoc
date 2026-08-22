# Findings

**Class: LIVING.** Updated in the same commit as any change to the rules below.

Measured behaviour of Cyberpunk 2077 and its modding frameworks. One file per
finding, named `YYYY-MM-DD-kebab-slug.md` by the date the measurement
completed.

These are published because they are useful whether or not ripperdoc ever
ships. Several of them contradict advice that circulates widely in the
community, and a couple describe behaviour that is documented nowhere at all.

## What a finding here owes you

Every document in this directory states:

- **The law** — what was actually measured, as a rule you can apply.
- **How it was measured** — the instruments, the readings, and enough detail to
  repeat it.
- **What would have refuted it** — for something measured from a run, the
  competing explanations and the specific observation that killed each one; for
  something read out of a source or a binary rather than run, the observation
  that *would* settle it, and a plain statement that it has not been made yet.
  A finding that never had a way to come out differently is not a finding.
- **Its evidence class** — measured, read, or inferred. Not every document here
  rests on a run, and one that does not says so where it says it, rather than
  borrowing the authority of the ones that do.
- **Where it stops** — the limits, honestly. An untested case is labelled
  untested, not quietly folded into the claim.

**Predictions were fixed before execution**, in a written design, including the
stop rules that would have invalidated a run. That is what separates these from
"we tried it and it seemed to work".

## What you will not find here

**No numbers from anyone's install.** These measurements were taken partly on a
real modded setup, and its contents, size and layout are not published. Where a
figure depends on the shape of a particular install, it is either omitted or
labelled as coming from a single sample — never generalised into a claim about
what installs are like.

**No claims dressed as measurements.** Where something is inferred rather than
observed, the document says *inferred*. Where a rule is assumed pending a
measurement, it says *assumed*.

## Corrections

These are ARCHIVE documents: they record what was measured on a date, and they
are not edited afterwards. A correction supersedes in a new document, which says
what it supersedes and why.

**What has been superseded, and by what.** A superseded document cannot carry a
pointer forward — it was written before its successor existed and editing it is
exactly what the class forbids — so the forward pointers live here, where they
can be added in the same commit as the correction. Anyone arriving at one of the
documents on the left is reading a law this project has since corrected.

| Superseded | Superseded by | What changed |
|---|---|---|
| [2026-08-19 tweak file order](2026-08-19-tweak-file-order.md) | [2026-08-22 tweak file order: three groups](2026-08-22-tweak-file-order-groups.md) | The read order has three groups decided by the first character of each file's own name; the original instrument populated only the middle one |

**A document with no row here has not been superseded.** An empty column is not
the same as an unchecked one.

**If you have measured something here differently, that is the most valuable
report this project can receive.** Open a `[Docs]` issue with what you measured
and how.
