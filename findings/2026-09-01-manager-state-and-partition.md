# The deployment manager's on-disk state: what it knows, and what joins to what

**Class: ARCHIVE.** Measured 2026-09-01 against a Vortex 2.5.0 instance and two
archived deployment records, read-only throughout. Corrections supersede in a
new document.

**Evidence class: measured**, except where a paragraph says otherwise. Two
statements in "Where this stops" are explicitly *not* measured and say so.

---

## The law

> **1. Only the manager knows what was wanted.** The game directory can be
> enumerated, but nothing in it records how many mods were *meant* to be there.
> That number lives in the manager's own state and nowhere else.
>
> **2. For Vortex, that state is a LevelDB**, not a document. Keys are
> `###`-separated paths, values are JSON. The wanted set is
> `persistent###profiles###<profileId>###modState###<modId>###enabled`,
> filtered to the profile whose `gameId` names the game.
>
> **3. One identity carries the whole join.** The mod id *is* the staging
> directory name, *is* `installationPath`, *is* the `source` field of every
> entry in the deployment record. A tool that invents a second identity — a
> display name, a Nexus id, a version string — has invented a join that can
> drift.
>
> **4. Deployed-to-wanted is only computable through the deployment record.**
> The manager deploys by hard link, so a deployed file carries no mark of which
> mod supplied it. `vortex.deployment.json` at the game root is the only thing
> that maps a deployed path back to a mod. **Absent that file, "deployed" is
> not unknown-and-assumable — it is unknown, and must be reported as such.**

## Why this matters more than it looks

A tool that reads only the game directory can tell you what is present. It
cannot tell you what is *missing*, because absence has no representation in a
directory. On the sample measured here that distinction is the difference
between "your install is fine" and "350 mods you asked for were never enabled":
the game tree was internally consistent in both cases.

And the identity point is not pedantry. The mod id is a composite string built
from a display name, a numeric id, a version and a timestamp. It changes when
the mod is updated. Every one of its four parts is also available separately,
and every one of them is a worse key: display names collide, versions repeat,
and the numeric id is shared by every file of a mod. The manager already picked
one identity and used it in all three places. A reader that picks a different
one is choosing to be wrong later.

## How it was measured

### The instrument

A read-only LevelDB reader written for this characterisation: write-ahead log
framing, SSTable block decoding, snappy decompression, and a newest-sequence
-wins merge across every table and log in the database. It reads **bytes from a
copy** and never opens a database, because opening one replays the log and can
compact — a write to the state under test.

The copy was taken with the manager not running, and verified by `sha256` per
file against the source before anything read it. Nine files, nine matches.

### What the database holds

| Namespace | Live keys | What it is |
|---|---|---|
| `persistent###downloads###files###…` | 19,673 | download records |
| `persistent###mods###<gameId>###…` | 11,674 | per-mod records, one game |
| `persistent###profiles###<profileId>###…` | 855 | the profile under test |
| `persistent###loadOrder###<profileId>` | 1 per profile | an ordered list |
| `persistent###deployment###…###<gameId>` | 2 per game | counter, needs-deploy flag |
| `persistent###collections###…` | 49 | collection and revision metadata |

40,942 distinct keys, 40,939 of them live. The reader was cross-checked against
a raw byte scan of the write-ahead log, which finds the same key namespaces; the
scan cannot see the compressed tables, which is why it is not the instrument.

**One namespace was deliberately not read**, and is named here rather than left
to be noticed: the database also holds account credentials. The instrument
enumerates key names and reads none of that subtree.

### The wanted set

| | |
|---|---|
| Profiles in the database naming this game | 2 |
| Profile under test — `modState` entries | **284** |
| Of those, `enabled == true` | **284** |
| Of those, `enabled == false` | 0 |
| Per-mod records for the same game | **284** |
| Ids in `modState` but not in the mod records | **0** |
| Ids in the mod records but not in `modState` | **0** |
| Mods whose `installationPath` equals their id | **284 of 284** |

### The partition, against a deployment record from the same state

| | |
|---|---|
| Wanted | **284** |
| Deployed — distinct `source` values in the record | **283**, over 1,913 files |
| **Wanted and deployed** | **283** |
| **Wanted and missing** | **1** |
| **Wanted and unresolvable** | **0** |
| **Deployed but unclaimed** | **0** |
| Record `source` values the manager knows | **283 of 283** |

