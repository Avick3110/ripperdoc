# The manager's state database: which profile is active, and what of the format a reader must model

**Class: ARCHIVE.** Measured 2026-09-02 against the same Vortex 2.5.0 instance
as [the 2026-09-01 finding](2026-09-01-manager-state-and-partition.md), read-only
throughout. That document is not edited; this one adds what it left unmeasured
and **corrects one figure it carries**. Corrections supersede in a new document.

**Evidence class: measured**, except for two paragraphs that say otherwise by
name — the in-use signature, which is not measured, and the disabled branch of
the wanted-set filter, which is read from key shape rather than from a reading
that exercised it.

---

## What this adds

The 2026-09-01 finding established the key space, the identity law and the
shape of the ordering metadata. It read "the profile under test" by hand, said
nothing about the on-disk format below the key space, and named the collection
manifest without saying where a tool would find one. A reader built on it
therefore had five open questions, and they are the five here.

## How it was measured

A per-file `sha256`-verified scratch copy of the state directory, taken with
the manager not running: **nine files, nine matches**. The copy is
byte-identical to the one the 2026-09-01 finding read — every one of the nine
hashes is the same — so the two documents describe the same bytes and the
figures below are directly comparable to that document's.

The instruments are the preserved phase-1 Python readers plus four written for
this pass. **No instrument opens a database**; every one reads bytes from the
copy. Nothing under the credentials namespace is decoded: it is enumerated by
key name and counted, and that is all.

---

## 1. The profile selection law

> **The manager records the active profile per game, and a reader keys on that.**
>
> `settings###profiles###lastActiveProfile###<gameId>` names the profile id
> that game last had active. It resolves to a profile whose own
> `persistent###profiles###<profileId>###gameId` names that same game, and a
> reader that finds otherwise has found a state it must refuse rather than
> resolve.

Measured, for the game under test:

| Key | Resolves to |
|---|---|
| `settings###profiles###lastActiveProfile###<gameId>` | the profile carrying **284** `modState` entries |
| `settings###profiles###activeProfileId` | the same profile |
| `settings###profiles###nextProfileId` | the same profile |

Two profiles name this game: one with **284** `modState` entries and one with
**1**. The selection key resolves to the 284-entry profile — **the one the
2026-09-01 finding measured**, which is what makes that finding's hand-picked
profile the one a tool would pick by itself.

**Three keys agree on this bench, and only one of them is the right one.**
That agreement is a property of this bench, not of the format, and a reader
choosing among them by what happens to work here would be choosing on a
coincidence. Two of the three are wrong by construction:

- `activeProfileId` is **not keyed by game**. It names whichever profile is
  active across the whole manager, and this bench manages a second game whose
  own `lastActiveProfile` entry names a different profile — so on a machine
  whose owner last played that other game, reading `activeProfileId` for this
  one returns a profile belonging to another game entirely.
- `nextProfileId` names a profile the manager is moving to, not the one whose
  state the mods on disk belong to.

`lastActiveProfile` is keyed by game and carries an entry for each game the
manager manages. That is why it is the law and the other two are not.

**Where this stops.** One instance, two games. That `activeProfileId` would
diverge from `lastActiveProfile###<gameId>` under a different game's session is
read from the key shape — the second game's own entry naming a different
profile — and not from a reading that observed the divergence.

### What a reader does when the key is not there

No heuristic. If the selection key is absent, or names a profile that does not
exist, or names one whose `gameId` is a different game, the reader **returns
every profile naming the game and refuses to pick**. Picking the most recent,
the largest, or the first would be inventing the second identity the
2026-09-01 finding's identity law warns against — this time an identity for
*which state* rather than for which mod.

## 2. The in-use signature — NOT MEASURED

**This is the one deliverable of this pass that is not measured, and it is
named rather than left to be noticed.**

A LevelDB directory always carries a `LOCK` file, and this one does: zero
bytes, last written months before any other file in the directory. **Its
presence proves nothing about whether a manager is running.**

What a *running* manager changes on disk that a stopped one does not has not
been measured, because measuring it requires the manager to be running and
this engagement starts nothing. The measurement is requested and the window is
the owner's to give.

Until it lands:

- The reader's in-use arm is **labelled unmeasured in its own provenance**. It
  does not claim to detect a running manager and it does not claim one is not
  running.
- The check for that arm is the one written when the measurement lands, not
  before. A check written against an unmeasured signature would assert this
  document's guess.

The hypothesis to be tested, stated so that the measurement can refute it: the
platform's own file lock on `LOCK` is held exclusively while the manager runs,
so an attempt to open `LOCK` for reading — creating nothing, writing nothing —
fails with a sharing violation while it runs and succeeds while it does not.
**Nothing here establishes that**, and a reader shipped on it would be shipping
a guess as a guard.

## 3. The disabled branch — from key shape, not from a reading

`enabled == false` was not observed on 2026-09-01 and is not observed here:
every one of the profile's 284 `modState` entries is `true`. The key shape is:

