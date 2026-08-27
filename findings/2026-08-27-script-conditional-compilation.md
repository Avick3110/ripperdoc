# Script annotations: `@if` removes the declaration beneath it from the compile

**Class: ARCHIVE.** Measured on redscript's standalone compiler as shipped with
game version 2.31, 2026-08-27. Corrections supersede in a new document.

**This document extends the limits of
[the annotation-order finding](2026-08-27-script-annotation-order.md).** That
finding measured how the compiler orders sources and which `@replaceMethod`
wins among them. It did not measure — and its "Where this stops" did not name —
the annotation that decides whether a `@replaceMethod` or `@wrapMethod` is in the
compile at all. Everything the earlier document states remains as measured; what
follows narrows the set of annotations its law ranges over.

---

## The law

> **Conditional compilation, redscript on game 2.31.**
>
> 1. **`@if(<condition>)` gates exactly the one declaration that follows it.**
>    The declaration after that one is unaffected.
> 2. **A declaration whose gate evaluates false is removed from the compile
>    entirely.** It emits no code, it takes part in no replacement contest, and
>    it raises no warning. The compiler's output is byte-identical to the same
>    compile with that declaration's source absent.
> 3. **A gated-out `@replaceMethod` is not "a previous annotation".** It does not
>    win, it does not lose, and it does not make the next replacement warn.
> 4. **`@wrapMethod` gates the same way.** A false-gated wrap is not emitted, so
>    it is not in the chain at all.
> 5. **Blank lines and comments between the gate and the annotation do not break
>    the association.**
>
> A resolver that reads annotations without reading `@if` reports mods as
> replacing and wrapping methods they do not touch.

## Why this matters

The earlier finding's law ranks the annotations that apply. This one decides
which annotations those are, and it is not a rare corner: gating on whether
another mod is present is how compatible mods stand down for each other. A
resolver blind to it does not merely miss a case — it emits the opposite of the
truth, naming a mod as taking a method it deliberately released.

Worse on the replacement lane, because the sentence a resolver emits there is its
strongest. A gated-out replacement last in source order looks like the winner,
and the replacement that actually wins is then reported as silently overridden —
"installed cleanly, compiled cleanly, does nothing" — which is exactly backwards.

## How it was measured

### The instrument

The compiler that ships with the game, run standalone against synthetic `.reds`
sources. Each cell built its own scratch tree mirroring the game's `r6/` layout
and was given a **copy** of the untouched base script blob, verified identical by
sha256 before the cell ran. The compiler derives its cache and log locations from
the scripts path's parent, which is why the layout is mirrored: with it, every
byte the compiler writes — output blob, backup, timestamp sidecar, logs — lands
in scratch. The installed game was never given to the compiler and was verified
byte-unchanged afterwards.

**Two instrument properties worth knowing before repeating this.** Warnings are
**off unless `-W` is passed**, and with the wrong argument shape the compiler
exits 0 and logs "Compilation complete" over a source with a syntax error — a
silent green. The shape that reports honestly is `-compile <scripts_dir>
<cache_file> -W`, with the base blob already at `<cache_file>`; in that shape the
compiler also prints its source listing, which the piped-stdout shape omits. A
compile that does fail raises a **modal dialog**, so this instrument is not
unattended.

**The observable is emitted code size**, for the reason the earlier finding gives:
string literals reach the blob whether or not their body survives. Competing
bodies were built to differ by roughly nine kilobytes of arithmetic. A single
fixed **+52 bytes** is stamped on any compile regardless of content, established
by three independent cells below, so "+52" reads as "this source contributed
nothing".

### The cells

Target: one vanilla method with no parameters, replaced or wrapped. `<absent>` is
a module name that no source declares, so `ModuleExists("<absent>")` is false and
its negation is true.

