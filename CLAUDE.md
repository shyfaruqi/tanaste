# CLAUDE.md — Tanaste Project Guide

> **Who reads this file?**
> Every Claude session working on this repository reads this file automatically.
> It is the single source of truth for how to behave, communicate, and contribute to Tanaste.

---

## 1. Project Overview

**Tanaste** is a personal media library manager that runs entirely on your own computer — no cloud, no subscription, no data leaving your machine.

Think of it like a smart filing cabinet for your digital media collection. You point it at a folder on your hard drive, and it:

1. **Discovers** every book, video, and comic in that folder automatically.
2. **Reads the metadata** embedded in each file (title, author, year, cover art, etc.).
3. **Deduplicates** your collection — if you have two copies of the same file, it notices and keeps only one record.
4. **Organises** everything into a tidy, searchable library called a *Universe*, made up of *Hubs* (collections) containing individual *Works* (titles).
5. **Serves a web interface** so you can browse, search, and manage your library from a browser.
6. **Pushes live updates** to your browser the moment a new file is detected — no page refresh needed.

The product is designed for a technically capable individual user who wants full control over their media collection without relying on third-party services.

---

## 2. Technical Stack

> **Note to Claude:** Translate all technical terms into plain English when communicating with the Product Owner. The list below is for reference during development, not for user-facing communication.

| What it does | Tool / Technology |
|---|---|
| Programming language | C# (.NET 10) |
| Database | SQLite — a single file on disk; no separate database server needed |
| Backend web server | ASP.NET Core — serves the API and handles browser connections |
| Frontend (browser UI) | Blazor Server — the web interface; runs on the server, renders in the browser |
| UI component library | MudBlazor 9 — pre-built interface components (buttons, cards, grids) |
| Real-time updates | SignalR — pushes live events (e.g. "new file added") to the browser without a refresh |
| EPUB reading | VersOne.Epub — reads book file metadata |
| API documentation | Swashbuckle — auto-generates interactive API docs at `/swagger` |
| Automated tests | xUnit — runs checks after every code change to catch regressions |
| Version control | Git + GitHub (`shyfaruqi/tanaste`) |

**Project layout (plain English):**

| Folder | Plain-English description |
|---|---|
| `src/Tanaste.Domain` | The rulebook — core business concepts with no external tools |
| `src/Tanaste.Storage` | The filing clerk — reads and writes the SQLite database |
| `src/Tanaste.Intelligence` | The analyst — scores, deduplicates, and resolves conflicting metadata |
| `src/Tanaste.Processors` | The scanner — opens each file type and extracts its info |
| `src/Tanaste.Ingestion` | The mail room — watches folders and queues new files for processing |
| `src/Tanaste.Api` | The reception desk — exposes all features over HTTP to the browser |
| `src/Tanaste.Web` | The showroom — the browser interface the user actually sees |
| `tests/` | The quality inspector — automated checks for each module |

---

## 3. Communication Rules

**Claude must follow these rules in every session:**

### 3.1 — Audience-first language
The Product Owner is **not a developer**. All explanations must use plain English.

| Instead of saying… | Say… |
|---|---|
| "We'll refactor the repository pattern" | "We'll reorganise how the app reads and writes data" |
| "The DI container resolves scoped services" | "The app creates a fresh copy of this tool for each browser tab" |
| "SignalR fires an event on the WebSocket" | "The server tells the browser something changed, instantly" |
| "The Blazor circuit is disposed" | "The browser tab is closed and the app cleans up" |
| "CS0234 assembly reference error" | "The app is missing a library it needs — we'll add it" |
| "Null reference exception" | "The app tried to use something that wasn't there yet — we'll add a safety check" |
| "We need to update the csproj" | "We need to update the project's ingredient list" |

### 3.2 — Decisions require sign-off
Before writing any code, Claude must briefly explain:
- **What** is about to change (one sentence)
- **Why** (what problem it solves or what feature it adds)
- **Any trade-offs** the Product Owner should know about

Wait for a "go ahead" or equivalent confirmation before proceeding.

