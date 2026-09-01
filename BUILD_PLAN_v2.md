# BUILD_PLAN_v2 — ripperdoc

**Class: LIVING.** The one home for sequencing. Amendments are **appended to
§11**, never applied as silent edits to the table.

Successor to the build plan written during the research phase, which is
immutable and lives in the project's working corpus. This document supersedes
it; where they differ, this one is right. It iterates as `BUILD_PLAN_v3`, not
as an edit to itself.

**No temporal anchors.** Nothing here is estimated in days or weeks. Waves are
sequenced and sized by scope; the only ordering commitments are dependencies
and gates.

---

## Boot rules — diff, don't remember

A session booting against this plan does three things **before** starting work.
All three are diffs against live state, because memory of "what was in flight"
is the thing that goes stale first.

1. **Diff the open work against the wave table.** Anything open that is not
   routed to a wave here is a **boot-time flag**, raised then — not a discovery
   made halfway through an engagement.
2. **Diff deferred checks against §10.** A deferral with no line there is the
   same kind of flag. A check deferred without being written down at the moment
   it was deferred is a check that will not happen.
3. **Read the newest session handoff** for what is actually in flight. This
   document owns *sequence*; the handoff owns *state*. Neither substitutes for
   the other.

## 1. What this document owns — and what it does not

| Lives here | Lives elsewhere |
|---|---|
| Wave sequence and dependencies | What is in flight right now → the newest session handoff |
| Gates, and what each blocks | Why the architecture is what it is → [`decisions/`](decisions/) |
| Tripwires, promotion conditions, the not-routed list | What has been measured → [`findings/`](findings/) and the working corpus |
| CI tiers and posture | How we work → [`CLAUDE.md`](CLAUDE.md) |

**One home per fact.** If sequencing appears in two places, one of them is
wrong and nobody knows which.

### Milestones carry membership, never sequence

A GitHub milestone on this repository means exactly one thing: **this item
closes before that ship point.** It says nothing about order, and it is not a
theme or a label for related work.

That restriction is what stops it rotting. Sequence lives in §3 and changes
there; a milestone that also implied ordering would silently disagree with this
table the first time anything moved. What the milestone *is* good for is being
the **public projection of routing** — the one signal about what is coming that
an outside reporter can see without reading this document.

Two rules follow:

- **Routing an item and setting its milestone are one act.** An item routed to
  a wave without a milestone is invisible outside; a milestone set without
  routing is a promise nothing owns.
- **A milestone is a named ship point with a close-condition walk** — the
  condition is written in the milestone's own description, so closing it means
  checking something rather than deciding it. An item that fits no milestone's
  gate is a flag that a new one may be due, and creating one is Aaron's call.

## 2. The shape, in one paragraph

Build a deterministic resolved-state engine in C# on .NET 8 against a pinned
WolvenKit, in the settled value order: **schema layer → TweakDB resolved state
→ archives → scripts → diagnosis → budget → lookup → writes.** No AI inside the
engine; the agent layer and any GUI are clients of it. GUI last. Nothing is
hand-wired anywhere.

The engine is a **library** — see
[the topology record](decisions/2026-08-21-library-first-topology.md).

## 3. The wave table

| Wave | What it delivers | Depends on | Gate |
|---|---|---|---|
| **0** | Repository, standards, CI, the public decision and findings lanes, this plan | — | — |
| **1a** | **The dump-free spine of the schema layer** — the derivation transform, the hash validator, the validation manifest, and the schema IR in its dump-free (degraded) mode | 0 | — |
| **2** | **The first vertical slice: TweakDB resolved state.** The proof | 1a | — |
| **1b** | **The dump-bound trio** — typed reference graph, dependency-drift gate in CI, first-run generation experience | 1a | — |
| **3** | Archive layer — enumeration, contested sets, precedence resolution, the mod-manager instance reader | 2 | **7a** |
| **4** | Script layer — `@replaceMethod` collisions with the winner named, `@wrapMethod` chains in order, missing `wrappedMethod()` calls | 2 | — |
| **5** | Diagnosis lane — the bisection replacement | 3, 4 | — |
| **6** | Budget telemetry — flat-buffer usage against the ceiling, attributed per mod | 2 | — |
| **7** | Semantic lookup — the layer that turns a winner table into an explanation | 2 | — |
| **8** | Write assistance, prescriptive ordering, and CR2W | 2, 3 | **5** |
| later | The GUI | 8 | — |

