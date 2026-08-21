# CLAUDE.md — ripperdoc — how we operate

*ripperdoc is a deterministic resolved-state engine for Cyberpunk 2077 mod
setups: comprehensive data-layer access — TweakDB, CR2W resources, redscript,
archives — beneath the existing tooling. This file is the first thing every
session reads, and it is written so that a session which can read **only this
repository** operates correctly.*

---

## 1. Where we are

- **Phase: build.** The research phase closed with a chartered decision, not a
  drift into code. What that phase concluded is settled and is not reopened
  here; the reasoning is published in [`decisions/`](decisions/).
- **Sequencing lives in [`BUILD_PLAN_v2.md`](BUILD_PLAN_v2.md)**, which is the
  one home for what comes next and in what order. This file carries no wave
  numbers, no counts, no status — anything that can go stale belongs in a
  document that is expected to change, not in the manual.
- **Customer: Aaron.** He plays Cyberpunk with his own modded setup, and that
  install is the test bench. Community adoption is a later question, not a
  gating one.

## 2. Read in this order

Three lanes, kept separate on purpose. Do not collapse them into each other.

1. **This file — *how we operate*.** Stable. It changes when the way we work
   changes, not when the work changes.
2. **The newest file in `dev/session-handoffs/` — *where we are*.** Tactical.
   The newest handoff **is** the answer to "what is in flight"; there is no
   index or status file duplicating it, because two homes for one fact means
   one of them is wrong and nobody knows which.
3. **The working corpus in `dev/` — *why*.** Foundational, and expensive:
   read once, consult on demand, never per-session.

Then, as the work requires: [`decisions/`](decisions/) for why the project is
shaped the way it is, [`findings/`](findings/) for what has actually been
measured, and [`standards/`](standards/) for the conventions.

**Do not pad this file with tactical state** as insurance against a skipped
handoff. That is precisely how an operating manual bloats until nobody reads
it.

### The two trees

This repository is public, and `dev/` is **not tracked in it**. The working
corpus — research, handoffs, kickoffs, the advisor lane's decision log — lives
in the working tree only. It carries machine paths, install contents and the
bench characterisation, and none of that belongs in a public repository.

Two consequences a session has to hold:

- **Anything that must move in lockstep with the code lives in the tracked
  tree.** This file, `standards/`, `decisions/`, `findings/`. A document that
  governs the code but sits in `dev/` cannot be updated in the same commit as
  the code, and same-commit is the only discipline that actually prevents doc
  rot.
- **Scrub is a habit, not a pass.** Public-destined documents are written with
  relative or placeholder paths **from the first draft**. Nothing is pasted
  from the private corpus unscrubbed and cleaned up afterwards; that is how a
  machine path ends up in a history nobody can edit.

The sentinel-phase repository is retired and read-only. It is the archive of
how the project got here, and every commit cited in the record resolves
against it.

## 3. Cornerstones

These do not iterate. A stumbling block that seems to require breaking one is
a §4 event, not a judgement call.

1. **Coverage by construction, where possible.** The schema layer is generated
   or inherited from machine-complete sources — never hand-wired per record
   type. The lane split:
   - **TweakDB** — two machine-complete sources, one per mode, neither
     hand-wired. The no-setup default inherits the pinned library's compiled
     type model by reflection; the dump-bound mode derives our own schema
     mechanically from the RTTI dump. Both modes are validated field by field
     against the shipped database, and what the inherited mode cannot do is
     named in the artifact's own provenance, never silently absent.
   - **CR2W resources** — WolvenKit's type model is inherited, with the RTTI
     dump as a by-construction validator and a dependency-drift gate in CI.

   If a stumbling block ever frames a hand-maintained schema subset as
   "pragmatic for v1", that is a cornerstone violation. Stop and surface (§4).

2. **Composition by construction.** Bulk capability is the closure of a small
   primitive set, never a verb per job. A job-shaped verb is a bug report
   against the primitive set: add the primitive, or file the gap. Domain
   knowledge lives in data, not in the tool surface.

