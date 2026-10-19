# Prompts

The prompts I used to build TaskFlow with Claude Code, in order.

How I worked:
- **Requirements and rules live in files, not in prompts.** `CLAUDE.md` holds the stack and working rules, and `docs/USER_STORY.md` holds the business rules. Each prompt only says what's new for that step.
- **Ambiguities surfaced before code.** The AI reviews the user story and asks questions first; I make the product decisions. Mid-step questions come through Claude Code's interactive prompts: the AI lays out the options with their trade-offs and marks the one it recommends, and I pick one or type my own answer. A decision it can't settle alone stops the work until I answer.
- **TDD on every layer.** Red → green → refactor, one commit per phase, so the git history shows it.
- **One step per prompt**, and I review before the AI continues.
- **A model for each job.** My usual split is Fable for planning (user story review, solution design, adversarial review) and Opus for development (the TDD steps, fixes, docs), switching with `/model`.

How I verify AI output:
- **I review the tests before any implementation exists.** A test states *what* must happen in a few lines, so that's where my judgment goes: missing cases, tests that prove nothing, misread rules.
- **Every test is seen failing for the right reason** (a failed assertion, not a compile error) before it goes green.
- **Mutation check:** I break a rule on purpose and confirm that a test catches it.
- **Review in a fresh session**, so the reviewer doesn't share the context that wrote the code.

The *Notes* line under each prompt records what the AI got wrong and how I caught it.

---

## 0. Context (`CLAUDE.md`)

```
.NET 10 Web API (controllers) + React/TS. Requirements: @docs/USER_STORY.md
Clean Architecture: Domain has no dependencies, Application owns the ports, Infrastructure implements them.
MongoDB with the official driver. No EF, Dapper, MediatR or AutoMapper.
Expected failures return a Result, not exceptions. Errors go out as ProblemDetails. Time goes through TimeProvider.
Structured logging with ILogger; never log passwords or tokens.
xUnit + NSubstitute; Testcontainers for anything that touches Mongo.

TDD always: red, then green, then refactor. Never write implementation in the red phase,
never change tests in the green phase. After the red phase, show me the tests and the failure output and wait.
Run the tests after every change and show me the result.
Commits: test: for red, feat: for green, refactor: for refactor.

Ask before adding any NuGet or npm package.
Do only the step I ask for. If something is ambiguous, ask before assuming.
```

## Reusable prompts

**Green**
```
Make the failing tests pass with the minimum implementation. Don't touch the tests.
```

**Refactor**
```
All green. Refactor for readability and duplication without changing behavior; tests must stay green.
If nothing is worth changing, say so instead of changing it.
```

**Mutation check**
```
Break the riskiest rule we just implemented, run the tests and show me which test fails. Then revert.
```

---

## 1. User story

```
This is my user story. Before any design, list what it leaves ambiguous
and the edge cases it doesn't cover, and ask me about them. Don't propose solutions yet.
```
<details>
<summary>The AI's questions (round 1)</summary>

1. Validate email format, or only non-empty? Max length?
2. Password: anything beyond the 8-character minimum? Are surrounding spaces kept?
3. Registering a taken email: can it say "already registered", or must it hide that like login does?
4. After registering: logged in automatically, or sent to login?
5. Session: how long? Logout? Refresh / remember me?
6. Failed logins: attempt limit or lockout?
7. Are title/description trimmed? Is a whitespace-only title empty? Limits counted after trimming?
8. Due date: date only, or date and time?
9. "In the past" in whose timezone? Is today allowed?
10. When is a task overdue? Can a Done task be overdue?
11. Allowed transitions: skips (To do → Done), going back (In progress → To do)? Where does reopen land?
12. Is status changed in the edit form, or as its own action?
13. On edit: can the due date be set to a new past date? Cleared?
14. List order? Paging or a limit?
15. Filter by one status or several? Is "overdue" a filter?
16. Delete: hard or soft? Confirmation?
17. Concurrent edits: last write wins?
18. Demo credentials: where are they shown?
19. The overdue demo task can't be created through the normal rules; seed it relative to setup time?
20. Re-running setup: reset demo tasks or leave them?
</details>

<details>
<summary>Round 2: follow-ups on unclear or conflicting answers</summary>

