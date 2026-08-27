# Script annotations: the last `@replaceMethod` wins, and the warning names the winner

**Class: ARCHIVE.** Measured on redscript's standalone compiler as shipped with
game version 2.31, across probe passes completing 2026-08-27. Corrections
supersede in a new document.

---

## The law

> **Script-layer precedence, redscript on game 2.31.**
>
> 1. **Source order is the directory index's own order** — a case-insensitive
>    comparison on the **uppercased** name — and a subdirectory is walked **in
>    place**, at the position its own name holds among its siblings. Files in
>    the root and files in subdirectories interleave; they are not grouped.
> 2. **The compile set is not only `r6/scripts`.** Runtime-extension plugins
>    contribute `.reds` of their own, and those are appended **after** the whole
>    `r6/scripts` walk.
> 3. **Among `@replaceMethod` annotations on one method, the LAST in that order
>    wins.** Every replacement after the first raises a warning, and the
>    warning is attached to **the annotation that wins**, not the one that loses.
>    The losing body's code is never emitted.
> 4. **`@replaceMethod` and `@wrapMethod` do not compete.** Their relative
>    position makes no difference: the surviving replacement becomes the body,
>    and wraps wrap it, whichever came first in source order.
> 5. **A `@wrapMethod` whose body never calls `wrappedMethod()` compiles
>    silently** — no error, no warning.
>
> A resolver that reports the *first* replacement as the winner, or that reads
> only `r6/scripts`, names the wrong mod.

## Why this matters more than it looks

**The warning blames the wrong mod.** When two mods replace one method, the
compiler emits one warning, it names a file, and that file is **the one that
took the method**. The text — *"this method replacement overwrites a previous
annotation targeting the same method"* — is accurate, and it is read by almost
everyone as naming the problem mod. The mod that actually lost its
functionality is **never named anywhere**. Nothing in the log identifies it.

**And a mod can lose without anything at all being said.** The warning fires
once per extra replacement, so with three mods on one method the first is
silently overridden and only the second and third are mentioned. A mod whose
`@replaceMethod` lost is a mod that installed cleanly, compiled cleanly, and
does nothing — the exact class of silent wrong answer that is worth building a
tool to end.

**Two widely-repeated claims are wrong.** Both were carried into this project's
own research corpus before being measured here:

| Claim in circulation | Measured |
|---|---|
| "A second `@replaceMethod` **loses**" | It **wins**. The last one in source order takes the method |
| "...with a warning that does not say which mod won" | The warning **does** name a file — the winner's. It is the loser that is unnamed |

**The layer inversion, now three deep.** Each layer of this game resolves
conflicts by a different rule, and none of them announces it:

| Layer | Who wins |
|---|---|
| Archives | **First** loaded ([finding](2026-08-19-archive-load-order.md)) |
| Tweak files | **Last** applied ([finding](2026-08-19-tweak-file-order.md), [superseded](2026-08-22-tweak-file-order-groups.md)) |
| **Script replacements** | **Last** in source order |

Renaming a file to sort earlier promotes an archive, demotes a tweak file, and
**demotes a script replacement**. The same instinct is right in one layer out of
three.

## How it was measured

### The instrument

The compiler that ships with the game, run **standalone** against synthetic
`.reds` sources authored for this measurement. Each cell compiled its own
sources against a **copy** of the game's untouched base script blob, taken by
file copy and verified identical by sha256, with output written to scratch.

**Nothing was launched, and the game directory was not written to** — with one
exception, recorded here because a finding that hid it would be worth less: the
compiler writes a small timestamp sidecar next to whatever base blob it is
given, so the first probe, which pointed at the installed blob directly,
overwrote that sidecar. Every subsequent cell used a copy, which moves the
sidecar into scratch. This is a property of the instrument worth knowing before
anyone repeats the measurement.

Target methods were chosen with the help of generated type information, purely
to pick vanilla methods whose exact signatures would compile — **an instrument
convenience only**. Nothing in the law depends on it, and the capability this
measurement supports reads plain text and needs no such input.

