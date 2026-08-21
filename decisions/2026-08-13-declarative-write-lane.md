# The write lane is declarative, and originals are never touched

**Class: ARCHIVE.** Decided 2026-08-13. Corrections supersede in a new record.

## Context

A tool that reports what a mod setup resolves to will eventually be asked to
*change* something — reorder a load, resolve a collision, add a value. How it
writes determines what it is allowed to break.

Cyberpunk 2077 offers several ways to change game data, and they are not
equivalent in what they cost the user. Some rewrite the game's own files. Some
rewrite another mod's files. One class of framework accepts a declaration of
intent and applies it at load time, leaving everything on disk untouched.

## The question

When ripperdoc writes, what does it write?

## Options

**(a) Edit game files directly.** Unpack the archive, change the record, repack.
Maximum reach — anything expressible is reachable.

**(b) Emit a REDmod.** Use the official mod format as the output target.

**(c) Emit a declarative overlay** — ripperdoc's own `.archive` plus a manifest
for the ecosystem's resource-extension framework, and YAML for the tweak
framework. Nothing that already exists on disk is modified.

## The call

**(c), the declarative overlay.** Settled 2026-08-13 and treated as closed
since — this is not a question later waves reopen.

## Reasoning

The three options differ mainly in what happens when ripperdoc is **wrong**,
and a tool whose entire pitch is "you will not have to bisect any more" has to
be honest about that case.

Under (a), being wrong means a modified game file and a user who now has to
verify their install rather than their mods. The failure is silent and it is
not obviously ripperdoc's.

Under (c), being wrong means one extra mod that can be read, reviewed, and
deleted. The output is **legible** — a person can look at what ripperdoc
decided, in a text file, before trusting it — and it is **removable** without a
trace. That is the same property that makes the equivalent lane workable in
other modding ecosystems: the tool's opinion is a separate, inspectable layer
rather than an edit to the substrate.

(b) was not rejected on quality. It is a real format with real support, but it
is a heavier unit of output for what is usually a small declaration, and it
does not have the same "delete it and everything is exactly as before"
property.

There is a second reason, and it is about the product rather than the format:
the declarative frameworks are **where the ordering rules the engine has to
model already live**. Writing into that lane means writing in the same terms
the engine reads in, rather than translating between two representations and
being wrong at the seam.

## Would be wrong if

**Some class of change turns out not to be expressible declaratively**, and the
tool needs a fallback that touches originals after all. That would not be fatal
— the fallback would be gated and loud — but it would weaken the promise from
"never touches your files" to "usually doesn't", which is a materially worse
promise and would deserve saying plainly rather than quietly.

**Or if the declarative frameworks stop being maintained.** They are
third-party, and this decision ties the write lane to their continued
existence. Priced as acceptable: they are the ecosystem's de facto standard,
and the read lane does not depend on them at all — only the write lane would
need rehoming.

The reversal cost is moderate and mostly one-directional: writing declaratively
now does not prevent adding a direct-write path later, while starting with
direct writes would have made the declarative lane feel like a downgrade.

## Outcome

*Not yet backfilled — no write lane has shipped.*
