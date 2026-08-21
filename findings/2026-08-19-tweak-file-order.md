# Tweak file order: a pre-order walk, one case-insensitive collation, and the last writer wins silently

**Class: ARCHIVE.** Measured on game version 2.31 with TweakXL 1.11.4,
completing 2026-08-19. Corrections supersede in a new document.

---

## The law

> **TweakXL file order.** Walk the tweak directory recursively. Within each
> directory, take entries — **files and subdirectories together** — in one
> **case-insensitive collation**; when the entry is a directory, read its
> contents **immediately, at that position**, before continuing with the rest of
> the parent directory.
>
> **Apply order = read order**, and the **last** writer to a given value wins.
>
> Silently. No warning of any kind.

## The two things worth knowing before anything else

**1. The last writer wins — the opposite of the archive layer.** When two
archives contest a file, the **first**-loaded one wins
([see the archive-order finding](2026-08-19-archive-load-order.md)). When two
tweak files write the same value, the **last**-applied one wins. Prefixing a
filename to sort it earlier promotes an archive and **demotes** a tweak file.
One instinct, two frameworks, opposite outcomes, and nothing tells you.

**2. Nothing reports the collision.** Two files writing the same value produce
no warning — not in the framework's own log at debug level, not as a popup, not
anywhere. It is not merely that no tool checks for tweak conflicts; **the data
needed to notice one is never surfaced by the framework that is applying it.**
Whichever file happens to sort last simply wins, and the setup looks like it is
working.

## How it was measured

### The instrument

Eight single-line writers, all setting the **same** value to eight distinct
strings, placed at paths constructed specifically to discriminate between the
competing explanations of ordering.

Two independent readings:

- **The framework's own debug log**, which states the read order directly.
- **A runtime read of the resolved value**, which names whichever file applied
  last.

The whole tree was **byte-identical across two separate boots**, and produced
identical results both times.

### The reading

```
Reading "rdp_a.yaml"
Reading "rdp_ab.yaml"
Reading "rdp_Ba.yaml"
Reading "rdp_m\rdp_inner.yaml"
Reading "rdp_m0.yaml"
Reading "rdp_n\rdp_deep\rdp_deepfile.yaml"
Reading "rdp_n\rdp_nfile.yaml"
Reading "rdp_zz.yaml"
```

The resolved value was the one written by `rdp_zz` — last in the logged read
order — on both boots, on both reads.

### Every competing explanation, and the exact path that killed it

| Hypothesis | Verdict | The observation that decided it |
|---|---|---|
| **Pre-order depth-first traversal** | **confirmed** | The full order matches the prediction exactly, which was fixed in writing before the run |
| Full-path string sort | **refuted** | `rdp_m\rdp_inner.yaml` was read **before** `rdp_m0.yaml`. A path sort puts `rdp_m0.yaml` first, since `0` (0x30) sorts before `\` (0x5C). This pair exists in the tree for exactly this test |
| Files before subdirectories | **refuted** | The root-level `rdp_zz.yaml` was read **after** every subdirectory's contents |
| Subdirectories before files | **refuted** | The root-level `rdp_a.yaml` was read **before** the contents of `rdp_m\` |
| Case-**sensitive** ASCII collation | **refuted** | `rdp_ab.yaml` came before `rdp_Ba.yaml`; case-sensitive ASCII puts `B` (0x42) before `a` (0x61) |
| Subdirectory files are not read at all | **refuted** | All three subdirectory files were read, including one at depth 2 |

Apply-order-equals-read-order had previously been established only for two
files in the same directory. The winner here is the last file in a logged order
spanning two directories and two levels of nesting, so it now holds **across
subdirectories** too.

## Where this stops — and why the limit has teeth

This measurement **cannot distinguish** two explanations that predict every
reading above:

1. TweakXL sorts entries case-insensitively.
2. TweakXL consumes the order the filesystem hands it, which on NTFS is
   **already** case-insensitively collated.

Both fit the data perfectly, and the measurement was taken on a single NTFS
volume.

**The distinction is not academic.** On a volume whose directory enumeration is
*not* collated — FAT32 or exFAT removable drives, and some network shares —
explanation 2 predicts **creation order**, and therefore a different winner for
exactly the same set of files. A tool that hard-codes a sort would then confidently
disagree with the game.

Recorded here as a **labelled open question**, not as part of the law. Anyone
who can run the eight-file tree from a non-NTFS volume would settle it.

## What this means for a tool

Replaying tweak application faithfully means walking the tree in pre-order,
taking files and directories in one collation pass rather than in two phases,
and treating apply order as read order. The last writer wins — so a tool that
wants to report *which mod actually set this value* has to model the whole walk,
not just look at which files mention it.

And because the framework itself reports nothing, **surfacing the collision at
all is the feature**. There is no existing signal to forward; it has to be
derived.
