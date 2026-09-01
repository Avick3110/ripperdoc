# Log attribution: the filename is not the boot, and one framework proves it twice

**Class: ARCHIVE.** Measured 2026-09-01 over three consecutive archived boot-log
corpora, read-only. Corrections supersede in a new document.

**Evidence class: measured.** Every row below comes from files on disk, hashed;
nothing here is read out of a source or inferred from a framework's
documentation.

---

## The law

> **Attribute a log line to a boot by a timestamp inside the log, never by the
> log's filename.**
>
> 1. **One framework rotates its current log and names the rotation after the
>    boot that displaced it, while filling it with the previous boot's
>    content.** The file called `…_r<T>.log` holds the boot *before* `T`.
> 2. **Two more naming families exist and behave differently.** Most frameworks
>    write one file per boot, stamped at creation, and the stamp is honest.
>    Some write a fixed filename carrying no stamp at all.
> 3. **So no single filename rule is correct across families**, and the one
>    rule that is correct everywhere is: parse the first timestamp the file's
>    own head yields, and report a file that yields none as unattributable —
>    by name, never bucketed by guess.
> 4. **Size is not identity here, and the trap is live.** In the measured
>    corpus a rotated log and the current log are the same byte length and
>    different content.

## Why this matters more than it looks

A diagnosis says "this mod failed at this boot". Get the attribution wrong and
every sentence downstream of it is confidently, specifically false — it names
real mods, cites real error text, and blames them for a run that already
happened. That is worse than saying nothing, because it survives review: the
mods are real, the errors are real, and only the boot is wrong.

The trap has a second edge. The rotation is created *by* the new boot, so it
appears with the new boot's timestamp in its name at the moment the new boot
starts. A routine that sorts logs by filename and takes the newest two is
looking at the current boot and a mislabelled copy of the one before it —
exactly the two files most likely to be compared.

## How it was measured

### The instrument

For every log in three consecutive boots' archived corpora: the timestamp
parsed out of the **filename**, the first timestamp parsed out of the file's
own **head**, the modification time, the byte size, and a `sha256`. Three
timestamp grammars are recognised — a bracketed `YYYY-MM-DD HH:MM:SS`, a
long-form `[LEVEL - Ddd, DD Mon YYYY HH:MM:SS]`, and a tab-separated first
column — and a file whose head matches none is reported as such rather than
skipped.

Three boots, one after another, same machine, same install: a failing boot, then
two clean ones.

### The three families, and what each does

| Family | Naming | Files across the three boots | Name vs body |
|---|---|---|---|
| **Per-boot stamped** — the plugin loader and most plugins | `<name>-<YYYY-MM-DD-HH-MM-SS>.log`, one per boot, accumulating | most of the corpus | **honest** |
| **Rotating** — the script compiler | `…_rCURRENT.log` plus `…_r<stamp>.log` rotations | 5 rows, 3 distinct files | **wrong, by hours** |
| **Fixed name** — a few plugins | one filename, no stamp, overwritten | 3 rows | no stamp to be wrong |

### The readings

| | Boot 1 | Boot 2 | Boot 3 |
|---|---|---|---|
| Logs in the stamped-or-rotating families | 18 | 15 | 20 |
| Name stamp **agrees** with body stamp | 15 | 11 | 15 |
| Name stamp **disagrees** | 0 | **1** | **2** |
| Filename carries no stamp | 2 | 2 | 2 |
| Head yields no parseable timestamp | 1 | 1 | 1 |

Across all three corpora: **44 rows carry both a name stamp and a body stamp.
39 agree exactly. 2 differ by one second. 3 disagree by hours** — 5,200 s once
and 13,604 s twice, the repeat being the same rotated file appearing in two
successive corpora unchanged.

**The one-second cases are not noise to be waved away.** The filename is stamped
when the file is created and the first line is written a moment later, so even
the honest family can differ from its own name. The largest such gap measured is
**1 second**. Any tolerance a reader applies is a choice, and it is stated here
rather than buried in an instrument: this measurement used two seconds, and no
row fell between 2 and 5,200.

### The rotation, proven by hash rather than by size

| Corpus | File | `sha256`[16] | Bytes |
|---|---|---|---|
| boot 1 | `…_rCURRENT.log` | `f9e65f1a1b8e066b` | 192,942 |
| boot 2 | `…_r<boot-2 stamp>.log` | **`f9e65f1a1b8e066b`** | 192,942 |
| boot 2 | `…_rCURRENT.log` | `f932f4c5f82e4fec` | 26,193 |
| boot 3 | `…_r<boot-3 stamp>.log` | **`f932f4c5f82e4fec`** | 26,193 |
| boot 3 | `…_rCURRENT.log` | `a30fb4475570340b` | **26,193** |