3. **No silent failure.** Never a silent wrong answer, never a silently
   degraded mode. If a tool is compromised, or a check could not run, or a
   value could not be validated — say so, say what was checked, and say what
   to try next. A skipped check reported as green is the same lie as a wrong
   answer.

## 4. Revalidation protocol

When a stumbling block challenges a foundational assumption, **stop — do not
work around it.** Name the assumption, re-read the document that established
it, and surface to Aaron as exactly one of:

- **(a)** the assumption holds, and a clean solution exists;
- **(b)** the assumption is wrong, but the goal survives a revision — wait for
  Aaron's go on the revision;
- **(c)** the assumption is wrong and no revision preserves the goal — an
  architecture decision, and Aaron's call.

Never normalise a compromise as "good enough" without Aaron's go. Surfacing
costs minutes.

## 5. Operating principles

How a session behaves.

1. **Empirical-first.** Nothing locks without empirical confirmation. A plan
   reviewed is not a thing proven.
2. **Candor is cheap.** Surface doubt — about a decision, a document, the
   direction itself — without waiting to be asked. Honest opinion is a
   first-class deliverable.
3. **Guardrails are tools, not sacred.** Locks, conventions, and this file are
   revisable when reality contradicts them. Propose the revisit; Aaron decides.
4. **Anti-bloat, and plain register.** The orientation surface stays small;
   prune aggressively, archive rather than accrete. Handoffs, commit messages
   and documents say what changed and why in ordinary sentences — no
   capitalised lore, no allusions that need the archive to decode. These
   documents are read by agents that pay tokens to parse them.
5. **Lanes.** Aaron architects and picks the execution method — capability
   scope, trade-offs, architecture, and how the work is approached. A session
   proposes options **with a clear recommendation**, then does the mechanical
   drafting and decomposition once he picks. Surface method choices; do not
   silently pick them — but once he has chosen, or where a call is plainly
   mechanical, proceed decisively.
6. **Explicit uncertainty over performed certainty.** "Here is what I think,
   why, what I do not know, and how we would find out" beats a tidy matrix
   implying false confidence.
7. **No silent failure** — cornerstone 3, restated as behaviour.
8. **Atomic, focused commits.** One logical change per commit; one logical
   change per pull request.
9. **No silent workarounds.** §4 generalised to any decision that trades away
   something that was supposed to hold.
10. **The threshold principle.** *A guard is added only after the incident has
    actually recurred here.* Conventions stay conventions; a rule earns a hook,
    a gate or a lint only once drift is a recorded, repeated problem in this
    project — not because it was one somewhere else. This is the principle that
    makes everything else in this file safe to adopt, and it is why an
    inherited lock-down is not imported wholesale.

## 6. Worktree and landing discipline

**Every change that will commit starts in a worktree. Solo sessions included.**

- **Check your branch before touching anything** — `git branch --show-current`
  — and **state it up front**. Booting onto `main` and editing there is the
  recurring drift this rule exists to stop, and the stated-out-loud half is
  the part that works.
- If you are on `main`, create the worktree **first**:
  `.claude/worktrees/<name>/`, branch `claude/<name>`.
- **The main repository folder stays on `main`, read-only** except for landing
  reviewed branches into it.
- **Commit freely on the worktree branch.** It is local and reversible; that
  is what makes the worktree cheap.

**Landing is a separate, outward-facing act, never automatic.** In order:

1. Review rounds (§7), for a code branch.
2. Push the branch.
3. Open a pull request. Link the issue it closes with a closing keyword, so
   closure happens on merge rather than being remembered.
4. **Aaron's own independent review, posted to the PR.**
5. Fold its findings.
6. **Aaron's explicit go** — each time, not once for the branch.
7. `gh pr merge <PR#> --rebase --delete-branch`. Rebase keeps history linear.
8. Remove the worktree, delete the local branch. `--delete-branch` cannot
   delete a local branch while its worktree still holds it, so the worktree
   comes off first.
9. **Confirm the linked issue actually closed.** Verified, not assumed.

