# Asking the tweak database for a record's type searches for it; asking the pool looks it up

**Class: ARCHIVE.** Measured on WolvenKit.RED4 8.20.0 against the shipped
database of game 2.31 with Phantom Liberty, 2026-08-21. Corrections supersede in
a new document.

---

## The law

> **`TweakDB.GetRecordType(id)` does not look a record up — it finds it.** Over
> the 193,354 records the game ships, resolving every record's type through it
> costs **421 seconds**. The pool underneath exposes the same answer as a
> dictionary lookup, `TweakDB.Records.GetRecord(id)`, and the same 193,354
> resolutions cost **47 milliseconds**.
>
> Same answers, every one of them. Roughly **nine thousand times** the cost for
> nothing.

This is not a micro-optimisation. It is the difference between a whole-database
sweep that takes seven minutes and one that takes under a second, which is the
difference between a check somebody runs and a check somebody stops running.

## Why it is worth publishing

It is the same shape as something this project already measured in a different
corner of the same toolchain: per-archive listing through the command-line tool
was thousands of times slower than reading the indices directly. Two unrelated
convenience surfaces, the same trap.

The pattern worth carrying is not "this method is slow". It is that **the
ecosystem's convenient entry points are shaped for one-off use, and none of them
says so.** Anything sweeping a whole install should look for the collection
underneath the convenience method and check what it costs, rather than assuming
the obvious call is the cheap one.

## How it was measured

A whole-database sweep, timed in two phases with the rest of the work held
identical: enumerate every record, resolve its type, and count the schema fields
that would be probed. Only the resolution call differed between runs.

```
enumerate only, via TweakDB.GetRecordType   : 420,958 ms
enumerate only, via TweakDB.Records.GetRecord:      47 ms
records enumerated, both runs               : 193,354
field probes that would follow, both runs   : 3,153,870
```

Release build, one machine, one run each; the ratio rather than either absolute
number is the finding. The answers were confirmed identical by a downstream
check that is exquisitely sensitive to them: the full schema validation sweep
explains **3,150,037 of 3,306,462** stored values either way, and a single
record resolved to a wrong type would move that count.

The two methods' implementations are consistent with the timings — the pool's
lookup compiles to a dictionary access, and the database's method does not.

## What would have refuted it

- **A difference in answers.** If the fast path resolved even one record type
  differently, the validation sweep's explained count would have moved. It did
  not, and that count also fixes the number of record types found in the
  database at 843 with none unknown to the schema.
- **The cost living somewhere else in the sweep.** The two phases were timed
  separately from parsing, reflection and schema derivation, all of which were
  unchanged between runs and together account for under nine seconds.
- **A caching effect flattering the second run.** The fast path was measured
  first in one ordering and second in another, with the database re-parsed each
  time.

## Where it stops

- **One library version, one database, one machine.** No claim is made about
  other versions of the toolchain, and none about what the method does that
  makes it slow beyond what its own compiled size suggests.
- **This is about resolving a record's type, not about reading a record.**
  Nothing here measures the cost of materialising a record's values.
- **The absolute numbers are a single sample.** A different machine will produce
  different ones; the point is that the two paths are not in the same league,
  and that nothing in the surface tells you which one you are on.
