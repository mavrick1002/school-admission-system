# Assumptions and decisions

## No overbooking

Applying runs in one transaction:

1. `SELECT ... FROM classes WHERE id = ? FOR UPDATE` locks the class row. Every apply, confirm and cancel for that class queues on it, so the count below cannot change before the insert.
2. `HELD` holds of that class past `expires_at` are set to `EXPIRED`, judged by `clock_timestamp()`. That is the database's current time at each statement, not the transaction start time that `now()` gives, so a request that waited on the lock does not use a stale time, and the API servers' clocks do not matter.
3. If the student already has a `HELD` or `CONFIRMED` hold in the class: `409 ALREADY_APPLIED` with its `holdId`.
4. `HELD` and `CONFIRMED` holds are counted. If the class is full: `409 CLASS_FULL`; otherwise the hold is inserted.

A partial unique index on `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')` also makes the database enforce one active application per student per class. A violation (`23505`) is still answered with `409 ALREADY_APPLIED`.

**Two backend processes:** the lock, the index and the clock all live in PostgreSQL, and the API keeps no in-memory locks or counters, so a second process queues on the same row lock. A backend test races 200 students across two API processes for 10 seats: exactly 10 get a hold, and no reading during the race shows `held + confirmed` above `totalSeats`.

**Why a row lock rather than a stored counter:** holds expire by time, with no write, so a counter would stay wrong until something corrected it, and it would have to be kept in step with every confirm, cancel and expiry. Counting holds under the lock keeps the holds the only source of truth. The cost is that requests for the same class run one at a time, a few milliseconds each; other classes are unaffected.

## Other decisions

- **Seat counts** are computed from holds on every read; no counter is stored.
- **Expiry:** reads treat a lapsed `HELD` hold as `EXPIRED` immediately, and a background service marks such holds `EXPIRED` every 5 s with `FOR UPDATE SKIP LOCKED`, which is safe with several processes.
- **Confirm is idempotent:** repeating it on a `CONFIRMED` hold returns `200` and `confirmedSeats` rises only once.
- **A `CONFIRMED` admission cannot be cancelled** (`409 HOLD_CONFIRMED`): the fee is paid, and refunds are out of scope.
- **Cancelling an already `CANCELLED` hold** returns `204`, so `DELETE` is idempotent; the student may then apply again.
- **`GET /holds/{id}` needs no `X-Student-Id`**, as in the brief; hold IDs are random UUIDs.
- **IDs** are UUIDv7 strings, and classes are listed in creation order.
- **Duplicate class names are allowed**; the brief does not require uniqueness, and a class is identified by its ID.
- **`expiresAt` has milliseconds**, for example `2026-11-01T04:10:00.123Z`; it is still ISO 8601 UTC with `Z`.
- **`secondsRemaining` is added** to hold responses, from the database clock, `0` when not `HELD`; the brief allows extra fields.
- **Student ID** must be exactly 17 ASCII digits (`^[0-9]{17}$`); `\d` would also accept Bangla digits.
- **The UI is in Bangla and the API in English:** the brief, its button labels and its users are Bangla-speaking, while the API contract is English.
- **The student ID is kept in `localStorage`**, inside `try/catch`, so applications are still visible after the browser is closed and reopened, without a login.
- **The admin screen is open**, because the brief says no login is needed.
- **Migrations** run at startup under a PostgreSQL advisory lock, so processes that start together do not migrate twice.
- **Bad configuration** (an invalid `HOLD_DURATION_SECONDS` or a missing connection string) exits with a clear message and code 1.

## Status and error codes

Every error is JSON `{ "code": "...", "message": "..." }`. The header is checked before the path ID, so a missing header gives `400` even when the ID is unknown.

| Status | Codes |
|---|---|
| 200 | `GET` requests; confirm, including a repeat |
| 201 | create class (`Location: /classes/{id}`); apply (`Location: /holds/{id}`) |
| 204 | cancel, including an already cancelled hold |
| 400 | `MISSING_STUDENT_ID`, `INVALID_STUDENT_ID`, `VALIDATION_ERROR` (empty name, `totalSeats` missing or below 1), `INVALID_JSON` (malformed body or wrong types), `BAD_REQUEST` |
| 403 | `NOT_YOUR_HOLD`: confirm or cancel of another student's hold |
| 404 | `CLASS_NOT_FOUND`, `HOLD_NOT_FOUND` (unknown or non-GUID ID), `NOT_FOUND` (unknown route) |
| 405 | `METHOD_NOT_ALLOWED` |
| 409 | `CLASS_FULL`, `ALREADY_APPLIED`, `HOLD_EXPIRED`, `HOLD_CANCELLED` (confirm of a cancelled hold), `HOLD_CONFIRMED` (cancel of a confirmed admission) |
| 413 / 415 | `PAYLOAD_TOO_LARGE`, `UNSUPPORTED_MEDIA_TYPE` |
| 500 | `INTERNAL_ERROR`, for example when the database is down |