**`main` is branch-protected: land through the pull request, never by hand.**
A direct push is rejected, and that is deliberate.

**Any commit that edits this operating manual, or other self-governing
configuration, goes through the same gate.** Surface it; do not self-commit
the rules you are operating under.

## 7. Review rounds

Before a **code** branch is pushed, it is reviewed by agents that did not write
it — and that review is bounded, because an unbounded one makes the code worse.

**Where the bounds come from.** The reference implementation ran this unbounded
once: a three-fix branch spent **ten rounds, roughly four million tokens and
twenty agents**, four of its five high findings were **introduced by the
branch's own folds**, and the human review afterwards still found the two
mediums that actually mattered. The numbers below are that measurement, not a
preference — a future revision should argue against that baseline rather than
against a bare constant.

**The loop.** Spawn independent review agents over the branch diff, fold what
survives triage, then spawn a **new** round. Stop on the first of:

- **A round returns only low findings.**
- **The same failure CLASS recurs in two consecutive rounds.** A recurring
  class is a design signal, not a fix queue: invoke §4 and take the seam to
  Aaron. **Never fold a third instance.** Severity is explicitly *not* this
  signal — in the measured case, four consecutive folds were one class, while
  later rounds returned no highs at all and the feature was still broken.
- **Three rounds.** Whatever is open after the third goes to Aaron's review
  with the findings listed, not into a fourth round.
- **No new rounds after directed folds.** Fresh eyes are for before the PR
  opens.

**What makes the rounds worth their tokens rather than theatre:**

- **Fresh agents every round.** Never continue a reviewer that already saw the
  code; never let the session review itself. A reviewer that watched you fold
  its own finding is no longer independent.
- **Review agents get their own worktree**, never the tree you commit from.
  Break-testing reviewers mutate source by design — sabotage a value, confirm
  RED, restore — and an agent killed mid-test leaves its sabotage behind.
- **Read the diff before committing a fold. Stage explicitly.** Never
  `git add -A` after agents have had the tree.
- **Seed reviewer prompts with the branch, the base, "conventions are in
  `CLAUDE.md`", and the branch's settled-decisions list.** Never with what you
  changed, where you think the risk is, what you are unsure of, or what an
  earlier round found. A prompt that names your worry gets that worry back; a
  prompt without the settled list gets chartered design re-litigated every
  round.
- **Triage every finding against the source before folding.** A finding you
  cannot reproduce is one you refuse, and you say why. **A round folded at 100%
  must state why nothing was refused** — the refusal rate is the triage health
  signal, and obedience is not triage.
- **A finding about a user-facing sentence gets a probe, not a rewording.**
  Measure what the sentence tells the caller to do before changing what it
  says.
- **A fold that adds a conditional ships an arm per branch, RED-checked in both
  directions.** A branch that cannot be fixtured honestly is a design signal to
  escalate the conditional, not a testing gap to work around.
- **Report the rounds in the PR body** — how many, what each found, what was
  refused and why.
- **Scope: code branches.** A docs-only or config-only change does not need
  rounds. **Say so, rather than performing them.**

## 8. Proof discipline

The other side of §7: what makes a check a check.

- **A fix ships a check that fails before the change and passes after.** If one
  could not be written, say which and why — do not ship silence.
- **A defensive path ships the claim it makes, checked.** A guard, a state, or a
  failure message asserts something to whoever reads it, and that assertion
  carries a check that it holds. A claim that cannot be checked is narrowed to
  one that can.
- **Sabotage RED-checks run from a committed state.** Commit the fold, then
  sabotage, then restore, and **verify the restore** by looking for something
  the fold introduced rather than assuming it worked. Never sabotage a dirty
  tree; stashing first is the same trap wearing a different hat.
- **A scripted sweep carries a known-RED canary.** Verification machinery fails
  toward green — a build swallowed by a pipe, a grep that eats its own FAIL
  lines — so an all-green sweep proves the harness ran only if something in it
  was expected to fail. A sweep with no cell expected to fail is a broken
  sweep, not a passing one.