- **Register vs. login leak:** I first said both must hide whether an email exists, which conflicts with logging in right after registering. The AI explained account enumeration (credential stuffing, targeted phishing) and the options: a generic reply, email verification, or reveal it with a rate limit. Decision: reveal "already registered", log in on success, and rely on a rate limit that also covers register. Logged as a trade-off.
- **Anti-bot:** the server can't see browser tabs, so attempts are counted per email (registered or not, so the lockout doesn't leak) plus a rate limit per IP.
- **Finished after the due date:** not a new status; a "Completed late" badge derived from the completion time.
- **Paused status:** dropped. Three statuses, any move allowed, as in a real board.
- **Status changes:** action buttons, not a field in the edit form.
- **Due date on edit:** can't be moved into the past or removed.
- **Priority:** new field, Low / Medium / High, optional, defaults to Medium. Filter and sort by it.
- **Overdue demo task:** the seeder writes straight to the database with dates relative to setup time, so it's overdue right after setup.
- **Re-running setup:** idempotent; it creates the demo account only if it's missing.
</details>

My answers:
```
Account
- Email must have a valid format; trimmed and compared case-insensitively.
- Password: at least 8 characters, with at least one letter, one digit and one symbol.
- Registering a taken email says "email already registered". Accepted trade-off: it reveals the
  email exists; the rate limit below slows enumeration. Login keeps the single generic error.
- A successful registration logs the user in.
- Session: 15-minute access token, 7-day refresh token rotated on every use. When the refresh
  fails, back to login. A logout button revokes the refresh token.
- Anti-bot: 3 failed logins for an email lock it for 1 minute, whether it's registered or not.
  Login and register are also rate limited per IP.

Tasks
- Title and description are trimmed and limits count after trimming. A whitespace-only title is empty.
- Priority: Low, Medium or High. Optional, defaults to Medium.
- Due date is a date and time, in UTC. When set or changed it can't be before now, and once set
  it can't be removed. Editing other fields of an overdue task keeps its past due date.
- Statuses: To do, In progress, Done. Any move is allowed; reopening a Done task moves it to
  In progress. Status changes through action buttons, not the edit form.
- Completion time is recorded when a task becomes Done and cleared when it leaves Done.
- Overdue: due date before now and not Done. A task completed after its due date shows "Completed late".
- List: filter by status (one at a time), priority and overdue; sort by due date, priority or
  creation date, ascending or descending; simple paging.
- Delete is soft and the UI asks for confirmation. To the user, a deleted task doesn't exist.
- Concurrent edits: last write wins.

Demo
- Demo credentials are shown on the login screen.
- The seeder writes the demo tasks directly, with dates relative to setup time, so the overdue
  one is overdue right after setup.
- Setup is idempotent: it creates the demo account only if it's missing. Data persists in Mongo.
```
Then:
```
Update the acceptance criteria with my answers. Don't add any rule I didn't give you.
```
*Notes:*

## 2. Plan

```
Propose the solution structure following CLAUDE.md. Show me the project references
and list the assumptions you're making so I can validate them.
```
<details>
<summary>The AI's questions</summary>

