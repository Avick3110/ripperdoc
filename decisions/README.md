# Decisions

**Class: LIVING.** Updated in the same commit as any change to the format or
the rules below.

Why the project is shaped the way it is. One file per decision, named
`YYYY-MM-DD-kebab-slug.md` by the date the decision was made.

There is deliberately **no index here**. A list of decisions is a second home
for facts that already live in the files themselves, and second homes go stale
while looking authoritative. The directory listing is sorted by date because
the filenames start with one.

## The format

Each record carries, in this order:

| Field | What goes in it |
|---|---|
| **Context** | What was going on that made this need deciding |
| **The question** | Stated as a question, in one sentence |
| **Options** | Every option that was actually on the table, including the ones that lost |
| **The call** | What was decided, and by whom |
| **Reasoning** | Why — in plain sentences, not a scorecard |
| **Would be wrong if** | The falsifier: what would have to be true for this to have been the wrong call, and what it would cost to reverse |
| **Outcome** | Backfilled later, once the decision has played out |

Two of those fields are not in the usual architecture-decision-record
convention, and they are the two that make the format worth keeping.

**"Would be wrong if"** forces the decision to name its own falsifier at the
moment it is made, while nobody is invested in it yet. A decision that cannot
say what would falsify it has not been reasoned about; it has been preferred.

**"Outcome"** is backfilled once, later, in a commit that says so — the record
of whether the reasoning actually held. Everything above it is frozen from the
first commit. That is the only sanctioned edit to a record here.

## Two rules

1. **A record lands in the same commit as the change it governs, or it is not
   worth having.** Written afterwards, it is a justification exercise and it
   reads like one.

2. **A record is written scrubbed.** These are public from the first draft:
   relative or placeholder paths, no machine paths, no usernames, no contents
   of anyone's install.

## What is not here

The **evidence** a decision cites — measurement logs, probe designs, session
records — lives in the project's working corpus, which is not public. Where
that evidence has itself been published, the record links to
[`findings/`](../findings/) instead.

That split is not two homes for one fact: the public record is the home of the
*decision*, and the corpus is the home of the *evidence*. Different facts.
