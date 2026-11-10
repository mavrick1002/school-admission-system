# School Admission System

Online, first-come-first-served school admission.

- A school admin creates classes. Each shift is its own class, for example "Class 6 - Morning Shift", with a fixed number of seats.
- A guardian enters the student's 17-digit birth registration number and applies to a class. Applying puts a seat on **hold** for a limited time (`HOLD_DURATION_SECONDS`, 10 minutes by default).
- If the admission fee is paid ("ভর্তি ফি দিন") before the hold expires, the admission is **confirmed**. If not, the hold expires and the seat goes back to the pool by itself.
- `held + confirmed` never exceeds a class's seats, however many requests arrive at once, and also with several backend processes on the same database.

Stack: .NET 10 Minimal API with EF Core and PostgreSQL 16; React 19 with TypeScript, TanStack Query and React Router; nginx; Docker Compose.

## Running it

Only Docker is needed (Docker Desktop, or Docker Engine with the Compose plugin).

```sh
docker compose up --build
```

| What | URL |
|---|---|
| UI | http://localhost:3000 |
| API | http://localhost:8080 (health check: `GET /health`) |

The first build takes a few minutes. The backend waits for PostgreSQL to be healthy, then applies its database migrations before it starts serving requests.

Data is kept in the named volume `db-data`, so it survives `docker compose down` followed by `docker compose up`. Only `docker compose down -v` deletes it.

[`api.http`](api.http) at the repository root has a request for every endpoint and error case, for manual testing from VS Code or JetBrains IDEs.

## Configuration

Every setting has a default in `docker-compose.yml`, so nothing has to be set. To change one, set it in your shell or in a `.env` file next to `docker-compose.yml` (the file is gitignored).

| Variable | Default | Used by | Meaning |
|---|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | backend | How long a hold lasts before it expires. Must be a positive whole number; otherwise the backend exits with code 1 and says why. |
| `API_BASE_URL` | `http://localhost:8080` | frontend | The backend URL the browser calls. Read when the frontend container starts, so changing it needs a restart, not a rebuild. |
| `CORS_ORIGIN` | `http://localhost:3000,http://127.0.0.1:3000` | backend | Comma-separated browser origins allowed to call the API. |
| `DB_CONNECTION_STRING` | `Host=db;Port=5432;Database=admission;Username=admission;Password=admission;Maximum Pool Size=40` | backend | Passed to the backend as `ConnectionStrings__Default`. If it is missing, the backend exits with code 1 and says why. |
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `admission` each | db, backend | Database name and credentials. The default connection string is built from them. |

Examples:

```sh
# bash
HOLD_DURATION_SECONDS=60 docker compose up -d

# PowerShell
$env:HOLD_DURATION_SECONDS = "60"; docker compose up -d
```

## Tests

All tests run in Docker, in the compose `test` profile. That profile has its own database on tmpfs and publishes no ports, so it does not touch the services above or their data.

```sh
# Backend: 38 tests over real HTTP against two API processes (api-a, api-b),
# plus one with a 2-second hold (api-short), all on the test database.
docker compose --profile test run --rm --build backend-tests

# Frontend: 21 Vitest + React Testing Library tests with a mocked fetch.
# They need no backend.
docker compose --profile test run --rm --build frontend-tests
```

The frontend tests also run without Docker with `npm ci` then `npm test` in `frontend/`.

### Output from the last run

Backend (`docker compose --profile test run --rm --build backend-tests`). The 30 table-driven validation cases are shortened to their rule names, and some are left out:

