# User story

> **As a** busy professional juggling work and personal to-dos,
> **I want** a private list of my tasks with a status and a due date,
> **so that** I can see at a glance what's pending, what's in progress and what's overdue.

## Acceptance criteria

**Account**
- I can register with my email and a password, and my account is stored.
- An email can only be registered once, regardless of letter case or surrounding spaces.
- The password must be at least 8 characters.
- I can log in with my email and password. A wrong email or a wrong password gives the same error, so nobody can find out which emails are registered.
- When my session expires, I'm sent back to the login screen.

**Tasks**
- A task has a title (required, up to 100 characters), an optional description (up to 1000), an optional due date and a status.
- A new task starts as *To do*.
- I can't create a task with a due date in the past. I can still edit a task that has become overdue.
- I move a task from *To do* to *In progress* to *Done*, and I can reopen a done task.
- I can see all my tasks, filter them by status, and overdue tasks stand out.
- I can edit and delete my tasks.

**Privacy**
- I only ever see and change my own tasks. To me, someone else's task doesn't exist.

**Demo**
- A demo account with tasks in every status, including an overdue one, is ready after setup.
