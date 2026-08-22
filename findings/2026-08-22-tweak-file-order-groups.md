# Tweak file order: three groups decided by one character, then the walk

**Class: ARCHIVE.** Established 2026-08-22 against TweakXL 1.11.4. Corrections
supersede in a new document.

**Supersedes [the 2026-08-19 tweak file order finding](2026-08-19-tweak-file-order.md),
which is not edited and remains exactly as written.** That document's law is
correct for the files it was measured over and incomplete for the layer as a
whole: the framework reads its files in **three groups**, and the instrument
that measured it populated only the middle one. Where the two documents differ,
this one is right.

---

## The law

> **TweakXL file order.** Walk the tweak directory recursively — within each
> directory take entries, files and subdirectories together, in the order the
> filesystem gives them, and read a subdirectory's contents at that
> subdirectory's own position.
>
> Sort the files found into **three groups by the first character of each
> file's own name**:
>
> | Group | First character of the **file name** | Read |
> |---|---|---|
> | first | `_` `#` `$` `!` | before every other file |
> | second | anything else | after the first group |
> | last | `^` | after every other file |
>
> Read the first group, then the second, then the last. **Within a group, the
> walk order stands.**
>
> **Apply order = read order**, and the **last** writer to a given value wins.
> Silently. No warning of any kind.

## What is new here, and what was already right

The 2026-08-19 document measured **pre-order walk, one case-insensitive
collation over files and subdirectories together, apply order = read order,
last writer wins.** Every one of those readings still holds as a reading, and
none of them is contradicted here.

One of them does not survive intact as an *explanation*. That document could not
tell whether the collation it observed was the framework's doing or the volume's,
said so, and left the question open; the section below settles it as the
volume's. What was observed stands. What it means for a tool changes, and that
change is the point of that section.

What it could not see is the grouping. Its instrument used eight files named
`rdp_a.yaml`, `rdp_ab.yaml`, `rdp_Ba.yaml`, `rdp_m0.yaml`, `rdp_zz.yaml` and
three inside subdirectories — **every one beginning with `r`**, so every one
landed in the second group and the groups never separated. The measurement was
correct and the generalisation from it was too wide.

### The test is on the leaf, and this is the part that surprises

The group is decided by the first character of **the file's own name**. Not the
path, and **not the directory it sits in**. So:

- `#SomeDirectory\ordinary.yaml` is in the **second** group. Its directory's
  name does nothing to its group.
- `zzz\_promoted.yaml` is in the **first** group, however late in the walk its
  directory is reached.

Both mechanisms exist and they compose, which is easy to mistake for one
mechanism. A directory whose name begins with a punctuation character sorts
early **in the walk**, and that is a lexical effect with nothing to do with the
grouping; a *file* whose name begins with one of the four markers is **promoted
out of the walk order entirely**. A mod can use both at once, and one on a real
install does: it ships a directory named with a run of `#` characters *and* a
file inside it whose own name begins with `#`, so the file is promoted by the
grouping while its sibling in the same directory is carried only by the
directory's lexical position.

## Evidence, and its class — stated rather than implied

**This is not the same kind of evidence as the document it supersedes.** That
one was measured from the framework's own debug log across two boots. This one
rests on:

1. **The framework's published source at the tag matching the shipped build.**
   `src/App/Tweaks/Declarative/TweakImporter.cpp` at `v1.11.4` collects paths
   into three vectors and reads them in sequence, with membership decided by
   `aPath.filename().string().front()` against the literals `"_#$!"` and `"^"`.
   Byte-identical to the same file on the project's default branch.
2. **The shipped binary.** The literal `_#$!` occurs exactly once in the
   TweakXL 1.11.4 DLL on the machine this was established from, inside a
   short-string construction of length 4. The grouping is in the build the
   2026-08-19 measurement was taken against, not a later addition.
3. **A real layer, replayed.** A 16-file install layer containing one
   first-group file was enumerated by this project's own implementation and
   compared against that install's TweakXL log, which states the read order
   directly: **identical, 16 of 16.**

**What point 3 does not do is discriminate.** On that layer the first-group
file already sorted first in the plain walk, so the superseded law and this one
predict the same order. The agreement confirms the walk and the rest of the law
on real data; it is not evidence for the grouping, and it is not reported as
though it were.

**The grouping itself has not been observed separating files in a running
game.** A boot over a layer built to separate them — a promoted file in a
directory that sorts last, an ordinary file in one that sorts first — would
settle it, and the framework's log states read order directly, so one boot is
enough. Until that runs, the grouping is **read from the shipped source and the
shipped binary, and labelled as such**.

## The collation question is answered — and the answer gives the old limit teeth

The superseded document recorded a limit it could not resolve: whether the
framework sorts entries case-insensitively, or consumes an enumeration that is
already collated because it was taken from an NTFS volume. It named the
consequence — on a volume whose enumeration is not collated the two explanations
predict **different winners** — and left it open.

**The source settles it: there is no sort.** The walk is a recursive directory
iteration and the entries are consumed in the order the filesystem yields them.
Explanation 2 is the correct one.

So the limit is not merely unresolved, it is **real**. On a volume whose
directory enumeration is not case-insensitively collated — some removable media,
some network shares — the read order is whatever that filesystem yields, and a
tool that sorts would confidently disagree with the game.

**What follows for a tool:** do not sort. Take the enumeration as it comes, and
**check** whether it is already collated rather than assuming it. A layer whose
enumeration is not collated is one where every ordering claim has to say so.

## Which files are read at all, and how the extension is matched

The same file that carries the grouping decides which reader a file goes to, by
its extension: `.yaml` and `.yml` to the YAML reader, `.tweak` to the reader for
the other tweak language. Anything else is not read.

**The comparison is case-sensitive**, so a file whose extension is spelled with
any capital — `Thing.YAML` — is not read by the framework at all. That follows
from the same source as the grouping: the extension is compared against wide
string literals with the standard library's path comparison, which compares the
text rather than folding case.

**This is a source reading, at the same evidence class as the grouping above,
and it has not been observed in a running game.** It is called out separately
because getting it wrong is asymmetric: a tool that reads such a file puts
values into its resolved state that the game does not have, which is a silent
wrong answer. A tool that declines to read one reports the file as passed over,
by name, where a reader can see it and disagree.

## Where this stops

- **The grouping is source-derived, not yet observed in a boot.** Stated above,
  and repeated here because it is the load-bearing limit of this document.
- **The four first-group markers and the one last-group marker are read from
  the source literals.** No boot has confirmed each character individually.
- **The case-sensitive extension match is source-derived and unobserved**, as
  stated above.
- **Nothing here measures what happens when two files in the same group and the
  same directory contest a value** beyond what the superseded document already
  established: the later-read one wins.
- **Ties between the framework's additional import paths are out of scope.** The
  framework allows other components to register further directories and single
  files; this document describes the default tweak directory only.

## What this means for a tool

A replay that implements the superseded law alone will name the wrong winner on
any layer where a promoted or demoted file contests a value — and the marker
characters are used deliberately, by authors trying to control precedence. The
grouping is the first thing to apply and the walk is the tie-break inside it.

And because the framework reports nothing when two files write one value,
**surfacing the contest at all is the feature**. There is no signal to forward;
it has to be derived by replaying the whole layer.
