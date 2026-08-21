# RIPPERDOC_NAMING.md — naming, core only

**Class: LIVING.** Updated in the same commit as any convention it states.

**Scope: deliberately partial.** This standard covers the surfaces that exist
now — the brand, the C# tree, directories, and document filenames. Sections for
skill folders, packaging, and installers are **deferred until those surfaces
exist**, because a naming rule written before its surface is a guess that
later has to be unpicked.

That deferral is not caution for its own sake. The reference implementation
pre-authored a full naming standard, then trimmed it back to its
architecture-agnostic core and deferred the sections whose surfaces had not
arrived. Writing those sections here would be repeating a mistake somebody has
already paid for.

**These rules are written to be checkable by a reviewer, and could later be
checked by a lint or a hook — but no such guard exists, and none is added until
drift here is a recorded, repeated problem.**

---

## 1. The brand, and where it is allowed to appear

**Brand belongs on the surface. It never appears in the interior.**

| Surface — brand belongs here | Interior — brand must not appear |
|---|---|
| Tool names on the eventual MCP surface | Namespaces |
| The repository, product, and binary name | Class and type names |
| Log banners and user-facing output | Source file names |
| The config directory the tool creates | Local variables, parameters, fields |

So: `RecordReader`, never `RipperdocRecordReader`. The type is named for what
it does; the product it belongs to is context the reader already has.

This is the rule that makes the next one achievable — if the brand is scattered
through interior identifiers, no single constant can own it.

## 2. The brand string lives in exactly one place in code

One constant, `Branding.Name` in `src/ripperdoc-core/Branding.cs`. Everything
user-visible derives from it. **A bare string literal of the brand anywhere
else is a defect**, not a style preference.

## 3. Tool names

Tools on the eventual surface are **`ripperdoc_<snake_case>`**, built from
`Branding.ToolPrefix`. A tool name that does not carry the prefix is a **hard
failure**, not a warning: the prefix is how a caller distinguishes this surface
from every other one loaded beside it.

Names are snake_case throughout — no camelCase, no hyphens.

## 4. Rebrand touch-points

Enumerated so that a rebrand is a one-change job and not an audit:

1. `Branding.Name` — the constant.
2. The tool prefix — derived from it, so it follows for free.
3. The project directory and `AssemblyName` (§6).
4. The solution file name.
5. The repository name and the README.
6. The environment variables the tool reads, in `scripts/ci-checks.sh`. Code
   derives these from the constant; a shell script cannot, so the gate spells
   one out. It is listed here rather than treated as an exception, because an
   unlisted site is exactly what stops this list being a complete answer.

If this list ever stops being complete, that is itself the defect — fix the
scattering, then update the list.

## 5. File and directory names

**No version numbers in directory, file, or identifier names.** Versioning
lives in configuration constants and release tags, where exactly one thing owns
it. `parser-v2/`, `ReaderV2.cs`, `schema_v3` — all wrong; the old thing is
deleted or the new thing is named for what makes it different.

**One carve-out, and it is not versioning.** A *document* may carry a
supersession ordinal — `BUILD_PLAN_v2.md` — because ARCHIVE immutability
requires the successor to be a **different file** rather than an edit. That is
the supersede-don't-edit discipline expressing itself in a filename, and it
applies to documents only. It never licenses a versioned identifier in code.

**Top-level directories are `kebab-case`.**

**Document filenames key on class**
([`RIPPERDOC_DOC_HYGIENE.md`](RIPPERDOC_DOC_HYGIENE.md) §1):

| Class | Convention | Example |
|---|---|---|
| LIVING | `SCREAMING_SNAKE_CASE.md` | `BUILD_PLAN_v2.md`, `RIPPERDOC_NAMING.md` |
| ARCHIVE | `YYYY-MM-DD-kebab-slug.md` | `decisions/2026-08-21-library-first-topology.md` |

The date leads on ARCHIVE names because those documents are identified by
*when* — a decision, a measurement, a session — and a directory listing sorted
by name is then also sorted by time. `README.md` keeps its conventional name
wherever it appears.

**Dates are absolute, everywhere, always.** Never "yesterday", never "last
week", never "recently".

## 6. C# conventions

| Thing | Convention | Example |
|---|---|---|
| Project directory | `kebab-case` | `src/ripperdoc-core/` |
| `AssemblyName` | matches the directory | `ripperdoc-core` |
| `RootNamespace` | `PascalCase`, dotted | `Ripperdoc.Core` |
| Source files | `PascalCase.cs`, named for the primary type | `Branding.cs` |
| Test projects | `<component>-tests/`, under `tests/` | `tests/ripperdoc-core-tests/` |

**A `-tests` suffix means xUnit**, and it means the project is part of the
gate. A purpose-named investigative harness — something built to measure a
question rather than to assert an answer — does **not** take the suffix, and is
not part of the gate. Blurring the two is how a harness that was never meant to
pass or fail ends up gating a merge.

Package versions are pinned centrally in `Directory.Packages.props`. **A
version in a `.csproj` is a defect** — see §5 on one thing owning a version.
