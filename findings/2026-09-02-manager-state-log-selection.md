# Which write-ahead logs a reader must account for, and why the manifest alone does not say

**Class: ARCHIVE.** Written 2026-09-02. This document **supersedes one law** of
[the format and selection addendum](2026-09-02-manager-state-format-and-selection.md)
of the same date — and only for write-ahead logs. That document is not edited;
corrections supersede in a new one.

**Evidence class: reasoned from the format, NOT measured.** Everything below
about what a writer does between opening a log and recording it is read from the
format's own recovery rule, not from a state observed in that condition.
Observing one requires the manager running and writing, which no instrument in
this engagement does. The reader's behaviour that follows from it **is** checked;
the condition that motivates it is not reproduced. Nothing here should be
restated as a measurement.

---

## What the addendum said, and where it stops

The addendum's §4 established, correctly, that a reader takes the files holding
state from the pointer and the manifest rather than from a directory listing:

> a table the manifest has dropped is still on disk until the manager deletes
> it, and reading one resurrects whatever it holds.

That reasoning is sound and it is about **tables**. The addendum applied the
same law to logs in the same breath, and for logs the hazard runs the other way.

## The format's own recovery rule

A database's recovery does not read the logs its manifest names. It reads
**every log in the directory numbered at or above** the number the manifest
records, and it does so deliberately: a writer that needs a new log **opens and
begins writing it before** the version edit naming it is written, because that
edit is written when the memtable flush completes. Between those two moments the
newest writes live in a file the manifest does not yet name.

So for tables the manifest is authoritative and a listing is unsound; for logs
the manifest is a lower bound and a listing is what the format itself uses.

## What this reader does, and what it does not

**It refuses; it does not read through.** A log present in the directory and
numbered above the newest log the manifest names makes the whole reading a named
refusal, saying that the state may have been left part-way through a flush and
that the newest writes would be in a file this reader would have passed over.

That is the conservative half of the correction. Reading such a log would be the
complete fix, and it is deliberately not done here: replaying it correctly means
ordering its entries against the tables under the same rules the writer used,
and this engagement has never seen a state in that condition to hold an
implementation against. A refusal says what is true — that the reading cannot be
whole — without guessing at what the file holds.

**Two arms, both checked.** A log numbered above the named one is refused. A
leftover log numbered below it is left unread and the reading is unaffected;
that check gives the leftover bytes that are not a log, so a reading that opened
it would refuse on the framing and the arm would fail.

## What this does not change

Everything the addendum says about **tables** stands unchanged, including the
reason a listing is unsound there. The modelled subset, the checksums, the
selection law for the active profile, and both joins are untouched.

## Where this stops

**No state was observed mid-flush.** The window is short and needs the manager
running and writing; this reader has never been shown a directory in that
condition. What is measured is only that the bench's own directory holds no log
above the one its manifest names, so the refusal does not fire there.

**The read-through is not written.** Until a state in this condition is
captured, an implementation that replayed an unnamed log would be asserting an
ordering nobody here has seen. That is filed rather than guessed, and the
measurement window that would settle it is the same one
[`BUILD_PLAN_v2.md`](../BUILD_PLAN_v2.md) §10 row 21 describes for a running
manager.