```
persistent###profiles###<profileId>###modState###<modId>###enabled       -> true | false
persistent###profiles###<profileId>###modState###<modId>###enabledTime   -> number
persistent###profiles###<profileId>###modState###<modId>###disabledTime  -> number
```

All three leaf names occur under the profile under test; `disabledTime` occurs
beside entries that are currently `true`, which is consistent with a mod having
been disabled and re-enabled and is not a reading of a disabled one.

So the disabled branch of the wanted-set filter is fixtured **from this key
shape** and the fixture says so in its own name. It is not evidence that a
disabled mod reads as disabled on a real instance.

## 4. The modelled subset — the reader's contract

Every value in the table below is one this bench exercises. **Every byte value
outside it is refused by name**: the reader states which file, which construct
and which value it did not model, and stops. Nothing outside the table is
guessed at, best-effort decoded, or skipped.

| Construct | Modelled | Seen on this bench |
|---|---|---|
| `CURRENT` | one line naming a manifest file in the same directory | 1 |
| Manifest / log framing | 32 KiB blocks, 7-byte record header | — |
| Log record type | `0` zero-pad, `1` full, `2` first, `3` middle, `4` last | log: 66 / 58 / 14 / 58; manifest: 9 full |
| Record checksum | CRC32C, masked | 196 log + 9 manifest + 599 table, **all matching** |
| Batch entry kind | `0` delete, `1` value | 9,926 value, 3 delete in the log; 33,588 value in the tables |
| Version-edit tag | `1` comparator, `2` log number, `3` next file, `4` last sequence, `5` compact pointer, `6` deleted file, `7` new file, `9` previous log number | 1 / 8 / 8 / 8 / 6 / 22 / 25 / 8 |
| Comparator name | `leveldb.BytewiseComparator` and nothing else | 1 |
| Table footer | 48 bytes, magic `0xdb4775248b80fb57` | 3 tables, 3 matches |
| Block compression | `0` none, `1` snappy | 3 none, 596 snappy |
| Snappy | literal tags and all three copy tags | — |

**Tag 8 is deliberately absent from the modelled set.** The format assigns it to
a construct that no longer exists, this bench carries none, and a reader that
skipped it would be skipping something it cannot describe.

### What the manifest decides, and why the reader reads it

The preserved phase-1 instrument takes every `.ldb` and `.log` in the directory
by glob. That is sound on this bench and unsound in general: a table the
manifest has dropped is still on disk until the manager deletes it, and reading
one resurrects whatever it holds.

So the reader reads `CURRENT`, then the manifest it names, then applies its
version edits in order, and reads **only** the tables the accumulated edits
leave live plus the logs the edits name. On this bench that is three tables at
levels 0, 1 and 2, and one write-ahead log — **the same four files the glob
takes**, which is why the two instruments agree here and why the agreement is
not evidence that a glob is correct.

### Newest sequence wins, and a delete is an absence

Every entry carries a sequence number; across every live table and log the
highest sequence for a key is the one that stands. An entry whose kind is
delete makes the key **absent**, not present with the value it had before.
On this bench: **40,942 distinct keys, 40,939 live** — three deletes,
reproducing the 2026-09-01 figure exactly.

### One namespace is never read

`confidential###account` — **3 keys**, enumerated by name and never decoded.
The reader materialises a value only for a key under a prefix it needs. That is
a claim about the code and not a promise in prose, so it ships with a check: a
fixture carries a credential-shaped key beside the modelled ones, and the check
reads that the reader saw the key and materialised no value for it.

## 5. The collection manifest — where it is, and how its sides join

> **The manifest sits in the collection container's own staging directory**, at
> `<staging root for the game>/<the mod id of the mod whose type is
> `collection`>/collection.json`, and both halves of that path are readable out
> of the state database: the staging root from
> `settings###mods###installPath###<gameId>`, the container from the one mod
> whose `type` is `collection`.

Measured: the game's staging root holds **284** subdirectories and exactly
**one** `collection.json` within two levels of it — in the directory named by
the single mod the state gives `type: collection`. That mod is the one the
2026-09-01 finding identified as deploying nothing by construction, which is
the same fact seen from the other side.

### The shape

| Where | Field | Carries |
|---|---|---|
| manifest root | `mods`, `modRules`, `loadOrder`, `info`, `tools`, `collectionConfig` | — |
| `mods[]` | `source.logicalFilename`, `source.md5`, `source.modId`, `source.fileId`, `name`, `version`, `optional`, `phase` | the declared mod |
| `modRules[]` | `type` (`before` / `after`), `source`, `reference` | one pairwise rule |
| a rule side | `fileExpression`, `logicalFileName`, `fileMD5`, `versionMatch` | a **file**, never a mod id |

**The two spellings differ in case and a reader must not assume they do not.**
A rule side spells it `logicalFileName`; a declared mod spells it
`source.logicalFilename`. They are the same value and two different keys.

### The join, and its residue

A rule side names a file; the graph needs a mod. The join takes each side to
the declared mod it names, in this order — the order the 2026-09-01
characterisation used:

