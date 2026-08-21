# RIPPERDOC_DOC_HYGIENE.md — document classes and the rules that keep them honest

**Class: LIVING.** Updated in the same commit as any change to the practice it
describes.

**This is a convention, not an enforced gate.** There is no hook and no ship
gate behind it. If document drift ever becomes a real, recurring problem here,
*that* is when enforcement earns its place — and not before. The rule that
governs this standard is the same one that governs every other guard in the
project: a guard is added after the incident recurs, not in anticipation of it.

Most document rot is two moves: **editing something that should have been
superseded**, and **failing to update something in the commit that made it
wrong**. Almost everything below is one of those two.

---

## 1. Two classes, and only two

Every document in this repository is **LIVING** or **ARCHIVE**. A document
that fits neither should not exist.

| | LIVING | ARCHIVE |
|---|---|---|
| What it is | A current description of how something is | A dated record of what was true, decided, or measured at a moment |
| When it changes | In the same commit as the thing it describes | Never |
| When it is wrong | The document is the bug — fix it | It is not wrong; it is *past*. Supersede it |

**LIVING — same-commit rule.** A LIVING document is updated in the commit that
changes what it describes. Not the next commit, not at the end of the branch.
If a LIVING document contradicts the code, **the document is the bug**, because
the whole point of the class is that it can be trusted without checking.

**ARCHIVE — immutable from its first commit.** Corrections go in a **new**
document that supersedes, never in an edit. A superseding document says what it
supersedes and why; the superseded one stays exactly as written, because the
record of what we believed at the time is the thing that makes the record worth
keeping.

**The one exception: `[ARCHIVE typo-fix]`.** A commit may fix a typo, a broken
link, or a formatting slip in an ARCHIVE document if it **adds and removes no
content**. The commit message says `[ARCHIVE typo-fix]` so the exception is
visible in the log rather than inferred from a diff. Anything that changes what
the document *claims* is not a typo fix, however small it looks.

## 2. Declare the class

**Every document whose class could be guessed wrong declares it in a header
line**, near the top:

```
**Class: LIVING.** Updated in the same commit as <the thing it describes>.
```

```
**Class: ARCHIVE.** <What this recorded, and when.> Superseded by <doc>, or
corrections supersede in a new document.
```

The marker is the load-bearing signal. A reader deciding whether they may edit
a file should get a mechanical answer from the file itself, not from a
judgement call about what kind of document it looks like.

## 3. The transition — LIVING becomes ARCHIVE

A plan that closes, or a document that is superseded, **crosses from LIVING to
ARCHIVE in a stated transition**:

1. The final-state edits — including the class-line change — land in **one
   commit**, and that commit says it is the transition.
2. The document is **frozen from the next commit onward**.
3. Later work writes a **new** document. It does not reopen the old one.

Doing it this way means there is never a period where a document's class is
ambiguous, and never a commit where content edits and the freeze are mixed with
each other in a way nobody can untangle later.

## 4. Class mapping for the older labels

The pre-build corpus used four labels. They map onto the two classes as
follows, so that "may I edit this?" always has an answer:

| Older label | Class | Meaning |
|---|---|---|
| `LIVING` | LIVING | unchanged |
| `DRAFT` | LIVING | in flight, edited in place, has not been ratified yet |
| `FINAL` | ARCHIVE | issued and binding; a correction supersedes |
| `findings` | ARCHIVE | a dated measurement; ARCHIVE from its first commit |

`DRAFT` and `FINAL` carried real meaning for the charter corpus and for
engagement kickoffs, and are kept for those. They are **not** a third and
fourth class — each is one of the two, spelled differently for a lane where the
distinction mattered.

## 5. Decision records, and their one backfilled field

A decision record is **ARCHIVE**: the context, the options, the call and the
reasoning are what was true when the decision was made, and they are never
revised into something more flattering.

**One field is exempt: `Outcome`.** It is backfilled once, later, when the
decision has actually played out — in a commit that says so. That is the
opposite of a justification exercise: it is the record of whether the reasoning
held, written at a point where it can be checked. Everything above it stays
frozen.

**A decision record lands in the same commit as the change it governs, or it is
not worth having.** Written afterwards it is a rationalisation, and it will
read like one.

## 6. One home per fact

**Before writing a sentence that restates another document's fact, link
instead.**

**A pointer document carries zero state predicates.** An index, a README that
routes, a table of contents: no dates, no counts, no statuses, no "done"s, no
paragraph summarising what it points at. Every one of those is a second home
for a fact, and second homes go stale silently while looking authoritative.

The split that makes this work in a repository with a private working tree:
**the public record is the home of the *decision*; the private corpus holds the
*evidence* the decision cites.** Those are different facts, not two homes for
one — and the pointer runs public → private only where the evidence is not
itself published.

## 7. Source comments

**A comment states the constraint and its reasoning — never the pull request,
review round, or finding that discovered it.** Discovery provenance lives in
`git blame`, which is better at it and never goes stale.

The exception is a pointer standing in for a rationale too large to restate in
a comment.

Existing citations are cleaned **opportunistically, when a file is touched for
another reason** — never as a bulk scrub. A tree-wide cleanup commit is churn
that makes every subsequent `git blame` worse.

## 8. The doc map

| Path | Class | Note |
|---|---|---|
| `CLAUDE.md` | LIVING | The operating manual. Edits go through the same gate as code |
| `README.md` | LIVING | The public face |
| `BUILD_PLAN_v2.md` | LIVING | The one home for sequencing |
| `standards/*.md` | LIVING | Conventions, including this file |
| `decisions/*.md` | ARCHIVE | One per decision, `Outcome` backfilled once (§5) |
| `decisions/README.md` | LIVING | Pointer only — format and rules, no listing |
| `findings/*.md` | ARCHIVE | Dated measurements |
| `findings/README.md` | LIVING | Pointer only |
| `tests/fixtures/README.md` | LIVING | Fixture rules |
| `dev/session-handoffs/*.md` | ARCHIVE | One per session, frozen from its first commit. The newest **is** "where we are" |
| `dev/**` (rest) | mixed | Untracked working corpus; each document declares its own class |

## 9. File naming

Document file names key on class. The rule and its rationale are in
[`RIPPERDOC_NAMING.md`](RIPPERDOC_NAMING.md) §5.

The untracked `dev/` corpus predates this standard and carries an older
spelling. It is **not** retrofitted — a bulk rename would break every citation
in the record for no benefit, which is §7's rule applied to filenames.
