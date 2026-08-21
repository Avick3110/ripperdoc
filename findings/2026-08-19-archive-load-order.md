# Archive load order: `modlist.txt` is honoured, and the first-loaded archive wins

**Class: ARCHIVE.** Measured on game version 2.31, across two probe passes
completing 2026-08-19. Corrections supersede in a new document.

---

## The law

> **Archive-layer precedence, game 2.31 — the first-loaded archive wins,
> always.**
>
> 1. **With no `modlist.txt`:** every `.archive` in the mod directory loads in
>    ASCII-alphabetical order by filename.
> 2. **With a `modlist.txt` present:** listed archives load first, in the order
>    they are listed; then every unlisted archive. **Being listed outranks any
>    filename.**
>
> A resolver that treats `modlist.txt` as a whitelist, or that merges unlisted
> archives into alphabetical order, reports the wrong winner.

## Why this matters more than it looks

**Adding a mod to an install that has a partial `modlist.txt` silently demotes
it below every listed mod.** Not because of its name — because it is not on the
list. The mod loads, it works, and it loses every conflict it has with anything
listed. Nothing reports this.

That is the opposite of what the common renaming instinct expects. Prefixing an
archive filename with `!` to promote it does nothing at all if the competing
archives are listed and yours is not.

**And the instinct is inverted again one layer over.** In the archive layer the
**first**-loaded file wins. In the tweak layer the **last**-applied file wins
([see the tweak-file finding](2026-08-19-tweak-file-order.md)). The same rename
that promotes an archive demotes a tweak file. Two frameworks, two opposite
rules, both silent.

**A community source claiming `modlist.txt` is ignored after game version 2.0
is wrong** for 2.31. The official documentation is correct on this point. This
was checked by measurement rather than by weighing citations, because the two
sources disagreed and one of them had to be wrong.

## How it was measured

### The instruments

Purpose-built archives that conflict on **exactly one asset** — a single
vanilla localisation entry — each overriding it to a distinct, unmistakable
string. Localisation was chosen because the resulting value is readable
mechanically at runtime, so no observation depends on anyone looking at a
screen and deciding what they saw.

The detector reads the resolved string **twice per boot**: once at
initialisation and again ten seconds later. Both reads agreed on every boot in
both passes.

### Pass one — is the file honoured, and in which direction?

| Boot | `modlist.txt` | Predicted | Observed |
|---|---|---|---|
| 1, control | absent | A wins on filename order | **A** |
| 2a | lists B, then A | B ⇒ file is honoured | **B** |
| 2b | lists A, then B | A ⇒ first-listed wins | **A** |
| 3, revert | deleted | back to A | **A** |

**The confound that was deliberately designed out.** A single experimental boot
listing `B, A` would have read **A** under *both* "the file is ignored" **and**
"the file is honoured, last-listed wins" — a confident-looking result that could
not distinguish the two. Splitting into 2a and 2b separates them and pins the
direction in the same stroke.

### Pass two — what happens to archives that are *not* on the list?

The second pass added a **presence detector**: a third archive, never listed in
any `modlist.txt`, carrying its own **uncontested** file.

That detail is load-bearing. Archive shadowing is **whole-file** — an archive
that loses a contested file contributes *nothing* to it. So an unlisted archive
that loaded but lost would be indistinguishable from one that never loaded at
all, if you only watched the contested asset. The uncontested file makes
"loaded" visible independently of "won".

| Boot | contested asset | presence detector |
|---|---|---|
| control, no list | **A** | **present** |
| X — B listed; A and the detector unlisted | **B** | **present** |
| Y — A listed; B and the detector unlisted | **A** | **present** |
| revert | vanilla | vanilla |

### Every competing explanation, and what killed it

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| `modlist.txt` is a **whitelist** — unlisted archives do not load | **refuted** | The presence detector was never listed, and its file was live in every boot |
| Unlisted archives are **merged into alphabetical order** with listed ones | **refuted** | This predicts the unlisted A beats the listed B in boot X. It did not — B won |
| Unlisted archives load **first** | **refuted** | Same boot, same reasoning, opposite direction |
| **Unlisted archives load after every listed archive** | **confirmed** | The only hypothesis consistent with all four boots |

### Cache defence

The contested asset ran **A → B → A → vanilla** across the boots, and the
presence detector ran **present → present → present → vanilla**. A stale cached
value cannot flip back in step with files being deleted, and the X↔Y flip turns
on the order of **one line in a text file** with the archives themselves
byte-identical between the two boots. Nothing here is a caching artefact.

Every reading's timestamp was cross-checked against its boot timestamp, so no
reading is attributed to the wrong boot. The list file's bytes were verified by
hex dump both times.

## Where this stops

**Ordering among the unlisted archives themselves is untested.** In both boots
of pass two, the unlisted archives did not contest any shared file, so their
relative order was unobservable. ASCII order is the natural guess. **It is a
guess** — it is not measured, and nothing here should be read as measuring it.

This does not weaken the listed-versus-unlisted result, which is measured
directly.

## What this means for a tool

A resolver that wants to report the true winner for a contested file must read
`modlist.txt` when it exists, honour listed order, and place unlisted archives
after all listed ones. Deriving precedence from filenames alone is correct only
for installs with no list at all — and it fails **silently** everywhere else,
which is the worst way for it to fail.
