# AI usage

## Tools

- **Claude Code in VS Code** wrote the code.
- **Claude chat** helped plan the phases and review each result.

I ran every phase's checks myself before committing.

## Where the AI went wrong, how it was found, and how it was fixed

| What went wrong | How it was found | How it was fixed |
|---|---|---|
| The assignment PDF's text layer was corrupted. | The copied text was garbled. | The AI read the pages as images to build the spec, and I checked the spec against the PDF myself. |
| A student who already had a hold in a full class got `CLASS_FULL`. | While reviewing the "choices you might question" part of its report. | I made it check the student's existing hold first and return `ALREADY_APPLIED` with the `holdId`, then tested it in `api.http`. |
| An invalid `HOLD_DURATION_SECONDS` crashed with exit code 139, and so did a missing connection string. | In its report. Exit code 139 means a crash. | I asked for a clear message and exit code 1. |
| It left the empty `UnitTest1` test and the Vite template README. | While reading its reports. | I had both removed. |
| A `GET /classes` response that was not an array silently showed an empty list. | From its list of limitations. | I made it show the server error instead. |
| The first concurrency test only checked the final count. | When I asked for more tests. | It now checks every reading during the race. |
| The page kept showing "will expire at" next to `EXPIRED`. | The AI's own frontend test run. | I had the line hidden and added a test for it. |
| CORS allowed only `localhost:3000`, so opening the UI at `127.0.0.1:3000` broke every API call. | While reviewing the README. | I had both origins allowed by default. |
