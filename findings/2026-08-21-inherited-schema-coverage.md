# The inherited type model explains 95.27 % of shipped tweak values — and contradicts none of them

**Class: ARCHIVE.** Measured on game version 2.31 with Phantom Liberty, against
WolvenKit.RED4 8.20.0, completing 2026-08-21. Corrections supersede in a new
document.

---

## The law

> **A tweak record's schema can be had without generating anything.** The type
> model that ships with WolvenKit's packages already describes **965 record
> types** declaring **4,687 fields**, which resolve through inheritance to
> **7,255 field slots**. Reflecting over it fails on nothing.
>
> Checked against the database the game ships, that schema accounts for
> **3,150,037 of 3,306,462 stored values — 95.27 %**.
>
> And where it accounts for a value, it is **right about the type**: across all
> 3,150,037, the type the schema claims and the type the value is actually
> stored as agree **every time**. Zero contradictions.

Two consequences worth separating, because they are usually run together.

**A schema you did not generate is not a schema you have to take on trust.** A
stored value's identifier is arithmetic over its name, and the arithmetic is
incremental — a field's identifier follows from its record's identifier plus
the field name. So every claim a schema makes can be checked against the whole
shipped database offline, with no table mapping identifiers back to names and
nothing generated from a game install. The audit is available in the mode that
has the least information.

**The 4.73 % it does not explain is not a hole in the schema.** That residue was
characterised separately and is development and cut-content residue: fields no
runtime accessor exposes, and values whose owning record no longer exists. This
document does not re-measure that; it measures what the schema *does* cover and
whether it is right about it.

## The number that is new here

Coverage at 95.27 % was already measured. What had not been checked is whether
the schema is *correct* where it claims coverage — and it is, exhaustively.

That matters more than it sounds. A schema can explain a value simply by
guessing a field name that happens to hash to something present; agreeing on
the stored type as well, three million times without a single exception, is a
much harder thing to do by accident. It is also the check that would catch the
inherited type model drifting away from the game in a way that changes
behaviour, which is the standing risk of inheriting a type model rather than
generating one.

## Every field is marked, and the marks add up

The point of the exercise is not the headline percentage. It is that no field
is left in an unknown state.

| Verdict | Field slots |
|---|---|
| Confirmed by at least one shipped value, with the type agreeing | **6,584** |
| Contradicted — a value exists, stored as some other type | **0** |
| The record type has shipped records; none carries this field | **13** |
| The record type has no shipped records at all, so nothing could confirm it | **658** |
| **Total** | **7,255** |

The last two rows are both "unconfirmed" and they are **not the same thing**.
One means the data was looked at and did not carry the field. The other means
there was no data to look at. Collapsing them would report one as the other,
and the difference is exactly what a reader needs in order to know whether an
unconfirmed field is suspicious or merely unused.

### The thirteen, named

These are the field slots the type model claims on record types that really
exist and that no shipped value corroborates:

| Record type | Field | Claimed type |
|---|---|---|
| `gamedataAIActionTicket_Record` | `stdDevStarsTrash` | `TweakDBID` |
| `gamedataAIHasWeapon_Record` | `maxShotsToDefeatCrowd` | `CName` |
| `gamedataAIRingTicket_Record` | `stdDevStarsTrash` | `TweakDBID` |
| `gamedataAISubActionModifyStatPool_Record` | `minFallHeightToConsiderInputToggles` | `array:TweakDBID` |
| `gamedataAISubActionSetUnequipPrimaryWeapons_Record` | `UnequipDuration` | `TweakDBID` |
| `gamedataAIVelocityDotCond_Record` | `effectorClassName` | `Bool` |
| `gamedataAvoidLineOfSightSelectionParameters_Record` | `is_paralax` | `array:TweakDBID` |
| `gamedataCurveStatModifier_Record` | `heightToEnterFall` | `gamedataLocKeyWrapper` |
| `gamedataGameplayLogicPackageUIData_Record` | `maxFactor` | `Float` |
| `gamedataRPGAction_Record` | `visibilityConeStartAngle` | `TweakDBID` |
| `gamedataVehicleDestructibleLight_Record` | `Thrusters` | `Bool` |
| `gamedataVehicleSeat_Record` | `springReboundDampingLowRate` | `array:TweakDBID` |
| `gamedatadevice_scanning_data_Record` | `iconScale` | `array:TweakDBID` |