**Why 1b sits after 2, and why the numbering looks odd.** Wave 1 was originally
one wave whose exit criterion required a generated RTTI dump. It has an
internal seam — most of it is dump-free — and that seam runs exactly where the
first vertical slice's real dependencies stop. Splitting it moves the **proof**
earlier and cheaper. See
[the seam record](decisions/2026-08-21-dump-free-spine-first.md).

The numbers are deliberately **not** resequenced into 1, 2, 3. Gate and wave
numbers are referenced across several immutable documents; renumbering would
break every one of those citations to make a table look tidier.

## 4. Waves 0–2, specified

### Wave 0 — bootstrap

A public repository with the working corpus untracked inside it; a .NET 8
solution with the dependency pinned exactly and a test asserting the pin; the
operating manual and the first two standards; a CI gate whose check set has one
source of truth; the issue lane; the public decision and findings lanes; and
this plan.

**No engine code.** The bootstrap commit lands directly on `main` by exception;
branch protection is on from immediately after, and every commit since has gone
through a worktree, a pull request, and Aaron's review.

### Wave 1a — the dump-free spine

Four deliverables, none of which need anything generated from a game install:

1. **The derivation transform** — record fields derived mechanically from the
   type information's getter shapes, with its normalisation rules and its one
   documented exception.
2. **The hash validator** — the game's own identifier hash, self-tested against
   the pinned library's own conversion. This is the arbiter: it is what makes
   "the schema says X" checkable against real shipped data rather than
   asserted.
3. **The validation manifest** — every record field marked *validated* or
   *unvalidated* against real shipped values. This is the no-silent-failure
   cornerstone in structural form: an unvalidated field is **labelled**, never
   silently wrong.
4. **The schema IR, dump-free mode** — the artifact, generated without a dump
   and honestly degraded, carrying its provenance block.

**Exit:** the spine reproduces the research sketch's measured results as
product code. Divergence from those numbers is a defect in the port, and is
investigated rather than accepted.

### Wave 2 — the first vertical slice, and the real proof

End to end, on a real install, with no probe involved: read the tweak layer,
replay it in the frameworks' own order, and report a same-value collision
**named with provenance** — this value, from this mod, because of this rule.

**Exit:** a collision on a real modded install, found and explained by a tool
rather than by a probe. This is the wave that makes the product real, and it is
why 1b was moved behind it.

### Wave 1b — the dump-bound trio

The typed reference graph recovered from the type information; the
dependency-drift gate running **in CI**, failing loudly on divergence; and the
first-run generation experience — detect a missing or stale dump, explain what
is needed and why nothing is redistributed, verify provenance, generate.

## 5. Waves 3–8 — sequenced, not locked

Shape emerges from wave-2 experience. What is locked is the order and the
dependencies.

**Wave 3 — archive layer.** The precedence law is measured and published
([archive load order](findings/2026-08-19-archive-load-order.md)); the resolver
implements exactly that and nothing inferred. Three design inputs the research
phase produced, each of which would otherwise be discovered the hard way:

- **Report by hash, never omit.** A significant fraction of mod-archive entries
  have no name in the toolchain's dictionary, and the pinned package cannot
  name entries at all — the dictionary ships in a different distribution. This
  wave either takes that dependency or carries the resources with provenance.
  **Decide it explicitly; do not default into name-only reporting**, which
  silently drops every unnamed entry.
- **Read indices directly, not through the command-line tool.** Per-archive
  listing through the CLI was measured thousands of times slower than reading
  indices through the pinned library, and it interleaves diagnostic lines into
  its listings that a naive parser would ingest as data.
- **Path-level analysis has a blind spot, and the output must say so.**
  Resource-level contests were observed between mods that share no file path —
  a contested-path computation cannot see them by construction.

**Platform priority within this wave** (Aaron, 2026-08-21): manual installs
first, then the deployment manager he actually uses, then polish for the other.
The directory read that serves manual installs also serves one manager's
deployed state for free; the other's reader waits on gate **7a** regardless,
because until that characterisation runs there is nothing on disk to read.

