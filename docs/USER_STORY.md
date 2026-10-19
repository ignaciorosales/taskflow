# User story

> **As a** busy professional juggling work and personal to-dos,
> **I want** a private list of my tasks with a status, a priority and a due date,
> **so that** I can see at a glance what's pending, what's in progress and what's overdue.

## Acceptance criteria

**Account**
- I can register with my email and a password, and my account is stored. Registering logs me in.
- The email must have a valid format. It can only be registered once, regardless of letter case or surrounding spaces; trying again tells me it's already registered.
- The password must be at least 8 characters and contain at least one letter, one digit and one symbol.
- I can log in with my email and password. A wrong email or a wrong password gives the same error.
- After 3 failed logins for an email, that email is locked for 1 minute, whether it's registered or not. Login and register are also rate limited per IP.
- My session stays alive while I use it: a 15-minute access token, renewed with a 7-day refresh token that is replaced on every use. When it can't be renewed, I'm sent back to the login screen.
- I can log out, and my refresh token stops working.

**Tasks**
- A task has a title (required, up to 100 characters), an optional description (up to 1000), a priority, an optional due date and a status. Title and description are trimmed, and the limits count after trimming; a title of only spaces is empty.
- Priority is *Low*, *Medium* or *High*, and defaults to *Medium*.
- The due date is a date and time, in UTC. When I set or change it, it can't be in the past, and once set it can't be removed. I can still edit the other fields of a task that has become overdue.
- A new task starts as *To do*. The statuses are *To do*, *In progress* and *Done*, and I can move a task between any of them with buttons. Reopening a done task moves it to *In progress*.
- When a task becomes *Done* its completion time is recorded; it's cleared if the task leaves *Done*.
- A task is overdue when its due date has passed and it isn't *Done*. Overdue tasks stand out, and a task completed after its due date shows as *Completed late*.
- I can see all my tasks, a page at a time. I can filter by status (one at a time), priority and overdue, and sort by due date, priority or creation date, in either direction.
- I can edit my tasks. I can delete them after confirming; deleted tasks are kept as soft-deleted but no longer exist for me.
- If I edit the same task in two places, the last save wins.

**Privacy**
- I only ever see and change my own tasks. To me, someone else's task doesn't exist.

**Demo**
- A demo account with tasks in every status, including an overdue one, is ready after setup. Its dates are relative to the setup time, so the overdue task is overdue right away.
- Its credentials are shown on the login screen.
- Running setup again doesn't duplicate or reset anything; data persists in the database.
