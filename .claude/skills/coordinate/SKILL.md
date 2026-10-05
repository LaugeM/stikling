---
name: coordinate
description: Build a feature, or a batch of small features, with this session as coordinator and worker agents on Sonnet doing the implementation. Use when the user runs /coordinate or asks to work as coordinator with workers.
---

# Coordinating a feature with workers

This session plans the feature, splits it into parts, writes a brief for each part and checks the result. The `worker` agent (`.claude/agents/worker.md`) builds each part on Sonnet. The design decisions stay here.

If the whole change is small, one or two files, skip this and build it directly. A worker's start-up costs more than it saves on a small change. Several small features together are a different case: see "A batch of small features".

## Which way to build it

- **One feature that splits into parts** (Core, then storage, then screens): the steps below, one worker at a time in this worktree.
- **A batch of small, separate features**, like several rows from the feature list: one worker per feature, run in parallel. See "A batch of small features" at the end.
- **Work that is mostly judgment or writing**, like a design question, a data model change or the Help page: build it in this session. Workers are good at a clear brief with a build and tests to check it, and weaker at deciding what the feature should be.

A batch of 21 small features built with parallel workers came to about a third to a quarter of the cost of building the same kind of features on Opus alone. Most of the saving came from this session's conversation staying short, since every step re-reads the whole conversation, rather than from Sonnet's lower price.

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

Don't run these workers in parallel. Each one builds on the one before, and they share this worktree. Parallel workers are only for separate features, each in its own worktree (see the last section).

## 6. Finish as usual

Check any UI change in the browser at phone width by running the `browser-check` agent, not by driving the browser here, then follow "Before opening a pull request" in CLAUDE.md. Don't review the finished branch in this session. Run the `reviewer` agent, which reads it with a fresh context.

## 7. Report

In the chat, not in the pull request, tell the user:

- the plan usage before and after
- how many workers ran, and how many were sent back for fixes
- what the reviewer found

## A batch of small features

For features that touch different parts of the app, each worker builds one whole feature (Core, pages, Help and `docs/FEATURES.md`) in its own worktree, and several run at once.

1. **Pick features that don't overlap.** Two features on the same page or the same service will conflict. Put those in different rounds. Check `docs/FEATURES.md` and the code first, since a feature can be partly built already.
2. **Write the decisions into each brief.** A worker can't ask, so settle anything the user would notice: what a field is called, where it shows, what happens to old values. If a decision is really the user's, ask before starting that worker. Name `Pages/Help.razor` and `docs/FEATURES.md` in the brief along with the code, since the worker only changes the files it is given.
3. **Start every worker from the current branch.** Commit what is in this worktree first. Then pass `isolation: "worktree"` and begin each brief with `git merge <sha>`, this branch's latest commit, since an agent's worktree starts from `main`. The worker commits on its worktree's branch and reports the branch name, as `worker.md` allows in a batch.
4. **Merge each result as it reports.** Read the diff, merge the branch into this one, and build. Expect conflicts in `docs/FEATURES.md` and `Pages/Help.razor`, where every worker adds a line. Resolve them here.
5. **Send design problems back.** When a worker built the wrong thing, like duplicating a field that already exists, send it back with SendMessage and say what to change. Fix small things directly.
6. **Start the next round** from the new commit while checking the last one in the browser.

When a round is merged, build, run the tests, run the `browser-check` agent on the changed screens and run the `reviewer` on the whole branch, as in "6. Finish as usual". Keep each pull request to one or two rounds, so the user has a reasonable amount to review.