1. Docker isn't on the shell's PATH; Testcontainers and compose need it. Is it installed? → Yes (Docker Desktop, daemon running).
2. Refresh-token reuse detection (a reused, already-rotated token revokes all of that user's tokens)? → Yes.

Result: [docs/PLAN.md](PLAN.md).
</details>
*Notes:*

## 3. Scaffold

```
Scaffold the solution we agreed on. Projects and references only, no classes yet.
Central Package Management, warnings as errors, and it must build clean.
```
<details>
<summary>The AI's questions</summary>

1. The xUnit template adds `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` and `coverlet.collector`. Approve? → Yes. The `Microsoft.AspNetCore.OpenApi` package added by the webapi template was removed until step 8.
</details>
*Notes:*

## 4. Domain: red

```
Red phase. Tests for the TaskItem and User entities from the user story's rules. Cover the boundaries.
Keep the entities rich: the tests should expect them to enforce their own invariants.
```
*Notes:*

## 5. Application: red

```
Red phase. Tests for a TaskService on top of a repository port, covering the task and privacy criteria.
Someone else's task must be indistinguishable from one that doesn't exist.
```
*Notes:*

## 6. Infrastructure: red

```
Red phase. Integration tests for the Mongo repositories against a real Mongo via Testcontainers; don't mock the driver.
One container shared per test class. Email uniqueness must be enforced by the database,
and a duplicate comes back as a Result, not an exception.
```
*Notes:*

## 7. Identity API

```
Red phase. Tests for auth: register, login returning a JWT, a protected /me and a public health check.
Unit tests for the AuthService, integration tests through WebApplicationFactory for the endpoints.
```
Green:
```
Hash passwords with ASP.NET's PasswordHasher. The JWT key comes from the environment; fail at startup if it's missing.
Use the built-in health checks and rate limit the login endpoint.
```
*Notes:*

## 8. Tasks API

```
Red phase. Integration tests for the Tasks API: CRUD, every endpoint authorized, user taken from the token.
Prove that user A can't read, edit or delete user B's tasks, and that a missing token gives 401.
```
Green:
```
Keep controllers thin and map Result to HTTP in one place. Unexpected errors go through IExceptionHandler.
Built-in OpenAPI with Scalar as the UI, with Bearer auth.
```
*Notes:*

## 9. Seed + Docker

```
Add an idempotent seeder for the demo account in the user story. Then a docker-compose so a clean clone
runs with one command. The browser sees a single origin: Vite proxy in dev, nginx in compose,
routing /api/auth and /api/tasks to each API. No CORS.
```
*Notes:*

## 10. Frontend

```
Red phase. Vitest + Testing Library tests for the task form, the tasks hook and the tasks screen:
server validation errors are shown in the form, and a 401 logs the user out. The tasks screen is a kanban
board with one column per status: dragging a card onto another column moves it, New task and Edit open
a modal dialog, and each column loads more on demand.
```
Green:
```
React frontend for the user story: login/register and a kanban board of tasks. Cards are dragged between
the status columns with native drag and drop; keep the status buttons for keyboard and touch. Moves apply
right away and roll back if the server refuses. New task and Edit open a modal dialog instead of pushing
the page down. Feature-based folders, a typed API client, auth in a context. Keep state ordered: server
data in the feature's hook, UI-only state (open dialog, drag) in the component that owns it, no global store.
Responsive without a UI library: three columns on wide screens, stacked on narrow ones, usable on a phone.
Loading, empty and error states. No console warnings under StrictMode.
```
*Notes:*

## 11. Adversarial review (fresh session)

```
Review this repo the way an interviewer would: architecture leaks, missing ownership checks, timezone bugs,
tests that only prove the happy path. Don't fix anything; list the issues by severity with file and line.
```
*Notes:* One high finding: editing the demo's overdue task failed, because the form rounded the due date to the minute and the server saw it moved into the past. The tests had missed it because they only used due dates that fell exactly on a minute. Three medium findings: a missing priority or status silently fell back to a default, a due date without an offset was read in the server's time zone, and nothing tested that the frontend runs a single refresh. Ten low findings. Ownership checks held everywhere.

## 12. Fix review findings

```
Fix the high-severity issues one at a time. For each bug, first write the failing test that reproduces it.
```
*Notes:* I fixed the high and medium findings, each red then green, one commit per phase. The mutation check caught a test that proved nothing: the StrictMode test still passed with the shared refresh removed, because a `<StrictMode>` wrapper in `renderHook` didn't run effects twice. Switching to `reactStrictMode: true` fixed it. A probe showed that ASP.NET reports JSON errors under keys like `$.dueDate`, which the form can't match to a field, so the fix also renames them to plain field names.

## 13. Docs + CI

```
Write the README: what it is, the user story, one-command setup, demo credentials, how to run the tests and coverage.
Add docs/ARCHITECTURE.md with a Mermaid diagram of the layers and the main decisions with their trade-offs.
Then a GitHub Actions workflow that builds and tests on every push, with Docker for Testcontainers, and its badge in the README.
```
*Notes:* The docs quote no coverage percentage. Each test project's coverage file includes the other projects it loads, so the files overlap and there's no single honest figure without adding ReportGenerator. CI passed on the first run, including the Testcontainers tests.

---

## GenAI exercise

My answer is the sequence above. If I had to condense it into one prompt:

```
Build a REST API in ASP.NET Core 10 for task management.

User story: as a user I want a private list of tasks so I can track what's pending and overdue.
A task has a title (required, max 100), description (optional), status (Todo, InProgress, Done) and due date
(optional, not in the past on creation). Each task belongs to the authenticated user; User already exists
and the user id comes from the JWT.

Rules:
- Full CRUD. Users only ever see their own tasks; someone else's task returns 404, same as a missing one.
- Clean Architecture: validation and business rules in a service layer, a repository over MongoDB. No EF.
- Errors as ProblemDetails, with validation errors per field.

Work test-first: state your assumptions, then give me the service tests for validation and ownership,
and wait for my review before writing the implementation.
```

**Output:** this repository. The prompts above produced it, and the git history shows each step as red, green and refactor commits. A representative sample for this prompt:
- [`TasksController.cs`](../src/TaskFlow.Tasks.Api/Controllers/TasksController.cs): the CRUD endpoints, with the owner taken from the token.
- [`TaskService.cs`](../src/TaskFlow.Application/Tasks/TaskService.cs): validation, ownership and soft delete.
- [`TaskItem.cs`](../src/TaskFlow.Domain/Tasks/TaskItem.cs): the task rules (title, due date, status).
- [`TaskServicePrivacyTests.cs`](../tests/TaskFlow.Application.Tests/Tasks/TaskServicePrivacyTests.cs): the ownership tests written before the implementation.

*Notes:*