Boot 1's current log **is** boot 2's rotation, byte for byte. Boot 2's current
log **is** boot 3's rotation, byte for byte. The trap is not a one-off: it
reproduced on consecutive boots, and a filename-keyed routine would have been
wrong about two boots in a row.

**The last row is the size trap in the same table.** Boot 3's current log and
boot 3's rotation are both 26,193 bytes and are different files. A check that
called them the same because their sizes matched would be wrong on this exact
corpus, today.

### The file that cannot be attributed at all

One plugin writes a tab-separated log whose first line is a **column header**.
Its timestamps are real and sit in the first column of every data row, but a
rule reading "the first timestamp on the first line" finds nothing. It is
reported unattributable rather than attributed by its filename — which, in this
one case, would have been right, and that is precisely why the rule cannot be
"fall back to the filename when parsing fails". The fallback would be correct
here and catastrophic on a rotation.

### Every competing explanation, and what killed it

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| Filename stamps are reliable, the rotation is a one-off | **refuted** | It reproduced across two consecutive boot pairs, with the identity proven by hash both times |
| Modification time is the sound key | **not refuted, but not sufficient** | mtime tracks the *last* write, so it is start-plus-duration; on the failing boot it sits 32 s after the boot the log belongs to. It corroborates the body stamp and cannot replace it |
| Only the rotating family needs body-stamp attribution | **refuted** | Three files in the corpus carry no filename stamp at all, and one yields no first-line stamp; both cases need a rule the filename cannot supply |
| Rotations can be identified by size | **refuted** | Two same-size, different-content files in one corpus |

## Compile-failure attribution composes from this

The failing boot's compiler log carries **101 error lines**. 100 of them match
the shape `[<level> - <stamp>] [<CODE>] At <absolute path>:<line>:<column>:`;
the remaining one is the run's summary line and is classified as such rather
than dropped. Six error codes appear, distributed 37 / 33 / 25 / 3 / 1 / 1.

Those 100 errors implicate **34 distinct source files**. All 34 sit under the
deployment target, and the deployment record claims **all 34** — zero source
files the record does not account for. Joining each to its record entry's
`source` yields **12 distinct mods**.

**The compiler independently names culprits, and the two lists are the same
size.** When compilation fails, the log's trailer lists the mods it blames.
That list holds **12** entries; path attribution implicates **12** mods.

**What is not established is that they are the same 12.** The two lists are in
different namespaces — the compiler names the script directory a source sits
in, ours names the manager mod id — so they cannot be compared as written, and
comparing them needs a join that is itself the thing under test. Joining
trailer entries to attributed mods through the record's script directories
resolves **11 of the 12**; the twelfth names a directory the record attributes
to more than one mod, which that join cannot decide. So the honest statement is
that two instruments produced the same count and eleven agreeing pairs, and
that the twelfth pair is unresolved by any route measured here. Closing it is
`BUILD_PLAN_v2` §10 row 20.

Nothing downstream rests on the stronger reading: the engine never consults the
trailer, and attribution runs from the error paths and the record alone.

**Correction to a count in this project's own private record**, made here rather
than by editing an archive: that trailer holds twelve entries, not thirteen.

## Where this stops

**Three boots, one machine, one install, one day.** The rotation law reproduced
twice, which is what makes it a law rather than an anomaly; it is still three
boots.

**Only one framework in the corpus rotates.** Whether any other does, under a
different naming scheme, is not measured — which is exactly why the rule is
"parse the body" rather than "special-case the one that rotates".

**The trailer cross-check exists only for failing compiles, and it is partial.**
A successful compile writes no trailer, so on a clean boot the path attribution
has no second instrument to agree with. On the failing case the two instruments
agree on the count and on eleven of the twelve pairs, with the twelfth
unresolved (`BUILD_PLAN_v2` §10 row 20) — so what
the second instrument corroborates is most of one reading, not all of it.

**The set of timestamp grammars is this reader's, not the frameworks'.** Three
were declared and each is exercised by the corpus. A framework writing a fourth
would be reported unattributable — the safe direction — but nothing here can
enumerate what the frameworks might write, and no derivation inside this project
could.

## What this means for a tool

Never key attribution on a log's filename. Parse the body, declare the grammars
you parse, and name the files you could not attribute instead of quietly
assigning them. Where a second instrument exists — the compiler's own list of
blamed mods — run it and compare, because two instruments agreeing is the only
cheap evidence that either is right.
