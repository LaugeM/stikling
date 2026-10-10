---
name: coordinate
description: Build an approved plan with worker agents on Sonnet, with this session as coordinator. Use it whenever a plan is approved in an Opus session and the build is more than one or two files, instead of asking the user to switch models. Also use it when the user runs /coordinate for a batch of small features.
---

# Coordinating a build with workers

This session plans the feature, writes a brief for the worker and checks the result. The `worker` agent (`.claude/agents/worker.md`) builds it on Sonnet. The design decisions stay here.

This is the default way to build an approved plan. Don't ask the user to switch models. Switching writes the whole planning conversation to the cache again, and every build turn re-reads it. A worker starts with only the brief.

If the whole change is small, one or two files, skip this and build it directly. A worker's start-up costs more than it saves on a small change. Several small features together are a different case: see "A batch of small features".

## Which way to build it

- **One feature from an approved plan**: the steps below, in this worktree.
- **A batch of small, separate features**, like several rows from the feature list: one worker per feature, run in parallel. See "A batch of small features" at the end.
- **Work that is mostly judgment**, like a design question or a data migration whose shape isn't settled: build it in this session. Workers are good at a clear brief with a build and tests to check it, and weaker at deciding what the feature should be. Once a migration is designed (the `DB_VERSION` bump, the `db.js` block, how old records are converted), a worker can write it from the brief.

A batch of 21 small features built with parallel workers came to about a third to a quarter of the cost of building the same kind of features on Opus alone. Most of the saving came from this session's conversation staying short, since every step re-reads the whole conversation, rather than from Sonnet's lower price.

## 1. Note the usage

When the user ran `/coordinate`, note the plan usage (weekly and 5-hour percentage) before anything else. It's reported at the end.

## 2. Plan

Plan the feature as usual and get the plan approved before any worker starts. An approved plan in plan mode is the go-ahead. Outside plan mode, show the plan and wait for the user's go.

Settle the design questions in the plan, not in the brief. Anything the worker would have to ask about is a question to settle now, and if it's a product decision, ask the user now, before the worker starts.

## 3. One worker, or a few parts

Give the whole plan to one worker when it fits: a feature that one focused session could build without its context getting long. Every worker reads its own context from scratch, so more workers cost more.

Split it into two or three parts only when it is large, or when a check between parts catches problems early. Split in the order the parts depend on each other, so each part can be finished and checked with the build and tests on its own. For a large feature:

1. the model, repository interface, service and tests in `Stikling.Core`
2. the IndexedDB side and the backup, following `docs/dev/new-record.md`
3. the pages, components, Help answers, empty states and `docs/FEATURES.md`

## 4. Write a brief the worker can finish

The worker only sees the brief, CLAUDE.md and the docs they point to. Every question it sends back costs a round trip in this session, which is the expensive one, so the brief leaves nothing open that the worker can't settle from the code.

Before writing it, check every premise. Open the files the brief will name and confirm that the methods, fields, components and docs it mentions exist and work the way the plan assumes. A premise that turns out false is the most common reason a worker stops. While you're there, find the existing code each piece should copy.

The brief says:

- what the change is for and how someone uses it, in a few sentences
- the files to change, and for each the existing file to follow as a pattern
- every decision from the plan, written as decided, not as options. That includes names of fields, enum values and public methods, the text people see and where it shows, what happens when a list is empty or something fails, and how existing data is handled.
- the rules in CLAUDE.md that apply, named rather than repeated, and the docs under "Docs for some tasks" that the part needs
- the Help answers, What's new line and feature list rows the change needs, with what each should say
- what is out of scope, including work that belongs to a later part
- which tests should exist and pass
- that the browser check, the reviewer, commits and the pull request stay with this session
- what the worker may decide itself: anything the code has a pattern for, names nobody sees, the layout of tests, small layout details `DESIGN.md` settles

Give the conclusions, not the discussion. The options you weighed while planning only add things to read.

Then read the brief once as the worker will, without this conversation. Anything you would have to ask about, decide and write in.

## 5. Check each worker's result

When the build is split into parts, start one worker, wait for its report, and check it before the next one starts:

- `git status` and `git diff`: every changed file should be one the brief allowed
- the report should show the build and tests passing. If it doesn't, run them.
- read the decisions the worker made on its own, and change any you disagree with
- fix small problems directly
- for a real problem, send it back to the same worker with SendMessage instead of starting a new one, so it keeps what it already read
- if the worker stopped on a question, answer it with SendMessage, or ask the user first when it's a product decision. Then note what the brief was missing, so the next brief covers it.

Don't run the parts in parallel. Each one builds on the one before, and they share this worktree. Parallel workers are only for separate features, each in its own worktree (see the last section).

## 6. Finish as usual

Check any UI change in the browser at phone width by running the `browser-check` agent, not by driving the browser here, then follow "Before opening a pull request" in CLAUDE.md. Don't review the finished branch in this session. Run the `reviewer` agent, which reads it with a fresh context.

## 7. Report

In the chat, not in the pull request, tell the user:

- the plan usage before and after, when it was noted
- how many workers ran, how many came back with questions and how many were sent back for fixes
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
