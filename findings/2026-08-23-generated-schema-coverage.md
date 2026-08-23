# A schema generated from the game explains three more shipped values than the inherited one, types all 2,085 of its reference slots where the inherited one types none of its 2,095, and is wrong about 66 slots it is right about

**Class: ARCHIVE.** Measured on game version 2.31 with Phantom Liberty, against
WolvenKit.RED4 8.20.0 and an RTTI dump captured from a clean install of that
build, 2026-08-23. Corrections supersede in a new document.

---

## The law

> **Generating the schema from the game's own type information is not strictly
> better than inheriting it, and the two ways it differs point in opposite
> directions.**
>
> Against the same shipped database, the generated schema explains **3,150,040
> of 3,306,462 stored values (95.27 %)** and the inherited one explains
> **3,150,037 (95.27 %)** — a difference of **three values**.
>
> What generation actually buys is **reference kinds**: of the field slots that
> store a record identifier, the generated schema says what kind of record each
> may point at for **2,085 of 2,085**, and the inherited schema for **0 of
> 2,095**. That capability has no dump-free substitute.
>
> What generation costs is **storage-type fidelity on one family of fields**:
> shipped values contradict the generated schema on **66 field slots** and the
> inherited schema on **0**.

The three-value margin was already measured during research. The other two
numbers are new, and the second of them inverts the expectation that a schema
derived from the game must be the more accurate of the two.

## Why the generated schema is contradicted where the inherited one is not

The generated schema recovers a field's type from the accessor that reads it,
and for one family of fields the accessor answers in a form the value is not
stored in. All **66** are that family: the accessor gives a plain name where
the stored value is a localisation key.

**Another 34 slots hold a value of the wrong type under one of their candidate
spellings and values of the right type under another.** Those are not slots the
schema is wrong about. The generated mode recovers a field's name only up to
its capitalisation and carries both spellings until data decides; where the
data confirms one of them, what sits at the other candidate's identifier
belongs to whatever else that identifier addresses, and the field has been
confirmed rather than contradicted. Counted against the field rather than
against the spelling, those 34 read as schema errors and the total reads as
**100**.

That attribution is measured, not inferred: the 34 are exactly the slots that
are corroborated under one spelling and contradicted under another, and the
three counts are asserted together — 100 contradicted somewhere, 34 confirmed
elsewhere, 66 left.

The engine does not correct the generated schema from the data. The derived
claim stands and the arbiter contradicts it, loudly, in the validation
manifest and in the artifact's own named losses. Overwriting the claim with
what the data holds would make the schema right about those slots and destroy
the signal that says when it is wrong, which is the more expensive of the two
losses.

## The reference graph, checked rather than asserted

A typed edge claims that values stored in one field name records of one kind.
Followed over every such value in the shipped database:

| | |
|---|---|
| Typed edges | **2,085**, none untyped |
| Distinct kinds of record pointed at | **490**, all of them record types the schema knows |
| Individual references followed | **2,300,852** |
| Naming a record of the permitted kind, or one deriving from it | **1,915,600** |
| Naming a record of some other kind | **59** |
| Naming no record in this database at all | **385,193** |

Every value under **every** spelling the schema offers for the field, not only
the one it leads with — which is why these counts are larger than a probe of
the leading spelling alone would produce.

**59 of 1,915,659 resolvable references — 0.003 % — name a kind the field does
not permit.** They are not a defect in the derivation: in every case examined,
the kind stored and the kind permitted meet only at the base type all records
share, so they are unrelated by the game's own type graph. The game's data puts
a sibling kind in the field. Anything reporting "this points at the wrong kind
of record" therefore has a floor of 59 on vanilla data, and that is a property
of the data rather than of the check.

The 385,193 that name no record are counted apart from those, because a
reference into content that is not shipped says nothing about whether the
schema is right.

## What the framework's metadata is, and is not

The framework that applies tweaks carries its own metadata declaring
properties beyond the type model's. An earlier measurement found **382 of
1,249 property writes** on one modded install naming something the inherited
type model alone lacks, all of them covered by that metadata, and left open
whether the generated schema would carry them itself.

**It does not.** Run over the same layer with the generated schema in place of
the inherited one, the count is **382 of 1,249** again, and the framework's
metadata covers all of them in both cases, leaving **zero** writes unexplained
either way.

So the metadata is not schema the game's type information carries and the
inherited model happens to miss. It is schema **neither** description of the
game contains, supplied by the framework at runtime — and generating from the
game does not reduce what it has to explain.

**Scope limit.** The two counts being equal is measured; whether they are the
*same* 382 writes was not separately established. One install, one layer.

## Evidence class

**Measured**, throughout, from runs of this project's own product code over a
real dump and a real shipped database. The one place a reading was taken by
inspection rather than by a run is named where it appears: the mismatched
references were characterised from a sample of their inheritance chains. That
sample was drawn when the count stood at 57 and the count is now 59; the two
that following the second spelling added were not themselves examined.

## How it was measured

**The generated side.** Every class the dump describes is read, and record
types' fields are recovered from accessor shapes: an accessor taking nothing
and giving a value is a field, one writing into a parameter the dump marks as
an output is the same field in another form, and the count, item, membership
and second-reference accessors registered around a field are not fields. Two
types are rewritten to what is on disk — a reference is stored as an
identifier, a resource reference as a path. Fields are recovered from record
types only: the three ancestors every record type has are not record types,
and the accessors they register ask an object its class or its identifier.

That yields **965 record types declaring 4,796 fields**, reproducing the
research sketch's figure exactly, with no derivation failure and no key in the
dump the reader did not read.

**The arbiter.** The same one the inherited mode is measured with: a stored
value's identifier is arithmetic over its name, so every field the schema
claims is looked for across the whole shipped database offline. Where a name's
capitalisation cannot be recovered from the accessor, both spellings are
carried as candidates of one field and the data decides between them — **89**
fields are confirmed only under the capitalised spelling, and **8** are
confirmed under both at once.

**Nothing of the game's is copied anywhere.** The dump and the database are
read in place from an installation, and no part of either is reproduced in
this project.

## What would have refuted it

- **A generated schema that explained fewer values than the research measured**
  would have meant a defect in the port. It explains 3,150,040, which is the
  figure the research measured for this mode.
- **Reference kinds that were an artifact of the derivation** would have shown
  up as edges pointing at kinds the schema does not contain. Counted: zero.
- **The mismatched references being a derivation error** would have shown the
  stored kind deriving from the permitted one after all. Checked on a sample:
  they meet only at the root that every record type shares.
- **The framework metadata being schema the dump carries** would have lowered
  the 382 when the generated schema replaced the inherited one. It did not move.

## Where it stops

- **Vanilla 2.31 plus Phantom Liberty, one install, one modded layer.** The
  property-write counts come from one person's mod set and would differ on
  another.
- **The dump describes the install it was taken from.** It was captured from a
  clean install, so it says nothing about record types a mod registers at
  runtime.
- **The 66 contradicted slots are counted, not individually adjudicated.** They
  were identified as one family by the shape of their type pairings rather than
  examined one by one. The 34 that are contradicted only under a spelling the
  data rejected are separated by measurement rather than by inspection, but
  what sits at each of those identifiers instead was not looked up.
- **Whether the two modes lack the same 382 property writes** is not
  established. Only that they lack the same number, and that the framework's
  metadata covers all of them in both cases.
