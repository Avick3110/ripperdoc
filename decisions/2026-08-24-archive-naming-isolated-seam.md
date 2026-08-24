# Archive naming: take the dictionary, behind an isolated seam

**Class: ARCHIVE.** Decided 2026-08-24. Corrections supersede in a new record.

## Context

An archive addresses the resources it carries by hash, not by path. Some
entries carry their own path and can be named from the archive alone; the rest
can be named only from a dictionary of known resource paths.

The build plan had already ruled out one answer: **name-only reporting**, which
drops every entry nothing can name, is forbidden, and the archive layer reports
by hash and never omits. What it left open, explicitly, was where names come
from — with an instruction to decide it rather than default into it.

The pinned library ships the *interface* for a name dictionary and **no
implementation**. So a caller must supply one, and that requirement is the
seam the decision turns on.

## What was measured

On one real install — 144 archives, 14,610 entries, 14,204 distinct, no
`modlist.txt` — read through the pinned library's index reading:

| | archive-declared paths only | with the dictionary |
|---|---|---|
| distinct entries named | 7,594 (53.5 %) | 11,427 (80.4 %) |
| archives with no named entry | 84 of 144 | 1 of 144 |
| **contested entries named** | 298 of 405 (73.6 %) | **405 of 405 (100 %)** |

The contested row is the one that decided it: those 405 contested entries are
what the archive layer exists to report, and without the dictionary 107 of them
would be reported as bare decimal hashes.

Three properties of the dictionary-less figure were checked rather than
assumed. It does not change when the game's compression library is loadable, it
does not change with the order archives are read in, and the names it recovers
are per-archive rather than borrowed between archives.

**Scope.** One install, one deployment shape, no list file. Nothing here
measures a larger collection or another deployment channel, and the 100 % is a
statement about this corpus.

## The question

Take the dictionary as a dependency, or carry the narrower naming and no new
dependency?

## Options

**(a) Take it into the engine.** The dictionary ships in a package by the same
publisher, at the same pinned version and the same upstream commit as the
library already relied on. Widest coverage, one line of build configuration.

**(b) Do without.** No new dependency, and 46.5 % of entries — and a quarter of
the contested ones — reported by hash.

**(c) Take it, behind an isolated seam.** Naming becomes an interface in the
engine core; the dictionary-backed implementation lives in its own assembly
that a client opts into. Coverage of (a), dependency closure of (b) for anyone
who does not ask for it.

## The call

**(c).** Aaron, 2026-08-24, with the measurement above in front of him.

## Reasoning

The dictionary's coverage is worth having: the contested set is the
deliverable, and reporting a quarter of it as decimal hashes would be honest
and useless at the same time.

What made (a) the wrong shape for it is the cost that is not in the coverage
table. The package carrying the dictionary brings an object-relational mapper,
an embedded database, a graphics library whose upstream has been discontinued,
a glTF reader and a native texture toolchain. That is a mod editor's dependency
tree, and it is being acquired for a hash table. The engine is a library that a
command-line client and a server both load, and a dependency taken into its
core is taken by every client whether or not it asked.

So the split follows the actual shape of the requirement: **coverage is
optional, the contract is not.** Every entry is reported either way, and an
entry with no name is reported by hash either way. The dictionary moves where
the boundary between named and hash-only falls; it does not move what the
inventory contains. Something that only changes a boundary belongs behind a
seam, and the interface the pinned library already demands made that seam
nearly free to build.

**The self-check is part of the decision, not an implementation detail.** The
service that owns the dictionary reports a population of zero whether or not it
loaded anything, so a caller that trusted it would produce an inventory whose
every entry came back by hash while its provenance claimed dictionary
coverage — honest entry by entry, dishonest as a whole. The source therefore
verifies its own load against the resolver the names have to reach, and refuses
the read if it cannot.

## Would be wrong if

**The dependency turns out to be unusable rather than merely heavy.** One of
its native pieces already fails to resolve in a plain console host; nothing on
the naming path touches it, and the read completes, but a future version that
made the whole package unloadable on a bare runner would strand the opt-in
assembly. The engine core would be unaffected, which is the point of the split.

**Or the coverage gap closes on its own.** If archives in general came to
carry their own paths, the dictionary would be buying a shrinking margin and
the second assembly would be scaffolding around nothing. The measurement says
otherwise today: 84 of 144 archives on the measured install name nothing at
all.

**Or a client appears that wants the coverage without the opt-in.** If every
real client ends up installing the dictionary anyway, the seam is ceremony and
(a) was the simpler answer all along. That would be visible from how clients
are actually configured, and reversing it is deleting a project rather than
unpicking a design.

## Outcome

*Not yet backfilled — no client has shipped.*