**The single missing mod is missing for a structural reason the reader can
state**: its record carries `type: collection`. It is the manifest container for
a curated list, it declares no deployable content, and it deploys nothing by
construction. That is the difference between a partition with a reason attached
to every bucket and one that reports a bare count and leaves the reader to guess
whether they have a problem.

Deployed files land across eight top-level directories, two of which are not
part of the game's own layout. A resolver enumerating "the known lanes" would
miss those two.

### Every competing explanation, and what killed it

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| The wanted set is derivable from the staging directory | **refuted** | Staging directory names give installed mods, not enabled ones, and carry no per-mod manifest — a sweep of every staging directory found manifests only in the collection container |
| The wanted set has a document form somewhere | **refuted** | The per-profile directories are empty; `snapshots/snapshot.json` is two bytes, `[]`; no JSON under the manager root carries profile or mod state |
| Deployed-to-mod can be joined without the record | **refuted for path-based joins** | Deployment is by hard link; the deployed path and the staging path share content but the game tree stores no mod attribution |
| The mod id is unstable across the three sites | **refuted** | 284 of 284 `installationPath` matches, and 283 of 283 record `source` values resolve |

## Ordering metadata — the shape, and a result that did not come out as expected

Ordering intent has two homes, and neither is the game directory:

| Home | Shape | Read on the samples |
|---|---|---|
| The collection manifest's `modRules` | pairwise `before` / `after`, each side naming a **file** by expression, hash, logical name and version match | 2,221 rules on the large sample; 5 on the small one |
| The manager's per-mod `rules` | pairwise, each side naming a **mod** by internal id, archive id or hint | 288 edges on the live state, 283 of them `requires` |
| `loadOrder` for the profile | an ordered list | **empty**, both samples |

**A cycle check over these inputs was run, and found none.** This is stated
plainly because the expectation was the opposite:

| Input | Rules | Cycles |
|---|---|---|
| Large sample, collection `modRules`, keyed as the prior instrument keys them | 2,221 | **0** |
| Same, keyed by file hash alone | 2,221 | **0** |
| Same, each side joined to the declared mod it names | 2,217 | **0** |
| Small sample, collection `modRules` | 5 | **0** |
| Live state, per-mod `rules` | 288 | **0** |

Rule sides carry a uniform field set in 4,420 of 4,442 endpoints, so the keying
choice is not hiding a cycle behind a split node — and the three keyings were
run precisely to test that.

**The manager itself reported cycles on the large sample**, three times, and
interrupted deployment. Its log never names a member, an edge or a path. The
state it was checking no longer exists: every key version in the database was
scanned, not only the newest, and no record of that sample survives.

**So the honest statement is a negative one.** The ordering metadata that is
readable before deployment was measured acyclic on the one sample whose
deployment a cycle interrupted. Either the manager's graph carries edges that
none of these three inputs contain, or it uses a different node identity than
any of the three tried here. **Which of those is true is not established**, and
nothing in this document should be read as establishing it.

## Where this stops

**The cycle that interrupted a real deployment was not reproduced.** It is not
refuted either — the manager's warning is a real log line from a real run. What
is measured is that it is *not derivable* from any ordering metadata still on
disk. A check built on these inputs is a real check on a real graph; it is not
evidence that it would have caught that failure, and it must not be described as
though it were.

**One manager, one game, two samples.** Everything above is the on-disk shape of
a single manager at a single version. The other deployment manager in common use
stages through a virtual file system and puts nothing on disk to read; its
characterisation has not been run, and every claim here is silent about it
rather than covering it.

**The partition was measured against an archived deployment record, not a live
one.** The instance measured is not currently deployed: its state says a
deployment is not needed and the deployment record is absent from the game tree.
That combination — the manager believing itself deployed while the record it
would have written is gone — is itself a state a diagnosis has to be able to
report, and it is the state the live bench is in.

**`enabled == false` was never observed.** Every entry in the profile under test
is enabled, so the disabled branch of the wanted-set filter is read from the key
shape rather than from a reading that exercised it.

## What this means for a tool

Read the manager before judging the game directory, and key everything on the
one identity the manager already uses in three places. Report the partition with
a reason on every bucket, never a bare count. And where the deployment record is
absent, report the deployed side as unreadable rather than computing a
difference against an empty set — the arithmetic works and the answer is a lie.