```text
  Passed Admission.Tests.ValidationTests.Invalid_requests_are_rejected_with_a_json_error_code(rule: "confirm an unknown hold") [1 s]
  Passed Admission.Tests.ValidationTests.Invalid_requests_are_rejected_with_a_json_error_code(rule: "list my holds with an invalid X-Student-Id") [15 ms]
  Passed Admission.Tests.ValidationTests.Invalid_requests_are_rejected_with_a_json_error_code(rule: "create a class with totalSeats as a string") [15 ms]
  Passed Admission.Tests.ValidationTests.Invalid_requests_are_rejected_with_a_json_error_code(rule: "apply with Bengali digits") [8 ms]
  ...
  Passed Admission.Tests.HoldOwnershipAndStateTests.Another_student_can_neither_confirm_nor_cancel_a_hold_and_the_hold_stays_held [1 s]
  Passed Admission.Tests.HoldOwnershipAndStateTests.Cancelling_a_hold_frees_the_seat_and_the_same_student_can_apply_again [45 ms]
  Passed Admission.Tests.HoldConcurrencyTests.Held_plus_confirmed_never_exceeds_total_seats_when_200_students_race_for_10_seats_across_two_api_processes [1 s]
  Passed Admission.Tests.HoldOwnershipAndStateTests.A_confirmed_admission_cannot_be_cancelled [29 ms]
  Passed Admission.Tests.HoldConcurrencyTests.Twenty_parallel_confirms_of_one_hold_all_succeed_and_confirmed_seats_rise_exactly_once [42 ms]
  Passed Admission.Tests.HoldConcurrencyTests.A_student_sending_20_parallel_applications_for_one_class_gets_exactly_one_hold [39 ms]
  Passed Admission.Tests.HoldExpiryTests.An_expired_hold_returns_its_seat_without_any_api_call_and_the_student_can_apply_again [4 s]
  Passed Admission.Tests.HoldExpiryTests.An_expired_hold_can_be_neither_confirmed_nor_cancelled [3 s]
Test Run Successful.
Total tests: 38
     Passed: 38
 Total time: 8.7411 Seconds
```

Frontend (`docker compose --profile test run --rm --build frontend-tests`):

```text
> frontend@0.0.0 test
> vitest run
 RUN  v5.1.0 /app
 ✓ src/pages/HoldPage.test.tsx (1 test) 299ms
 ✓ src/student/studentId.test.tsx (13 tests) 427ms
 ✓ src/pages/ClassListPage.test.tsx (7 tests) 448ms
 Test Files  3 passed (3)
      Tests  21 passed (21)
   Duration  4.80s (environment 63%, setup 17%, tests 9%, import 5%, transform 5%, worker 1%)
```

### Cleaning up after the tests

`run --rm` removes the test runner, but the test database and the three test API containers keep running. Remove them, and only them, with:

```sh
docker compose --profile test rm --stop --force db-test api-a api-b api-short
```

> **Warning: do not use `docker compose --profile test down -v`.**
> `down` with a profile removes the default `db`, `backend` and `frontend` containers too, and `-v` deletes the `db-data` volume, which holds every real class and application.

## Project structure

```text
.
├── docker-compose.yml          db, backend, frontend; "test" profile: db-test, api-a, api-b, api-short, backend-tests, frontend-tests
├── api.http                    manual requests for every endpoint and error case
├── backend/
│   ├── Dockerfile              build, test and runtime stages
│   ├── Admission.slnx
│   ├── src/Admission.Api/
│   │   ├── Program.cs          configuration, CORS, JSON options, startup migration, routes
│   │   ├── Data/               DbContext, entities, migrations, advisory-locked migrator, expiry sweeper
│   │   ├── Endpoints/          classes and holds endpoints, X-Student-Id validation, UTC JSON dates
│   │   └── Errors/             middleware that turns every error into JSON { code, message }
│   └── tests/Admission.Tests/  HTTP tests: concurrency, expiry, ownership and state, validation
└── frontend/
    ├── Dockerfile              build, test and nginx stages
    ├── docker/                 nginx.conf, and the script that writes config.js at container start
    └── src/
        ├── api/                fetch client, TanStack Query hooks, types, server-reachability state
        ├── components/         layout, student ID box, server-down banner, error boundary, messages
        ├── pages/              class list, create class, application, my applications, not found
        ├── student/            student ID cleaning, validation and storage
        ├── errors.ts           every backend error code in plain Bangla
        └── test/               test setup and helpers (mocked fetch)
```

See [ASSUMPTIONS.md](ASSUMPTIONS.md) for the decisions the brief left open, and [AI_USAGE.md](AI_USAGE.md) for how AI was used.
