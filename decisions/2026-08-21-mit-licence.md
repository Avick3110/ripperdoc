# Licence: MIT

**Class: ARCHIVE.** Decided 2026-08-21. Corrections supersede in a new record.

## Context

The repository was created public at wave 0, and deliberately shipped its first
commits with **no licence file**. That is not a neutral state: with nothing
declared, default copyright applies and nobody may reuse the code. The README
said so plainly rather than leaving it ambiguous, and the choice was left to
the project's owner rather than made by whoever happened to be scaffolding.

This record closes that.

## The question

Under what licence is ripperdoc's own code released?

## Options

**(a) MIT.** Permissive. Anyone may use, modify, and redistribute, including in
closed-source work, provided the notice travels with it.

**(b) Leave it undeclared.** Public for reading, not for reuse.

**(c) A copyleft licence.** Derivatives must stay open under the same terms.

## The call

**(a), MIT.** Aaron, 2026-08-21.

## Reasoning

**It matches the layer underneath.** The WolvenKit packages this project reads
the resource type model through are MIT. Choosing the same licence keeps the
whole stack under one set of terms instead of a chain of compatible-but-
different ones that every downstream user has to reason about. (The WolvenKit
*application* is under a copyleft licence; its published packages are not, and
those packages are what ripperdoc depends on.)

**It fits how the modding ecosystem actually works.** Tools here get forked,
vendored, adapted into other people's pipelines, and bundled into things nobody
anticipated. A permissive licence is the one that does not make any of that a
question someone has to ask permission for — and a tool whose whole purpose is
to remove friction from other people's work is a strange place to add some.

**(b) was already the status quo and was always temporary.** Leaving it would
have meant the findings and the reasoning were public while the code was
readable-but-untouchable, which is a confusing posture and not one anyone
wanted.

**(c) was not chosen**, and the reason is worth being honest about rather than
dressing up: this is a one-user tool at the start of its life, and the risk
copyleft protects against — someone taking the work closed and giving nothing
back — is a small risk against a real cost, which is that copyleft makes the
code awkward to reuse in exactly the informal, bundled ways this ecosystem
reuses things.

## Would be wrong if

**Someone builds a closed product on this and the community gets nothing back
while the project carries the maintenance.** That is the failure MIT permits by
design. Priced as acceptable: the genuinely valuable artifacts here are the
*measurements*, which are published as prose in `findings/` and cannot be
enclosed by anyone.

**Or if a future dependency turns out to be copyleft** and forces the
combined work into stricter terms than this record promises. Mitigated by the
current dependency posture — pinned, few, and permissive — but it is the thing
that would make this record need superseding rather than merely revisiting.

Reversal is genuinely one-directional and that should be said plainly: **code
already released under MIT stays available under MIT.** A later switch binds
future versions only. That asymmetry is the real cost of choosing (a), and it
was chosen knowingly.

## Outcome

*Not yet backfilled.*