They read like entries that slipped a position while the model was being folded
by hand: a field name attached to a record type it does not belong to, and a
type that does not match the name. `is_paralax` on a line-of-sight selection
parameter, an `effectorClassName` typed `Bool`, a `heightToEnterFall` typed as a
localisation key. Earlier work put this class at "about ten" from a different
direction; this is the complete list, and it is thirteen.

**They are not removed.** They are labelled. A field the data does not confirm
might be wrong, or might be real and simply unused by anything the game ships —
the data cannot tell those apart, so neither does the label.

## How it was measured

**The schema side.** Reflect over the packaged type model, take every class
whose name marks it as a record type, read each annotated property's stored
name and stored type through the model's own type resolution, and resolve each
record type's full field set by walking its ancestry with the nearest
declaration winning. Ancestors are followed through the type graph rather than
looked up in a filtered list, so a chain cannot end early because an ancestor
was not on the list. Nothing is hand-entered and there is no per-record-type
special case anywhere in the path.

**The arbiter.** `tweakdb_ep1.bin` as the game ships it, opened read-only,
sha256
`89c7ee678c1366d4c289edc78beaa60ce3d64bf44b300fc3902adc94f6ac14c5`. For each of
its 193,354 records, the schema's field set for that record's type is turned
into candidate identifiers arithmetically and each is looked for among the
3,306,462 stored values. Where one is found, its stored type is compared with
the type the schema claims.

**Every record type in the database is one the schema knows** — 843 of the 965
appear in shipped data, and no record in the file has a type the schema has
never heard of. The other 122 have no shipped records at all; **118** of them
declare at least one field and together account for the 658 unconfirmable slots
above, while the remaining four resolve to no fields whatever and so contribute
none. (Seven of the 965 resolve to no fields in total — they are the abstract
bases of the family.)

**Nothing of the game's is copied anywhere.** The database is read in place from
an installation and never moved, and no part of it is reproduced in this
project. The checks that need it run only on a machine that has it, and the
gate that runs them announces them as skipped, by name, on a machine that does
not.

## What would have refuted it

- **A schema that explained values by luck** would have disagreed about stored
  types. Three million values, twenty distinct stored types, and a name-hash
  that gives no clue about the type: a schema guessing names would have been
  wrong about types constantly. It was wrong zero times.
- **A truncated inheritance walk** would have produced a lower coverage figure.
  It was checked from both directions: following the type graph and following
  only the record-named types give the same 3,150,037, because the classes
  between a record type and the root declare no fields at all.
- **A sweep that had quietly stopped working** would have kept reporting the
  same number. So one field the database really does carry is renamed and the
  sweep is re-run as part of the same check: it is required to come back with
  fewer values explained and to mark that field unconfirmed. An unchanged
  number there fails the run.
- **A record type outside the schema** would have meant records checked against
  nothing while the coverage figure looked healthy. Counted separately, and it
  is zero.

## Where it stops

- **Vanilla 2.31 plus Phantom Liberty only.** This is not a measurement of a
  modded install. A mod can declare record types no packaged type model
  contains, and how often that happens at collection scale is unmeasured.
- **This is coverage of *stored values*, not of *capability*.** It says the
  schema describes what the game ships. It says nothing about whether a given
  record type is useful to read.
- **Reference targets are not typed.** 2,095 of the 7,255 field slots store a
  reference to another record, and nothing in the packaged type model says what
  kind of record any of them may point at. A reference can be checked for
  existence in this mode; it cannot be checked for kind.
- **Drift is not detectable in this mode.** The packaged type model is the only
  description of the game that this mode has, so it cannot notice that model
  diverging from the game. That is the standing cost of inheriting rather than
  generating, and it is why the schema this mode produces carries the statement
  in its own provenance rather than in a footnote.
- **Names outside plain ASCII are refused, not measured.** The packaged
  conversion replaces such a character with a placeholder, so two different
  names come out as one identifier. Rather than reproduce that collision, a name
  carrying one is refused. What the game itself does with such a name has not
  been measured.
