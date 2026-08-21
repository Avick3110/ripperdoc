# One public repository, with the working corpus untracked inside it

**Class: ARCHIVE.** Decided 2026-08-21. Corrections supersede in a new record.

## Context

The project decided to build in the open. That created a problem it had not had
while everything was local: the working corpus — research notes, session
records, measurement logs, the decision log — was **committed to git**, and it
carries machine paths, usernames, and a full characterisation of a specific
person's game install.

Publishing the history publishes all of that. Not the current state — the
*history*, which cannot be edited after the fact without rewriting it.

Version control had also been doing real work for that corpus: entries in the
decision log cite commits, findings are dated by their commit, and the
append-only property of the record was enforced by git rather than by habit.
Giving that up is a genuine loss, not a formality.

## The question

How do a public code repository and a private working corpus coexist?

## Options

**(a) One public repository, corpus untracked.** Create a fresh public
repository; the corpus lives inside the working tree but is gitignored. The
corpus loses version control.

**(b) Split into two repositories.** A public one for code and public
documents; the existing repository continues privately as the corpus, keeping
its history. Cross-repository changes are no longer atomic and need a
hand-applied discipline to stay consistent.

**(c) Publish everything.** One repository, corpus included, after a scrub pass
— which could clean the current state but not the history.

## The call

**(a).** Aaron, 2026-08-21.

His answer was conditional, and the condition is the reasoning: *"the last time
I had two repos it got very messy, are you sure this time it will be solid?"* —
and if not, his vote was for the single-repository model. The honest answer to
that question was **no**.

## Reasoning

Option (b) was the standing recommendation before this exchange, and it lost on
evidence rather than on preference — which is worth recording, because the
recommendation had been argued for at length.

**What (b)'s integrity actually rested on** was a hand-applied rule: a change
that touches both repositories must update both in the same stroke. There is no
mechanism enforcing that. It is a discipline, and this project's own standing
position is that **disciplines decay** — which is why guards exist at all. The
analysis proposing (b) had even named its own falsifier: *two repositories prove
more friction than the history is worth*. The customer then testified, from
direct prior experience, that exactly that had happened to him before. A
falsifier the decision named in advance, satisfied by the person who would be
paying the cost, is about as clean a refutation as a design argument gets.

**(c) was never viable.** The corpus contains someone's machine layout and
install contents. Scrubbing the working tree does not scrub the history, and the
history is where the exposure is.

**A fresh repository was forced regardless of which option won.** The corpus is
in roughly a hundred commits of the existing history, so *that* history could
never be pushed anywhere public under any option. The only question was whether
the old repository continued privately or was retired.

**It is retired, read-only**, as the archive of the research phase. That
preserves the one thing that mattered about its history: every commit cited
anywhere in the record still resolves.

### The consequences, named rather than discovered later

- **From here, the anchoring substrate is issues and pull requests**, not
  corpus commits. That is a change in where the project's "why" is pinned, and
  it is the main thing being traded away.
- **Immutability for corpus documents becomes an honour-system rule.** With no
  commits, "ARCHIVE documents are never edited" is enforced by discipline
  alone. Accepted knowingly — it is the same state the reference implementation
  has run in, publicly, without incident.
- **Anything that must move in lockstep with code lives in the tracked tree.**
  The operating manual, the standards, the decision records, and the findings.
  A document that governs the code but sits in the untracked corpus cannot be
  updated in the same commit as the code, and same-commit is the only
  discipline that actually prevents documentation rot.
- **Two lanes become deliberate public deliverables**, rather than incidental
  leftovers: these decision records, and the measured findings. Both are
  authored scrubbed from the first draft — placeholder paths, no machine
  details — so that publication is a deliberate act rather than an accident of
  history.

## Would be wrong if

**A build engagement needs review-by-diff over a private document.** Reviewing
a long document by its diff is a genuinely better mode than re-reading it, and
it was used twice on this project's own corpus. Losing it costs real review
quality. Mitigated rather than solved: a document needing that rigour is either
promoted into the tracked tree as a decision record, or versioned ad hoc for
the engagement.

**Or if an untracked corpus inside a public repository leaks by mis-add.** The
ignore rule prevents staging, and it was put in place in the repository's very
first commit — before any corpus content existed in the tree, so there has never
been a moment when it *could* have been staged. The deliberate publication path
is covered by writing public-destined documents scrubbed from the start.

## Outcome

*Backfilled at the first point where the loss of corpus version control has
either bitten or proven immaterial.*