### 3.3 — Honest about uncertainty
If Claude is unsure about a technical approach, it must say so plainly — never guess silently. Example: *"I'm not certain which version of this tool to use — I'll check and come back to you."*

### 3.4 — Error reporting
When something fails to build or a test breaks, Claude must:
1. Say **what went wrong** in plain English.
2. Explain **what it will do** to fix it.
3. Fix it without asking for permission (errors during an approved task are part of the task).

---

## 4. License Compliance (AGPLv3)

> **This project is licensed under the GNU Affero General Public License v3.0 (AGPLv3).**

**What this means for every Claude session:**

- **AGPLv3 is a strong copyleft ("share-alike") license.** Any code that uses or is built on Tanaste must also be released under AGPLv3 if it is deployed over a network.
- **Every new dependency added must be license-compatible.** Before adding any new library or tool, Claude must verify that its license is compatible with AGPLv3.

**Compatible licenses (safe to use):**

| License | Compatible? |
|---|---|
| MIT | ✅ Yes |
| Apache 2.0 | ✅ Yes |
| BSD (2-clause or 3-clause) | ✅ Yes |
| LGPL v2.1 / v3 | ✅ Yes (with care) |
| GPLv2 | ⚠️ Only if "or later" clause is present |
| GPLv3 / AGPLv3 | ✅ Yes |
| SSPL | ❌ No |
| Commons Clause | ❌ No |
| Proprietary / commercial-only | ❌ No |

**If Claude is unsure about a dependency's license**, it must say so before adding the package and ask the Product Owner for a decision.

**Current dependencies and their licenses** (verified at time of writing):

| Package | License |
|---|---|
| Microsoft.Data.Sqlite | MIT |
| Microsoft.Extensions.* | MIT |
| Microsoft.AspNetCore.* | MIT |
| MudBlazor | MIT |
| Microsoft.AspNetCore.SignalR.Client | MIT |
| VersOne.Epub | MIT |
| Swashbuckle.AspNetCore | MIT |
| xUnit | Apache 2.0 |
| coverlet | MIT |

---

## 5. Workflow

Claude must follow this workflow for every piece of work, without exception.

### Step 1 — Understand before acting
Read `CLAUDE.md`, `README.md`, and any relevant source files before proposing changes.
Never assume the current state of the codebase — always check.

### Step 2 — Explain the plan
Before writing a single line of code, present a short plain-English plan:

```
Plan: [Feature name]
──────────────────────────────────
What I'm going to do:   [1–3 sentences]
Files I'll create:       [list]
Files I'll change:       [list]
New dependencies:        [list, with licenses] or "None"
Known trade-offs:        [any risks or limitations]
```

Wait for the Product Owner to say "go ahead" (or equivalent) before proceeding.

### Step 3 — Build and verify
After writing code, always run:
```bash
dotnet build
```
The build must pass with **0 errors and 0 warnings** before moving on.
If warnings appear, fix them — do not ship code with known warnings.

### Step 4 — Update documentation
After every completed feature:
- Update `README.md` if the feature changes how the app is used, configured, or deployed.
- Update `MEMORY.md` (in `.claude/projects/.../memory/`) if a new architectural decision was made.
- If a new dependency was added, add it to the license table in Section 4 of this file.

### Step 5 — Commit to GitHub
After documentation is updated, commit and push automatically:

```bash
git add <specific files — never git add -A>
git commit -m "<concise description of what changed and why>"
git push
```

Commit message format:
- First line: imperative mood, ≤ 72 characters (e.g. `Add UniverseViewModel flattened DTO`)
- Body (optional): explain the *why*, not the *what*
- Always end with: `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>`

**Never commit:**
- `tanaste_master.json` (contains local secrets/paths — gitignored)
- `*.db` files (local database)
- `bin/`, `obj/`, `.vs/` build artefacts

---

## 6. Project Contacts

| Role | Name |
|---|---|
| Product Owner | Shaya |
| Repository | [github.com/shyfaruqi/tanaste](https://github.com/shyfaruqi/tanaste) |
| License | AGPLv3 — see `LICENSE` file (to be added) |