**The observable, and why the obvious one does not work.** String literals are
**not** a usable signal: every parsed literal reaches the compiled blob whether
or not its body survives, so both competing markers are present in every
collision cell, at byte offsets that track source order rather than the winner.
The observable that does work is **emitted code size**. Competing bodies were
built to differ by roughly eleven kilobytes of *arithmetic* — deliberately not
strings — so that the surviving body is identified by how much bytecode the
blob gained.

### Question 1 — which `@replaceMethod` wins?

Two bodies, one method, **swapped between the same two file names**. The swap is
what separates "the later file wins" from "one of these bodies is special":
under the first hypothesis the two cells keep different bodies, under the second
they keep the same one.

| Cell | `a_one.reds` | `b_two.reds` | blob growth | Body kept |
|---|---|---|---|---|
| control | small | — | +39 | small |
| control | large | — | +10,918 | large |
| **swap A** | small | **large** | **+11,168** | **large** |
| **swap B** | **large** | small | **+322** | **small** |

Both experimental cells kept the body in `b_two.reds`. **The later file wins,
and the body itself is irrelevant.** The loser costs a few hundred bytes of
pooled strings and debug entries — its code is not emitted at all.

### Question 2 — what does the compiler say?

| Cell | Replacements | Warnings raised, by file |
|---|---|---|
| two-way | `a_one`, `b_two` | `b_two` only |
| two-way, swapped | `a_one`, `b_two` | `b_two` only |
| three-way | `a_rep`, `b_rep`, `c_rep` | `b_rep` **and** `c_rep` |

Every replacement after the first warns. Cross-referenced with question 1, the
file named by the **last** warning is the winner; the first replacement, which
is one of the losers, is never named.

### Question 3 — replaces and wraps together

| Cell | Sources | blob growth |
|---|---|---|
| control | large wrap alone | +11,441 |
| wrap **then** replace | large wrap, then small replace | **+11,392** |
| replace **then** wrap | small replace, then large wrap | **+11,392** |

**Byte-identical under both orderings.** The two annotation kinds are resolved
in separate phases rather than sequentially against a running result: the
surviving replacement supplies the body and wraps wrap it, regardless of which
appeared first. Adding a second, losing replacement to the wrap cell grew it by
a further 258 bytes — pooled strings and a debug entry, again with no body.

**This one refuted the prediction written before it ran.** The design predicted
that a replacement landing after a wrap would discard that wrap's code, on a
sequential model that the collision result had made plausible. It does not. The
prediction was wrong, which is why the remaining open question below is left
open rather than filled in by the same reasoning.

### Question 4 — source enumeration order

Nine sources across a root and three subdirectories, one nested two deep:

| Position | Path |
|---|---|
| 1 | `1digit.reds` |
| 2 | `Alpha\inner.reds` |
| 3 | `Alpha\Nested\deep.reds` |
| 4 | `A_first.reds` |
| 5 | `beta\inner.reds` |
| 6 | `m_root.reds` |
| 7 | `zeta\inner.reds` |
| 8 | `z_last.reds` |
| 9 | `_underscore.reds` |

