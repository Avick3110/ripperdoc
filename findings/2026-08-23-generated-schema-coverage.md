# A schema generated from the game explains no more of the shipped database than the inherited one, types all 2,085 of its reference slots where the inherited one types none of its 2,095, and is wrong about 66 slots it is right about

**Class: ARCHIVE.** Measured on game version 2.31 with Phantom Liberty, against
WolvenKit.RED4 8.20.0 and an RTTI dump captured from a clean install of that
build, 2026-08-23. Corrections supersede in a new document.

---

## The law

> **Generating the schema from the game's own type information is not strictly
> better than inheriting it. What it buys and what it costs point in opposite
> directions, and coverage of the shipped database is neither.**
>
> Against the same shipped database, the generated schema explains **3,150,039
> of 3,306,462 stored values (95.27 %)** and the inherited one explains
> **3,150,037 (95.27 %)**. The margin is **two values** in three and a quarter
> million, and the two modes round to the same share at four decimal places.
> Held to one name per field on both sides, they explain **exactly the same
> 3,150,037**.
>
> What generation actually buys is **reference kinds**: of the field slots that
> store a record identifier, the generated schema says what kind of record each
> may point at for **2,085 of 2,085**, and the inherited schema for **0 of
> 2,095**. That capability has no dump-free substitute.
>
> What generation costs is **storage-type fidelity on one family of fields**:
> shipped values contradict the generated schema on **66 field slots** and the
> inherited schema on **0**.

## The margin that was not there

An earlier statement of this law led with a **three-value** margin —
**3,150,040** against 3,150,037 — carried over from research. That figure was
an artifact of the instrument, not a property of the schema.

The generated mode recovers a field's name only up to its capitalisation, so it
probes each field under every candidate spelling and lets the data decide
between them. The count of explained values was taken **inside that loop**: a
value sitting at any candidate's identifier was counted, including at
candidates the very same run went on to reject. The inherited mode has no
alternates and probes one name per field, so the comparison was one mode
probing two names against another probing one, and the difference between them
was the second probe rather than the schema.

The count is now taken under **the names the arbitrated schema is keyed by** —
which is what the artifact carries, and therefore what it can address:

| Rule | Values explained |
|---|---|
| Every candidate spelling probed *(the old instrument)* | 3,150,040 |
| **The names the arbitrated artifact carries** *(this one)* | **3,150,039** |
| One name per field | 3,150,037 |
| Only spellings a shipped value vindicated | 3,082,768 |

The middle rule is the one that ships because it is the one the artifact
implements. Where the data confirms a spelling, the other candidates are
guesses it rejected and the artifact stops carrying them; where it confirms
none, nothing has decided and the artifact goes on carrying all of them, so all
of them still count. The gap to the strict one-name rule is the **two values**
reached only through an unconfirmed second candidate of a field the data never
settled.

**67,272 of the old figure's 3,150,040 — 2.14 % — were reached only under a
spelling no shipped value ever vindicated.** That is the measured size of the
old artifact. It is far larger than the three-value margin it produced, because
most of those values are also reachable under the field's confirmed name; only
the residue moved the total.

## Why the generated schema is contradicted where the inherited one is not

The generated schema recovers a field's type from the accessor that reads it,
and for one family of fields the accessor answers in a form the value is not
stored in. All **66** are that family: the accessor gives a plain name where
the stored value is a localisation key.

**Another 34 slots hold a value of the wrong type under one of their candidate
spellings and values of the right type under another.** Those are not slots the
schema is wrong about. Where the data confirms one spelling, what sits at
another candidate's identifier belongs to whatever else that identifier
addresses, and the field has been confirmed rather than contradicted. Counted
against the field rather than against the spelling, those 34 read as schema
errors and the total reads as **100**.

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

**These counts follow every candidate spelling, including ones the arbiter
rejected**, and so are counted by a wider rule than the coverage figures above.
The two instruments are deliberately not reconciled here: the reference check
runs without a validation manifest and has nothing to tell it which spellings
were settled. Two of the 59 come from following a second spelling, and whether
those two survive a settled-names rule is **not established**.

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
confirmed under both at once. Of **7,371** field slots offering more than one
candidate, the data settled **6,518**.

**Nothing of the game's is copied anywhere.** The dump and the database are
read in place from an installation, and no part of either is reproduced in
this project.

## What would have refuted it

- **A generated schema explaining materially less than the research measured**
  would have meant a defect in the port. Under the research's own counting rule
  it explains 3,150,040, which is the figure the research measured; the
  correction above is to the rule, not to the derivation.
- **The two modes differing in coverage by an amount that mattered** would have
  made generation worth its setup cost on coverage grounds. Two values in
  3,306,462 is not that amount, and under one name per field the difference is
  zero.
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
- **The coverage figures use the settled-names rule and the reference figures
  do not.** Both are stated at the rule they were taken under, and the
  reference figures have not been re-taken under the narrower one.
- **The 66 contradicted slots are counted, not individually adjudicated.** They
  were identified as one family by the shape of their type pairings rather than
  examined one by one. The 34 that are contradicted only under a spelling the
  data rejected are separated by measurement rather than by inspection, but
  what sits at each of those identifiers instead was not looked up.
- **Whether the two modes lack the same 382 property writes** is not
  established. Only that they lack the same number, and that the framework's
  metadata covers all of them in both cases.
