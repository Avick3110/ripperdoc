# Topology: the engine is a library, not a daemon

**Class: ARCHIVE.** Decided 2026-08-21. Corrections supersede in a new record.

## Context

ripperdoc's architecture was settled before its *process shape* was: the engine
is deterministic, and the agent layer and any eventual GUI are clients of it.
That says what the pieces are. It does not say whether they are one process or
several, or whether anything stays resident.

Two clients are expected early — a command-line interface and an MCP server —
and both need the same expensive thing: a parsed view of an install that takes
real work to build.

## The question

Is the engine a library that each client loads, or a standing process that
clients talk to?

## Options

**(a) Library-first.** The engine is a .NET library. The CLI and the MCP server
are thin clients that load it. Nothing stays resident between invocations.

**(b) Daemon.** A standing process owns the parsed state; the CLI and the MCP
server are thin clients that talk to it over some local transport.

**(c) One process serving both surfaces.** The MCP server *is* the CLI, with
mode switches.

## The call

**(a), library-first.** Aaron, 2026-08-21, at the build-plan walkthrough, with
the reference implementation's record and the sync-semantics question in front
of him.

## Reasoning

The case for a daemon is that two clients would otherwise each pay to build the
same state, and that a resident process keeps them in agreement about what the
install currently is.

The second half of that sentence is the part that does not survive examination,
and it is what moved the decision. **What keeps two clients in agreement is the
disk, not the process.** Both are reading the same install; if the install
changes, both should see the change, and a resident cache is a thing that can
be *stale* relative to disk in a way that a fresh read cannot. The daemon does
not buy agreement — it buys a shared cache, and it buys a new class of bug
where the tool's answer and the user's folder disagree and the user is the one
who has to work out why.

That leaves the daemon's real benefit as **cost amortisation**, and cost is
exactly the thing this project has measured rather than assumed. The read
layers are fast enough that nothing approached the threshold at which "parse
and hold" would beat "parse each time". Paying a permanent architectural cost
to optimise something that is not currently expensive is the wrong trade.

Against that sits an empirical point that is not about Cyberpunk at all: in the
reference implementation, the daemon topology **is where most of the friction
lived** — lifecycle, staleness, and the debugging of both. That is one project's
experience and it is not automatically transferable, but it is direct evidence
about this exact choice made by this exact team, which is better evidence than
a preference.

**The asymmetry is what makes this safe.** A library wraps into a server later
— the state it builds is the state a server would hold. A daemon does not
unwrap into a library nearly as cleanly, because everything acquires assumptions
about the process being there. So library-first keeps the daemon available,
while daemon-first would have closed the library off. Where one direction is
reversible and the other is not, the reversible one needs a much weaker case to
win, and the daemon did not clear even the weak bar.

(c) was never seriously in contention: it couples two surfaces with different
lifetimes and different failure modes for no benefit either of them asked for.

## Would be wrong if

**A future scenario genuinely needs shared hot state across clients** — several
long-lived clients on one install, where rebuild cost per invocation actually
starts to bite, or where one client's write needs to be visible to another
without a disk round-trip. This is the case the daemon is for, and if it
arrives, the answer is to wrap the library in a server. That is a real cost, and
it is priced: it is additive work rather than a rewrite, which is exactly why
this ordering was chosen.

**Or if the read cost measured on one install turns out not to hold at larger
scales.** The measurements were taken at a specific size; a much larger install
could move them. That would make amortisation matter more — but it would still
be answered by adding a cache or a server on top of the library, not by having
started with one.

## Outcome

*Not yet backfilled — no client has shipped.*