Two details decide the rule. `Alpha\` sorts **before** `A_first.reds`, and
`_underscore.reds` sorts **last** — after `z_last.reds`. Both follow from
comparing **uppercased** names (`ALPHA` before `A_FIRST` because `L` precedes
`_`; `_UNDERSCORE` last because `_` follows `Z`). A comparison on lowercased
names puts `_underscore.reds` first instead, and a plain byte-ordinal
comparison puts `A_first.reds` before `Alpha\`. Neither matches.

Subdirectories are recursed **in place**: positions 2–3 sit between `1digit` and
`A_first`, not gathered at either end.

### The cross-check — does this predict a real install?

The rule above was derived entirely from synthetic sources in scratch. It was
then used to **predict** the order printed in the compile log of a real modded
install's own launch, from a directory walk of that install's script tree.

| | |
|---|---|
| Files predicted from the tree | 220 |
| Positions predicted correctly | **220 / 220** |
| Files the log actually listed | **232** |
| Predicted files the log omitted | 0 |

**Exact on every position it covers**, which is what makes the rule usable
rather than merely consistent.

The twelve-file gap is the second half of the law. Those twelve are **not**
under the script directory at all: they are contributed by runtime-extension
plugins, appended after the entire walk, as absolute paths. Within one plugin
they are **not** in name order — one of them lists a file beginning `p` before
one beginning `m` — so plugin scripts arrive in the order the plugin registers
them, not in a walked order.

*Single sample.* The counts come from one install and describe that install, not
installs in general. What generalises is that the compile set has a second
source and that it lands last.

### What would have refuted each result

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| The **first** replacement wins | **refuted** | The swap pair: both cells kept `b_two.reds`'s body |
| One of the two bodies is special (size, content) | **refuted** | Same two bodies, swapped, opposite outcomes |
| A losing body is still emitted somewhere | **refuted** | Swap B grew by 322 bytes where the large body costs 10,918 |
| Replaces and wraps resolve sequentially in source order | **refuted** | Both orderings produced byte-identical output |
| Enumeration is byte-ordinal | **refuted** | `Alpha\` precedes `A_first.reds` |
| Enumeration is case-insensitive on **lowercased** names | **refuted** | `_underscore.reds` is last, not first |
| The rule is an artefact of synthetic fixtures | **refuted** | It predicts 220/220 positions of a real install's boot log |
| A missing `wrappedMethod()` is diagnosed | **refuted** | A wrap with no such call compiled with no error and no warning |

## Where this stops

**Which wrap in a chain is outermost is NOT measured.** Every wrap in a chain is
emitted, and total emitted code is the same under any nesting, so the size
observable that settled the other questions cannot see nesting at all. What is
measured is the **order the sources are enumerated in**; the execution order of
the resulting chain is a separate fact, and this document does not claim it.
Anything downstream that reports a chain should say *enumeration order* and stop
there. One boot with each wrap logging before it calls `wrappedMethod()` would
settle it, and that boot has not been run.

**Whether the enumeration is the directory index's order or a sort the compiler
performs is not discriminated.** Both produce this exact list on the volume
measured, which was a single case-insensitive NTFS volume. They come apart on a
case-sensitive file system, and on any volume whose enumeration is not collated
— the same limit the tweak-layer measurement carries, for the same reason.

**Non-ASCII names are untested.** The uppercasing rule was established with
ASCII names only. A name whose case folding is not one-to-one is not covered.

**Overloads are untested.** Every target here has exactly one method of its name
on its type, so nothing measured says whether replacement collides at the level
of a name or of a full signature.

**The standalone compiler is not the in-boot compile.** The measurement drives
the compiler directly against a copied base blob; a launch drives it through the
runtime loader against the install's own blob. The enumeration cross-check above
is direct evidence that the two agree on source order — the strongest available
without a launch — but it is evidence about ordering, not about every other
respect in which the two invocations might differ.

**This is a compile-time measurement.** Everything here was read from what the
compiler emitted. No claim is made about a mod's runtime behaviour beyond what
the emitted code contains.

## What this means for a tool

A tool that wants to name the winner of a script conflict must enumerate sources
in the directory index's order with subdirectories in place, **append the
runtime-extension plugins' own scripts after that walk**, and report the **last**
`@replaceMethod` on a method as the winner.

Two consequences are worth stating plainly, because both change what a report
should say:

- **Plugin-contributed scripts come last, so under last-wins they beat every
  mod's replacement of the same method.** A tool that reads only the script
  directory will confidently name a mod that did not actually win.
- **The losers are the interesting part.** The winner is discoverable from the
  compiler's own warning by anyone who reads it carefully. The mods that lost
  are named nowhere, and listing them is the thing only a resolver can do.