- **A check that cannot run self-skips and says so, by name.** No game, no dump
  and no install on a CI runner is a fact to announce, never an absence to
  leave quiet.
- **Size is not identity.** Where a hash exists, compare hashes. Files can be
  size-identical and byte-different, and a size-based "unchanged" is a proxy
  that fails silently.
- **Verify against a baseline, not against tidiness.** Returning something to a
  known state means checking what is actually there against a manifest, not
  assuming that the tool which removed things removed all of them.

## 9. Escalation — one statement, four routes

Everything here routes to §4. Which door you came through matters less than
actually stopping.

1. **§4 itself** is the destination: name the assumption, re-read the source,
   surface as (a), (b) or (c).
2. **A failure class recurring across two consecutive review rounds** is a
   design signal. Take the seam to Aaron; do not fold a third instance.
3. **The terminal gate.** Whatever is open after three rounds goes to Aaron's
   review with the findings listed. The rounds exist so that his review spends
   itself on judgement — they never replace it.
4. **Tripwires are pre-committed at routing time**, not discovered later: *if
   this fix balloons, ship the smaller thing and file the rest*; *if this change
   gets hand-copied to a third site, stop and surface the extraction*. One
   sentence when the work is routed saves an escalation afterwards.

The advisor lane is advisory: it renders judgement, Aaron decides, and worker
sessions reach it **only through Aaron**.

## 10. Naming

All names follow [`standards/RIPPERDOC_NAMING.md`](standards/RIPPERDOC_NAMING.md).
The two load-bearing rules: tools on the eventual surface are
`ripperdoc_<snake_case>`, and the brand string lives in **exactly one place in
code** — everything else derives from it, and interior names are purpose-based
and carry no brand at all.

## 11. Documents

Document classes, their markers, and the rules that keep them honest are in
[`standards/RIPPERDOC_DOC_HYGIENE.md`](standards/RIPPERDOC_DOC_HYGIENE.md).
The two that bite most often:

- **A LIVING document is updated in the same commit as the thing it
  describes.** If a LIVING document contradicts the code, the document is the
  bug.
- **An ARCHIVE document is immutable.** Corrections supersede in a new
  document; they never edit the old one.

**A decision record lands in the same commit as the change it governs, or it is
not worth having.** Written afterwards, it is a justification exercise.

## 12. What NOT to do

- **Don't reconstruct context from prior sessions.** Read the documents (§2).
  Memory supplements; it does not substitute.
- **Don't work on `main`.** §6. Check your branch at the start.
- **Don't treat coverage as a subset.** §3. Tempted to ship "the common record
  types"? Stop and read §4.
- **Don't ship a bespoke bulk verb.** §3, cornerstone 2.
- **Don't append a new domain to a large existing file.** A new subsystem's
  logic lands as its own file; a facade may keep a thin delegating member, but
  the logic gets its own home. This rule is here before the first large file
  exists because its cost is already known: in the reference implementation one
  service file absorbed nine-plus domains, because code landed where the last
  thing went and nothing ever asked whether it belonged.
- **Don't cite the discovery in a source comment.** A comment states the
  constraint and the reasoning — never the pull request, review round, or
  finding that discovered it. Discovery provenance lives in `git blame`. The
  exception is a pointer standing in for a rationale too large to restate.
  Existing citations are cleaned opportunistically when a file is touched,
  never as a bulk scrub. This rule is also here early on measured grounds: the
  reference implementation adopted it only after roughly five hundred such
  citations had accumulated across its source tree.
- **Don't silently work around a block.** §4.
- **Don't edit an ARCHIVE document.** Supersede it.
- **Don't touch Aaron's live game install destructively.** It is the test
  bench: read freely, write only through the declarative mod lane, never edit
  game files or existing mods in place.
- **Don't import a guardrail that has not earned its place here.** §5 #10.
- **Don't put game-derived bytes in this repository.** Not one record, not a
  "small excerpt". See [`tests/fixtures/README.md`](tests/fixtures/README.md).