1. `fileMD5` against `mods[].source.md5`
2. `logicalFileName` against `mods[].source.logicalFilename`
3. `logicalFileName` against `mods[].name`
4. `fileExpression` against `mods[].source.logicalFilename`

A side that reaches the end of that list joins to nothing. **It is residue and
it is labelled**, never assigned an invented identity: a side given a made-up
node is the second identity that hides a cycle behind a split node.

Measured over the two archived manifests and the live one:

| Sample | Declared mods | Rules | Sides | Sides joined | Rules with an unjoinable side |
|---|---|---|---|---|---|
| large | 2,519 | 2,221 | 4,442 | **4,419** | **23** |
| small (archived) | 283 | 5 | 10 | 10 | 0 |
| small (live staging) | 283 | 5 | 10 | 10 | 0 |

The 23 unjoinable sides are all `reference` sides naming a file that this
manifest's own `mods` list does not declare. A curated list may reference a mod
it does not itself ship; the join has nothing to resolve it against, and the
honest report is that it did not resolve.

### The second join: a declared mod to the manager's own id

The join above ends at a mod the **manifest** declares, and a manifest declares
mods by file, not by the identity the manager keys everything else on. A rule
set stopping there would sit beside the manager's own rules as a **disjoint
graph**, and a cycle running through both would be one neither half could see.

So each declared mod is carried one step further, to the manager's mod id:

| Route | Manifest side | Manager side | Index | Ambiguous | Declared mods joined |
|---|---|---|---|---|---|
| file hash | `mods[].source.md5` | `attributes###fileMD5` | 284 | **0** | **283 of 283** |
| file id | `mods[].source.fileId` | `attributes###fileId` | 283 | **0** | **283 of 283** |
| logical name | `mods[].source.logicalFilename` | `attributes###logicalFileName` | 284 | 0 | 282 of 283 |
| mod id | `mods[].source.modId` | `attributes###modId` | 273 | **9** | 283 of 283 |

**The hash is the route, with the file id behind it.** Both are exact and
neither is ambiguous on this bench. The last two are named to say why they are
not used: the logical name misses one, and the mod id is a page rather than a
file — nine of them name more than one installed mod, and a spelling two mods
answer to identifies neither.

A spelling that names more than one mod is **dropped from the index and
reported**, not resolved to whichever was read first. A declared mod that joins
to nothing is a mod the manager never installed, and it is counted rather than
given a node under the manifest's own spelling.

The manager's per-mod attributes carry 22 names on all 284 mods and 13 more on
283 of them; only the four above are read, and the join is stated as this
bench's, not the format's.

### A correction to the 2026-09-01 finding

That document's ordering-metadata table reads:

| Input | Rules | Cycles |
|---|---|---|
| Same, each side joined to the declared mod it names | 2,217 | 0 |

**2,217 is a distinct-edge count, not a rule count.** Its instrument reports
`edges=`, and the column it was carried into is headed `Rules`. Reproduced on
the same file: the joined keying gives **345 nodes and 2,217 distinct edges**
over **2,221 rules** — the join merges nodes that the other two keyings keep
apart (348 nodes down to 345), and **4 rules thereby become duplicates of edges
already in the graph**. Four duplicate edges; not four unjoinable sides.

The join yield for the same sample is **4,419 of 4,442 sides**, leaving **23**
rules with an unjoinable side, as tabulated above.

**Nothing downstream of the figure changes.** The cycle verdict is 0 under all
three keyings, before and after; the duplicate edges are duplicates of edges
already present, so they add nothing a cycle could run through. What changes is
what a reader building on the figure would expect its own join to yield — 4
residues rather than 23 — and a reader who found 23 would have had reason to
believe its join was broken.

## Where this stops

**One manager, one version, one instance, three manifests.** Every figure above
is the on-disk shape of a single Vortex 2.5.0 install and two manifests archived
from it. The other deployment manager in common use is not characterised and
nothing here is silent about it by accident — it stages through a virtual file
system and gate 7a is where that question lives.

**The format's modelled subset is this bench's exercise of it, not the
format's definition.** A construct the format permits and this bench does not
contain is one the reader refuses by name — which is the correct behaviour and
is also an admission: the reader has never been shown a database that uses it.
A second instance would widen the table or refuse, and either outcome is
information.

**The in-use signature is unmeasured** (§2), and the reader says so about
itself rather than leaving a caller to assume it was checked.

**The disabled branch is unexercised** (§3). It is fixtured from key shape and
the fixture is named for that.

## What this means for a tool

Read `CURRENT` and the manifest, not a glob. Take the active profile from the
per-game key and refuse to guess when it is not there. Materialise a value only
under a prefix you need. Refuse every byte value outside the table in §4 by
name. Carry both rule homes onto the manager's own identity, so that the graph
they share is one graph. And where a rule side joins to no declared mod, or a
declared mod to no installed one, say so and count it — the residue is the
measurement, and an invented node is the thing that turns a cycle check into a
coin flip.
