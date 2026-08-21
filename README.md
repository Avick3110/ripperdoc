# ripperdoc

A deterministic resolved-state engine for Cyberpunk 2077 mod setups.

**Status: early. There is no release.** The first layer of the engine exists -
the schema layer that knows what every record type looks like, and the check
that holds it to what the game actually ships. Nothing reads a mod setup yet.
Alongside the code sit the operating manual, the standards, the decision
record, and a set of measured findings about how the game and its modding
frameworks actually resolve conflicts. The rest arrives wave by wave from here.

---

## What it is meant to do

When two mods touch the same thing, something wins. Today, working out *what*
means launching the game and bisecting - pull half the mods, boot, look, repeat.
The frameworks that apply the changes generally do not report the collision at
all, so the data needed to notice it never reaches the person who needs it.

ripperdoc is meant to answer that question without a launch: read the install,
replay the same ordering rules the game and its frameworks use, and report the
resolved state with provenance - this value, from this mod, because of this
rule.

It sits underneath the existing tools rather than replacing them. WolvenKit
remains the editor; ripperdoc is the data layer beneath it.

## What it will not do

- **It does not edit your game files.** Output goes to its own mod, in the
  declarative form the ecosystem already supports - originals untouched,
  reviewable, removable.
- **It does not put AI inside the engine.** The engine is deterministic. Any
  agent or GUI is a client of it.
- **It does not guess.** A field it cannot validate is labelled unvalidated. A
  check it cannot run says so. Silence is never reported as success.

## What is in this repository

| Path | What lives there |
|---|---|
| [`decisions/`](decisions/) | Why the project is shaped the way it is - one record per decision, including the alternatives that lost and what would make the call wrong |
| [`findings/`](findings/) | Measured behaviour of the game and its frameworks. Several of these contradict widely repeated community advice |
| [`standards/`](standards/) | The conventions this repository holds itself to |
| [`src/`](src/) | The engine |
| [`tests/`](tests/) | Checks, and the [fixture rules](tests/fixtures/README.md) they run under |
| `CLAUDE.md` | The operating manual - how work is done here, in full |

## What works today

The schema layer, in the mode that needs no setup.

It reads the record type model out of the pinned dependency, resolves what
every record type's fields are, and then checks that schema against a shipped
tweak database - computing each value's identifier arithmetically, so nothing
has to map identifiers back to names. Every field comes out marked: confirmed
by real data, or not confirmed and labelled with the reason.

The artifact it produces carries its own provenance, including what this mode
cannot do. It cannot say what *kind* of record a stored reference is allowed to
point at, it cannot notice the type model drifting away from the game, and it
covers a newly patched game only once the dependency catches up. Those are
written into the artifact rather than into a footnote, because a degraded mode
that does not say what it lost is indistinguishable from a complete one until
it gives a wrong answer.

## The findings are worth reading on their own

Some of what is in [`findings/`](findings/) is useful whether or not this tool
ever ships, because it is measured rather than repeated. Load order is honoured
in ways the documentation does not describe, and the instinct that promotes one
kind of file demotes another. Each finding states what was measured, how, what
would have refuted it, and where it stops.

## Building

Requires the .NET 8 SDK.

```bash
bash scripts/ci-checks.sh
```

That script is the gate, and it is the same command CI runs. Checks that cannot
run on a machine without the game are announced as skipped, by name, rather
than left silent.

The dependency on WolvenKit is pinned exactly, and a test asserts the pin - the
type model is inherited rather than hand-written, so a version drift would be a
silent behaviour change.

The checks that read a shipped tweak database need one, and the database is the
game publisher's file rather than anything this project ships. Point
`RIPPERDOC_TWEAKDB_PATH` at your own copy to run them:

```bash
RIPPERDOC_TWEAKDB_PATH="<your game>/r6/cache/tweakdb_ep1.bin" bash scripts/ci-checks.sh
```

Those checks reproduce counts measured against one specific build — game 2.31
with Phantom Liberty — so the gate compares the database's fingerprint before
running them. Point it at any other build and the gate says so and skips that
tier by name, rather than failing counts that were never about your copy.

## Credit, and licence

ripperdoc builds on **[WolvenKit](https://github.com/WolvenKit/WolvenKit)**,
whose MIT-licensed packages provide the resource type model this project reads
through. The read and write formats it targets are those established by the
ecosystem's existing frameworks.

**ripperdoc is MIT licensed** - see [LICENSE](LICENSE). Use it, fork it, build
on it. The same licence the packages underneath it use, which keeps the whole
stack under one set of terms rather than a chain of compatible-but-different
ones.

## Contributing

Not yet set up for outside contributions - there is no code to contribute to.
Bug reports and gap reports are welcome through Issues once there is something
to report against.