| Cell | Sources | Exit | Growth | Warned |
|---|---|---|---|---|
| one ungated replacement | small body | 0 | −47 | no |
| **two ungated replacements** | small, then **large** | 0 | **+8,959** | **yes** |
| **second replacement gated false** | small, then large behind `@if(ModuleExists("<absent>"))` | 0 | **−40** | **no** |
| **second replacement gated true** | small, then large behind `@if(!ModuleExists("<absent>"))` | 0 | **+8,971** | **yes** |
| blank line between gate and annotation | as gated-false, with a blank line | 0 | −40 | no |
| comment between gate and annotation | as gated-false, with a comment line | 0 | −43 | no |
| gated declaration, then an ungated one | gated large replace, then a replace of a second method | 0 | −505 | no |

The **gated-true** cell is what makes the gated-false cell mean something. Both
carry an `@if`; they differ only in the condition's value. The true cell
reproduces the ungated control's growth and its warning exactly, so the compiler
is *evaluating* the gate rather than failing to parse the annotation beneath it.

### The gate's reach, isolated

Two cells differing only by whether a second, ungated declaration is present:

| Cell | Growth |
|---|---|
| gated large replace, alone | **+52** |
| gated large replace, then an ungated replace of a second method | **−417** |

The second declaration contributes **−469**, against **−421** measured for that
same replacement compiled by itself. It is compiled. The gate reached one
declaration and stopped.

### The wrap lane

| Cell | Growth |
|---|---|
| no annotation at all | **+52** |
| ungated large wrap | **+9,353** |
| large wrap gated false | **+52** |

A false-gated wrap contributes **exactly zero** — identical to a source carrying
no annotation. Three cells landing on the same +52 is also what establishes that
constant as the compile's fixed stamp rather than a small residue of content.

### What would have refuted each result

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| `@if` is inert text the compiler ignores | **refuted** | Gated-false and gated-true cells differ by 9 KB and by a warning |
| A gated-out annotation still joins the contest | **refuted** | The gated-false cell raised no warning where the ungated control raised one |
| A gated-out body is emitted somewhere | **refuted** | Gated-alone, gated-wrap and no-annotation cells all land on +52 |
| `@if` gates the rest of the file | **refuted** | The declaration after the gated one contributed −469 |
| The association breaks on a blank line or comment | **refuted** | Both cells behaved exactly as the adjacent-gate cell |
| `@if` does not apply to `@wrapMethod` | **refuted** | A false-gated wrap contributed 0 against the ungated wrap's +9,301 |

## Where this stops

**What a condition evaluates to is NOT measured here, and must not be inferred
from these cells.** The conditions used were chosen only to be reliably true and
reliably false so the cells would discriminate; nothing here establishes the rule
the compiler uses to decide `ModuleExists`, what else may appear in a condition,
or how a condition composes. A resolver that reads a gate and then decides its
truth value is guessing at a rule this project has not measured.

**Only `ModuleExists` was exercised.** It is the condition the surveyed layer
uses, but the annotation's grammar is not established here and other conditions
are untested.

**The braced form, if there is one, is untested.** Every gate in the surveyed
layer precedes a single declaration — annotations, functions, classes and imports
were all observed gated this way — and no braced group appeared, so none was
built. A gate spanning a group would not be covered by rule 1.

**Nesting is untested.** No cell carried two gates on one declaration.

**This is a compile-time measurement**, on one game version, through the
standalone compiler rather than the in-boot compile — the same three limits the
earlier finding carries, for the same reasons.

## What this means for a tool

A resolver must read `@if` and must **not** decide what it evaluates to. The two
halves are separate: the first is measured here, the second is not measured
anywhere.

The honest posture is a third state. An annotation carrying a gate is neither
live nor dropped — it is **undetermined** — and it must be kept out of contests
rather than counted into them, because counting it in is what produces the
inverted sentence above. Every result an undetermined annotation could have
changed has to say so, for the same reason a reading taken without a plugin's
scripts says its winners are provisional: the engine knows the input is
incomplete, and a reader who is not told cannot know.