**Wave 4 — script layer.** The cheapest of the three domains, because mods ship
script source by construction.

**Wave 5 — diagnosis lane.** The bisection replacement, and the capability that
most changes how the tool is actually used. Three hard requirements the research
phase produced:

- **Read the manager, not just the game directory.** Only the manager's
  manifest knows how many mods were *wanted*. A tool reading only the game
  directory can say what is deployed and nothing about what is missing.
- **Attribute logs by timestamp, never by filename.** One framework rotates its
  previous log and names the rotation after the **new** boot while filling it
  with the **old** boot's content. Reproduced on consecutive boots. A
  filename-keyed routine attributes the previous boot's failures to this one.
- **A cycle check over ordering metadata belongs here, before any judgement
  does — narrowed to what was measured.** What was observed is that a
  collection's deployment was interrupted with a large share of its mods never
  enabled, while the manager warned three times that mod rules contained
  cycles. It named no rule, no edge and no path, and the state it was checking
  no longer exists. Every ordering input that survives reads **acyclic** — the
  collection's rules under three independent node identities, 2,221 of them
  under two and 2,217 under the third that joins each side to the declared mod
  it names, the manager's own 288 rule edges — 283 of them `requires`, which
  the shipped check counts rather than edges — and a second collection's five
  ([measured](findings/2026-09-01-manager-state-and-partition.md)). What ships
  is the check itself: it reports a cycle as a **path** rather than a flag, and
  names in its provenance which edge sets were in the graph and which homes
  were not read. The readers that fetch those rules — from the collection
  manifest and from the manager's state database — are **not in this branch**
  and arrive with wave 5 part two, so until they do, every edge the check holds
  is one its caller supplied. **It does not claim to explain that failure**,
  and nothing built on it may: the attribution is unreproduced, filed as
  [#60](https://github.com/Avick3110/ripperdoc/issues/60), and its closure route
  is §10 row 19.

**Wave 6 — budget telemetry.** Nearly free once tweaks are parsed: flat-buffer
usage against the ceiling, **attributed per mod**. The vanilla-plus-expansion
baseline was reproduced identically across four independent boots. The open
successor question is per-mod apportionment of values that are pooled and
shared.

**Wave 7 — semantic lookup.** What turns a winner table into an explanation.
Corpus search over the user's own authored tweak sources plus the modding
wiki's machine-readable endpoints — **fetched at runtime, never vendored**, as
the wiki carries no licence.

**Wave 8 — write assistance, prescriptive ordering, and CR2W.** Thin by design:
declarative emission, validated. The ordering extension **stops at other
people's files** — where a chosen order would require a third-party file to
move, the tool names the file and the destination and stops, rather than
relocating something it did not author.

**Later — the GUI.** An analgesic view over the same engine, sequenced last.

## 6. CI — three tiers, and the posture

**A capability being dump-free does not make it CI-testable.** The shipped
database is not ours to redistribute either. What makes CI possible at all is
that base states are **constructible in memory** with the pinned library —
dump-free and free of any game-derived bytes.

| Tier | Covers | Runs |
|---|---|---|
| **(i) synthetic** | Most of the engine — replay ordering, collision detection, provenance, contributor chains, budget arithmetic | CI |
| **(ii) local, shipped database** | Binary parse over stored values; the validation sweep | Developer machine |
| **(ii) local, installed tweak layer** | The slice end to end over a real tweak lane — the tweak directory, the framework's own metadata, and the database whose values decide inheritance | Developer machine |
| **(iii) local, dump** | Drift gate; typed reference graph | Developer machine |

The two tier (ii) lanes are separate inputs, not one. A machine can have the
game's database and no mods, or the reverse. They also behave differently under
a check: the database can be fingerprinted, so checks over it reproduce counts
measured against one build and announce any other build as an input they do not
apply to — an installed layer changes whenever its owner installs a mod, so
checks over it **assert what holds of any layer and report the numbers rather
than asserting them**.

Tier (iii) runs locally; CI asserts that it ran and that its output is current.

**Why tier (ii) exists rather than folding into (i).** In the pinned library
version, a written TweakDB file **can be read back** by the same library's
reader until a **stored value** is in it. An empty database round trips, and so
does one carrying records; add a flat — loose, or set as a record's property —
and the file ends before the structure the reader is following does. This
blocks nothing, because the write lane never emits binary. A records-carrying
database is enough to put the file-reading path's success under tier (i), but a
**parse over stored values cannot be synthesised**, and must be exercised
against a real shipped database or not at all.

**Posture rules, binding wherever a check is written:**

1. **Run every check even when one fails**, and name each failure. A red run
   should tell you everything that is broken.
2. **A check that cannot run self-skips and says so, by name.** An absent tool
   never reads as a pass.
3. **Document a carve-out at the carve-out.** When an optimisation changes what
   a check can observe, the reason is written where the exception is.
4. **One source of truth for the check set.** Adding a check adds it
   everywhere.
5. **Clean before verifying a generator change.** Build-then-run can serve a
   stale binary, and wave 1's deliverable is a generator whose output is the
   thing under test.
6. **Publish with trimming off.** Trimming strips reflected types and silently
   loses coverage — the same hazard class as the schema layer's whole method.

## 7. Gate map

| # | Open item | Blocks | Wave | State |
|---|---|---|---|---|
| 1 | Buffer ceiling at scale | The *claim* about prevalence | 6 | **Closed — it is a curve, not a ceiling problem.** Successor question: per-mod apportionment of shared pooled values |
| 2 | Mod-declared record types at scale | Modded-coverage claims | 6 | **Closed — none found**, two samples, version-robust. Successor moved to wave 2: mods name **properties** the schema lacks, routinely |
| 3 | Archives absent from a partial list | Archive-layer completeness | 3 | **Closed.** Unlisted load after every listed — [published](findings/2026-08-19-archive-load-order.md). Residue: unlisted-vs-unlisted order, unmeasured, labelled |
| 4 | Tweak subdirectory ordering | Replay correctness | 2 | **Closed** — the only item that ever gated a capability. [Published](findings/2026-08-19-tweak-file-order.md), and **superseded 2026-08-22** by [the three-group law](findings/2026-08-22-tweak-file-order-groups.md), which the original instrument could not see. Residue: the grouping is source-derived pending one boot |
| 5 | CR2W parse validation | CR2W lane confidence | 8 | **OPEN.** Designed, not run. Gates confidence, not capability |
| 6 | Third-party conflict-listing tool evaluation | Archive-layer design | 3 | **Closed — SKIP.** It cannot report a conflict at any corpus size. Wave 3 inherits nothing from it |
| 7 | Deployment shapes | The deployment-agnostic claim | 3 | **Partly closed.** Two shapes measured; the third is a channel boundary only — converted into 7a |
| **7a** | **Manager/virtual-filesystem characterisation** | The **design** of the manager-instance reader | **3, pre-design** | **OPEN.** Its own engagement, not this plan's to run. Until it runs, every claim about that platform stays labelled gated |
| 8 | Upstream licence confirmation | *(was: any release)* | 0 | **Closed as reframed.** No longer a release gate — reframed to a non-blocking courtesy heads-up, **sent 2026-08-21**. Any reply is backfilled here and in the decision log |
| 9 | Baseline confirmation boot | Nothing | — | **Closed** |

**Two items remain open: 5 and 7a.** Item 5 gates confidence in the inherit
lane; 7a gates a design. **Neither gates a capability**, and the one item that
ever did — #4 — is closed and published.

## 8. What would make this order wrong

The falsifiers for the sequence itself. If one of these turns out to be true,
the order changes rather than being defended.

- **If the dump-free spine cannot honestly demonstrate the wave-2 slice**, the
  seam split was wrong and the dump-bound trio moves back ahead of the proof.
  This is the single most consequential assumption in the table.
- **If the schema layer turns out to be the hard part rather than the replay
  semantics**, the sizing is inverted and waves 1a/2 need re-scoping. The
  current reading is that the generator is small and well-understood while the
  **apply semantics** are the genuinely unmeasured part — specifically that a
  mod setting a value on a parent can move a child's resolved value, so "the
  same value" has an indirect form the replay must model.
- **If the archive layer's naming dependency has no acceptable answer**, wave 3
  either takes a heavier dependency than expected or ships reporting that is
  honest but harder to read. Either way the wave gets bigger.
- **If diagnosis turns out to be the thing the user actually wants first**,
  wave 5 has been sequenced three waves too late. Its inputs genuinely depend on
  waves 3 and 4, so the fix is not a reorder but a thinner earlier slice.
- **If the no-setup default surface does not survive contact with real use** —
  if people generate a dump anyway — then optimising the sequence around
  dump-free capability bought less than it cost.

## 9. Deliberately NOT routed — do not re-audit

Recorded with reasoning so that the same triage is not re-run at every boot.

| Item | Why not |
|---|---|
| Porting the reference implementation's two local hooks | They have their precondition now, but the threshold principle governs: a guard is added after the incident recurs **here**. They earn their place from this project's own incidents, if any |
| `CONTRIBUTING.md`, an agent-agnostic companion file, external-report triage | Contributor-facing, and there are no contributors and no code to contribute to. Costs nothing to defer, and would be guesswork now |
| A changelog | Nothing user-facing has shipped. The accrual discipline arrives with the first user-facing change, not before it |
| Skill-authoring and packaging standards | Standards for surfaces that do not exist. The precedent for deferring them is direct: the reference implementation pre-authored its naming standard and then trimmed it back to the part whose surfaces existed |
| A sub-project lane | No second track exists or is foreseen |
| Renumbering waves and gates | Numbers are cited across immutable documents. Tidiness is not worth breaking citations |

## 10. Deferred checks

Every check deferred gets a line here **in the session that defers it** — not
later, and not from memory. A line carries its origin, what would close it,
and its status: `OPEN`, `RUN <date> → result`, or `WAIVED <date>, Aaron,
<named risk>`. **Waived lines survive as records; nothing is deleted.**

| # | Deferred check | Origin | Status |
|---|---|---|---|
| 1 | Ordering among archives that are all unlisted | Archive-order measurement; unobservable in that design | **OPEN** — labelled assumption in the finding |
| 2 | Whether the tweak framework sorts, or consumes an already-collated enumeration | Tweak-order measurement, single NTFS volume | **RUN 2026-08-22 → it consumes; there is no sort.** Settled from the framework's source at the shipped tag, [published](findings/2026-08-22-tweak-file-order-groups.md). The limit the finding labelled is therefore real rather than open: on a volume whose enumeration is not collated the winner differs. The engine takes the enumeration as it comes and reports per run whether it was collated |
| 3 | Per-mod apportionment of shared pooled values | Buffer-ceiling measurement | **OPEN** — wave 6 |
| 4 | Whether mods name properties the schema lacks, and how often | Mod-declared-types measurement | **RUN 2026-08-22 → none, on one install.** 1,249 property writes on records whose type resolved (451 of them): **0** named anything absent from the type model plus the framework's own extra-flats metadata, and 0 declared type failed to resolve. **382 of the same writes name something the type model alone lacks**, so the metadata is carrying most of the answer — both counts come from the same instrument and are re-runnable. **Re-run 2026-08-23 with a schema generated from the game's own type information in place of the inherited one: the count is 382 either way**, and the framework's metadata covers all of them in both cases, so the metadata is schema *neither* description of the game carries rather than something a dump would supply ([published](findings/2026-08-23-generated-schema-coverage.md)). Scope: one install, one layer; 70 records written to had types this reading could not work out, and their writes were not checked |
| 5 | How the game addresses a name carrying a character outside ASCII | Wave 1a. The pinned conversion replaces such a character with a placeholder, so two different names come out as one identifier; the engine refuses the name rather than reproduce the collision | **OPEN** — would be settled by a mod-authored name outside ASCII, resolved in game and read back. Until then the refusal is a labelled limit, not a measured rule |
| 6 | Whether the three-group read order separates files in a running game | The three-group finding. Read from the framework's shipped source and confirmed in the shipped binary; the one real layer it was replayed against could not discriminate, because the promoted file there already sorted first without the grouping | **OPEN** — one boot settles it: a layer built to separate the mechanisms (a promoted file in a directory that sorts last, an ordinary file in one that sorts first), with the framework's own log stating read order directly. The grouping is implemented in shipping code and decides which mod a report names, so it would change a reported winner if wrong |
| 7 | Whether the framework's extension match is case-sensitive in a running game | The same finding, same evidence class: read from the source, never observed | **OPEN** — settled by the same boot, with a file whose extension is spelled in capitals present in the layer. The engine reports such a file as passed over; if the framework in fact reads it, every value that file writes is absent from the resolved state and no contest over them is reported |
| 8 | Tier (i) cover for an archive entry that nothing can name | Wave 3 enumeration. An archive this project authors carries its own paths, so every entry in one is named, and the nameless case cannot be produced from an authored fixture without byte-editing a written container | **OPEN** — the counting and reporting of nameless entries is covered at tier (i) from values built directly, and the real distribution is asserted at tier (ii) against a live mod directory. What is not covered on a bare runner is the whole path from a container holding such an entry to the report naming it. Closed by an authored archive whose path table is stripped, if that turns out to be worth its fragility |
| 9 | That the naming source is prepared **before** any archive is read | Wave 3 enumeration. The order is what stops a dictionary posture producing a fully under-named inventory whose provenance claims dictionary coverage | **RUN 2026-08-25 → covered at both tiers, and the deferral’s own justification was wrong.** It was deferred on the ground that moving the call after the read loop left every check green. Measured, that reorder already reddened the archive lane’s cross-posture comparison at tier (ii); it was tier (i) alone that could not see it. Tier (i) now observes the ordering rather than the throw — the read is driven with a name resolution that records being reached, and a source that cannot load has to fail before anything reaches it |
| 10 | What a coverage figure means in a process that has already read other directories | Wave 3 provenance. Names accumulate in a process-wide resolver as archives are read, so a long-lived client can name entries in one directory because of another; the provenance block records the posture and the dictionary, not the reading history | **OPEN** — the limit is stated in the provenance type. Closed either by recording what the process had already read, or by a measurement showing the effect is nil on real lanes |
| 11 | How the game orders archive file names carrying a character outside ASCII | Wave 3 precedence. The resolver orders by `StringComparer.Ordinal`, which compares UTF-16 code units; the measurement that established the law used ASCII names only | **OPEN** — settled by a boot with two contesting archives whose names differ only outside ASCII and no list file, read back through the same detector the law was measured with. Until then the file-name branch of the law is an extrapolation for such a name. It misorders only where no list file decides the contest, and a present list is unaffected because matching is by name rather than by order |
| 12 | What the game does with two archive names differing only in case | Wave 3 precedence. Windows cannot hold both in one directory, so the case is reachable only on a tree copied to a case-sensitive volume | **OPEN** — the resolver matches names case-insensitively and therefore treats the two as one archive when resolving a contest, while the order still lists both. Closed by a measurement on a case-sensitive volume, or waived if the engine is declared Windows-only. Not closable on this bench |
| 13 | Which `@wrapMethod` in a chain is outermost | Wave 4 script-annotation measurement. Every wrap in a chain is emitted and the emitted code is identical under any nesting, so the code-size observable that settled the collision questions cannot see nesting at all | **OPEN** — [published](findings/2026-08-27-script-annotation-order.md) as a named limit. Closed by one boot with each wrap logging before it calls `wrappedMethod()`, whose log prints the chain outermost first. Until then the engine reports a chain in **compile order** and says so in the name of the member carrying it (`WrapsInCompileOrder`), which a call site has to spell; it assembles no sentence about a result at all, and a check holds the text a limit carries to a reader - its declared consequence and the name it reports itself under, both of them, for every limit in the declared set - against the vocabulary that would name an execution nesting. That population is those two strings per limit and no wider: it walks the declared set rather than whatever one fixture produces, and a sentence assembled elsewhere in the layer is outside it, which a separate check over the result type covers. The finding calls the same order *enumeration order*; the engine says compile order throughout and this row names the engine's words, because a row prescribing the other ones reads as unmet against the code it governs. Batchable with rows 6, 7 and 11, which are also boot-gated |
| 14 | Whether script source order is the directory index's own order or a sort the compiler performs | Wave 4 script-annotation measurement, single case-insensitive NTFS volume. Both hypotheses produce the measured list exactly, and the cross-check against a real install's boot log cannot separate them either, because that install is on the same kind of volume | **OPEN** — same shape as row 2 and settled the same way, by reading the compiler's shipped source at the pinned tag or by a measurement on a volume whose enumeration is not collated. They come apart on a case-sensitive file system, where the winner of a replacement collision would differ. The engine takes the enumeration as it comes and reports which it used |
| 15 | Whether the compiler reads a script file whose extension is spelled with a capital | Wave 4 script layer. The engine takes such a file, because the file system it reads them from does not distinguish the spellings; whether the compiler does was never observed, and the layer the law was measured against contained none, so nothing there could have shown it either way | **OPEN** — the engine reports them by name (`ScriptEnumeration.SourcesNotSpelledInLowerCase`), so a caller with an empty list has nothing at stake and one with a non-empty list can see which sources rest on this engine's choice rather than on a measured rule. On the one layer read so far the count is 0. Settled by a compile over a layer holding such a file, read back from the compiler's own printed source list |
| 16 | What an `@if` condition evaluates to — the rule the compiler uses to decide `ModuleExists` | Wave 4 conditional-compilation measurement. That the gate is evaluated, and that a false gate removes the declaration beneath it from the compile entirely, is [published](findings/2026-08-27-script-conditional-compilation.md); the conditions there were chosen only to be reliably true and reliably false, so nothing in it establishes how the compiler decides one | **OPEN** — the engine reads the gate and refuses to decide it: a gated annotation is reported **undetermined**, kept out of contests, and named in every result it could have changed. Settled by a measurement over declared and undeclared modules across the shapes a condition can take, not by reasoning from module declarations present in a layer, which is the guess this posture exists to refuse |
| 17 | The gate's grammar beyond a single `ModuleExists` — a braced form gating a group, nested gates, and what else may appear in a condition | Wave 4 conditional-compilation measurement. Every gate in the surveyed layer preceded exactly one declaration and no braced group appeared, so none was built; no cell carried two gates on one declaration | **OPEN** — rule 1 of the finding covers one gate over one declaration. The engine detects a gate lexically and treats what follows as undetermined, so an unmodelled form fails toward undetermined rather than toward a false live reading; a braced form would leave later declarations in the group read as live. Settled by compiles over each form |
| 18 | Whether this engine's set of modelled source spans is what the compiler treats as not-code | Wave 4's reader, restated when the span set was declared. The pass models a line comment, a block comment, a string literal and an interpolation, and each declared span is checked against the pass in the one shape its own source carries; what no derivation inside this repository can find is a category nobody declared, because the set that would settle it is the compiler's own grammar | **OPEN** — the engine states the boundary on `ScriptTextSpan` rather than leaving it to be inferred from an absence, and an annotation whose *argument* shape this engine does not model is left unresolved rather than live, which is checked; an annotation inside a span category nobody declared is not, and stands as live code. A nested block comment and a single-quoted string each do this, and the second manufactures a contest whose winner takes a method it does not replace. A third shape sits inside a category that *is* declared: a string literal is modelled as far as the line it opens on, and one crossing a line hands its contents back as code, so "declared and handled" means checked in one shape rather than across the category. All three are measured and filed as [#55](https://github.com/Avick3110/ripperdoc/issues/55); how often any of them occurs in a real layer is unmeasured. Closed by the compiler-agreement measurement filed as [#45](https://github.com/Avick3110/ripperdoc/issues/45), which is blocked on the invocation shape rather than on a decision |
| 19 | Where a deployment manager's reported rule cycle actually lives | Wave 5 ordering-metadata characterisation. A manager warned of cycles and interrupted a real deployment; every ordering input that survives reads acyclic — the collection's rules under three independent node identities, 2,221 of them under two and 2,217 under the third that joins each side to the declared mod it names, the manager's own 288 rule edges — 283 of them `requires`, which the shipped check counts rather than edges — and a second collection's five. Its log names no member, edge or path, and the state it checked is gone: every key version in the database was scanned, not only the newest | **OPEN** — the shipped check reports a cycle as a path and names in its provenance the edge sets in its graph and the homes it did not read, so a caller can see the verdict's scope rather than infer it. What ships is the check alone: the readers that fetch rules from the collection manifest and from the manager's state database are not in this branch and arrive with wave 5 part two, so every edge it holds is one its caller supplied. What no input on this bench can settle is which graph the manager itself checks, so the engine asserts nothing about why that deployment failed and `BUILD_PLAN_v2` §5 carries the narrowed claim. Closed by one measurement: a deliberately cyclic collection installed under the manager with the state captured **before** deployment is attempted, so the authored cycle's shape can be held against the graph the manager reports on. Filed as [#60](https://github.com/Avick3110/ripperdoc/issues/60), unowned and unmilestoned |
| 20 | Whether the compiler's own list of blamed mods names the same mods path attribution implicates | Wave 5 log-attribution measurement. The two lists are in different namespaces - the compiler names the script directory a source sits in, the engine names the manager mod id - so they cannot be compared as written, and the join that would compare them is the deployment record, which is the thing under test | **OPEN** - the counts agree at 12 and 12, and joining trailer entries through the record's script directories resolves 11 of the 12; the twelfth names a directory the record attributes to more than one mod, which that join cannot decide. The finding states the partial agreement rather than the equality it first claimed, and nothing in the engine consults the trailer - attribution runs from the error paths and the record alone, so what is deferred is a corroboration and not a dependency. Closed by a route that resolves a blamed directory to one mod where the record attributes it to several, or by a failing compile whose blamed directories are each claimed by exactly one mod |

**The ship gate is: this table empty, or every remaining line waived by name.**

## 11. Amendment history

Append-only. Each entry carries its date and its authority.

**2026-08-21 — v2 created.** Supersedes the research-phase plan, which is
immutable. Carries the entries below.

1. **Topology: library-first** (Aaron, 2026-08-21). The engine is a .NET
   library; clients load it; no standing process. The predecessor plan held
   this open as a wave-0 decision; it was decided early, by the architect, on
   the record. → [decision record](decisions/2026-08-21-library-first-topology.md).

2. **Wave 1 restructured at the seam** (Aaron, 2026-08-21). Dump-free spine
   first as **1a**, the wave-2 proof next, the dump-bound trio after as **1b**.
   The predecessor plan carried this explicitly as a question for v2 rather
   than absorbing it. → [decision record](decisions/2026-08-21-dump-free-spine-first.md).

3. **Wave-3 platform priority** (Aaron, 2026-08-21). Manual-install ground
   first; the manager he uses prioritised over polish for the other; that
   manager's reader waits on gate 7a regardless. Recorded as sequencing input
   within wave 3 — it does not move the wave.

4. **The process-adoption list is a wave-0 record** (Aaron, 2026-08-21). Which
   of the reference implementation's rules port, which adapt, and which are
   deliberately declined, was ruled on and executed at wave 0. The declines are
   in §9; the rules themselves are in `CLAUDE.md` and `standards/`. The
   governing principle — a guard is added only after the incident recurs here —
   is why the list has declines at all.

5. **Repository topology: one public repository, corpus untracked** (Aaron,
   2026-08-21). The plan's original "worktree discipline activates when `src/`
   appears" is superseded: `src/` appeared in a **new** repository, and
   discipline was live from the commit after the bootstrap.
   → [decision record](decisions/2026-08-21-public-repo-private-corpus.md).

6. **Gate 8 reframed and closed** (Aaron, 2026-08-21). The upstream licence
   confirmation is **no longer a release gate**. The packages are published
   under a permissive licence and verified as such; the exposure was relational
   rather than legal. It became a non-blocking courtesy heads-up, which Aaron
   sent on 2026-08-21. Any reply is backfilled here.

## 12. Anti-lock

Direction-locked, not detail-locked. Waves 0–2 are specified because they carry
the proof; the rest are sequenced but expected to change shape as wave 2's
experience lands. Sub-step ordering inside any wave is direction, not detail.

A stumbling block invokes `CLAUDE.md` §4 — name the assumption, re-read the
source, surface as (a), (b) or (c). **Never a silent workaround.**

Iteration produces `BUILD_PLAN_v3`. The one thing that does not iterate is the
cornerstone set.
