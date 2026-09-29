---
name: coordinate
description: Build a feature with this session as coordinator and worker agents on Sonnet doing the implementation. Use when the user runs /coordinate or asks to work as coordinator with workers.
---

# Coordinating a feature with workers

This session plans the feature, splits it into parts, writes a brief for each part and checks the result. The `worker` agent (`.claude/agents/worker.md`) builds each part on Sonnet. The design decisions stay here.

If the change is small, one or two files, skip this and build it directly. A worker's start-up costs more than it saves on a small change.

## 1. Note the usage

Before anything else, note the plan usage (weekly and 5-hour percentage). It's reported at the end.

## 2. Plan

Plan the feature as usual and get the plan approved before any worker starts. Settle the design questions in the plan, since the workers can't ask.

## 3. Split into a few large parts

Split the plan into two to four parts, in the order they depend on each other. Each part should be something a worker can finish and check with the build and tests on its own. For a typical feature:

1. the model, repository interface, service and tests in `Stikling.Core`
2. the IndexedDB side and the backup, following "Adding a new kind of record" in CLAUDE.md
3. the pages and components
4. the Help page answers, empty states and `docs/FEATURES.md`

More, smaller parts cost more, not less. Every worker reads its own context from scratch.

## 4. Write the brief

The worker only sees the brief and CLAUDE.md. Each brief says:

- what this part is for, and where it sits in the whole feature
- the files to change, and the existing code to follow as a pattern
- the decisions from the plan that affect this part
- the rules in CLAUDE.md that apply, named rather than repeated
- what is out of scope, including work that belongs to a later part
- how to check it: which tests should exist and pass

## 5. Run the workers one at a time

Start one worker, wait for its report, then check it before the next one starts:

- `git status` and `git diff`: every changed file should be one the brief allowed
- the report should show the build and tests passing. If it doesn't, run them.
- fix small problems directly
- for a real problem, send it back to the same worker with SendMessage instead of starting a new one, so it keeps what it already read
- if the worker stopped on a question, decide it here, or ask the user when it's a product decision

Don't run workers in parallel. Each one builds on the one before, and they share this worktree.

## 6. Finish as usual

Check any UI change in the browser at phone width, then follow "Before opening a pull request" in CLAUDE.md. Don't review the finished branch in this session. Run the `reviewer` agent, which reads it with a fresh context.

## 7. Report

In the chat, not in the pull request, tell the user:

- the plan usage before and after
- how many workers ran, and how many were sent back for fixes
- what the reviewer found
