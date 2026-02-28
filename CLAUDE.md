# CLAUDE.md — Tanaste Project Memory

> **Who reads this file?**
> Every Claude session working on this repository reads this file automatically before doing anything else.
> It is the single, authoritative source of truth for what Tanaste is, how it is built, and how to work on it.
> It bridges the Product Owner's business goals with the technical team's execution.

---

## 1. Project Overview

### What is Tanaste?

**Tanaste** is a **unified media intelligence platform** that runs entirely on your own machine — no cloud account, no subscription, no data leaving your home.

Its core job is to bring order to a large, messy media collection spread across folders. You point it at your hard drive, and it automatically:

1. **Watches** your folders for new files — books, audiobooks, comics, TV episodes, and movies.
2. **Fingerprints** each file with a unique identifier (like a barcode), so it can track files even if you rename or move them.
3. **Reads the embedded information** inside each file — title, author, year, cover art, series name — and uses a *Weighted Voter* system to determine the most trustworthy version of each piece of information.
4. **Groups everything into Hubs** — a single, intelligent home for all versions of the same story.
5. **Serves a visual dashboard** in your browser for browsing, searching, and managing the library.
6. **Broadcasts instant updates** to your dashboard the moment a new file is detected, with no page refresh.

### The Hub Concept

The central idea in Tanaste is the **Hub**.

A Hub is the single digital home for a *story* — not just one file format, but every version of that story in your collection.

> *Example: "The Lord of the Rings" Hub might contain:*
> - *The eBook (EPUB)*
> - *The movie trilogy (MP4 videos)*
> - *The audiobook narration*
> - *The graphic novel adaptation (CBZ comic)*

All four live under one Hub. Tanaste links them together automatically by comparing their metadata. You browse by story, not by file type.

The Hub hierarchy works like this:

```
Universe  (your entire library — all Hubs)
  └── Hub  (one story — e.g. "Dune")
        └── Work  (one title in that story — e.g. "Dune Part One")
              └── Edition  (one physical version — e.g. "4K HDR Blu-ray Remux")
                    └── Media Asset  (one file on disk)
```

### Who is it for?

A single power user who wants complete, private control over a large media collection — without depending on services like Plex, Jellyfin, or any subscription platform.

---

## 2. Technical Stack

> **Note to Claude:** When speaking to the Product Owner, always use the plain-English column, not the technical column.
> The technical column is for implementation reference only.

### Core Tools

| Plain-English name | Technical name | Purpose |
|---|---|---|
| Programming language | C# / .NET 10 | Everything is written in this language |
| Local database | SQLite | A single file on disk that stores the entire library catalogue — no separate database server needed |
| Visual interface library | MudBlazor 9 | Pre-built visual building blocks (cards, grids, buttons) for the browser dashboard |
| Real-time intercom | SignalR | Pushes live updates (e.g. "new book added") to the dashboard without a page refresh |
| Book file reader | VersOne.Epub | Reads the information embedded inside EPUB book files |
| Engine API documentation | Swashbuckle | Auto-generates an interactive, explorable menu of all the Engine's capabilities at `/swagger` |
| Automated quality checks | xUnit + coverlet | Runs a set of automated tests after every change to catch mistakes early |
| Version control | Git + GitHub | Tracks every change made to the code, with full history |

### Headless Design — Engine and Dashboard are Separate

**This is a critical architectural decision.** The Tanaste *Engine* (the intelligence and data layer) is completely separate from the *Dashboard* (the visual interface).

**Why this matters to the business:**
- **Extensibility:** Other applications — like Radarr, Sonarr, or a custom mobile app — can connect directly to the Engine without needing the Dashboard. The Engine speaks a universal language (HTTP/JSON) that any app can understand.
- **Maintenance:** If the Dashboard needs to be redesigned, the Engine keeps running untouched. Each part can be updated independently.
- **Privacy:** The Engine can run as a silent background service with no interface at all, while the Dashboard is opened only when needed.

| Part | Technical project | Role |
|---|---|---|
| The Engine | `Tanaste.Api` | Handles all intelligence, data, and file operations. Exposes an API that any app can talk to. |
| The Dashboard | `Tanaste.Web` | The browser interface. Purely visual — it asks the Engine for data and displays it. |

### Source Code Layout (Plain English)

| Folder | What it is | Plain-English role |
|---|---|---|
| `src/Tanaste.Domain` | The Rulebook | Defines what a Hub, Work, and Edition *are*. No external tools — pure business logic. |
| `src/Tanaste.Storage` | The Filing Clerk | Reads and writes the SQLite database. Keeps all library data safe. |
| `src/Tanaste.Intelligence` | The Analyst | Runs the Weighted Voter system. Scores metadata Claims. Detects duplicates. |
| `src/Tanaste.Processors` | The Scanner | Opens each file type (EPUB, video, comic) and extracts its embedded information. |
| `src/Tanaste.Ingestion` | The Mail Room | Watches the Watch Folder. Queues new files. Manages the safe move of files into the organised library. |
| `src/Tanaste.Api` | The Reception Desk | The Engine's public interface. Exposes all features over HTTP. Hosts the real-time intercom. |
| `src/Tanaste.Web` | The Showroom | The browser Dashboard. Uses the Feature-Sliced layout (see Section 6). |
| `tests/` | The Quality Inspector | Automated checks for every module. |

### Dashboard Internal Layout (Feature-Sliced Design)

Inside `src/Tanaste.Web`, the code is organised by *what it does*, not by *what type of code it is*. This matches industry best practice for maintainable interfaces.

| Folder | Purpose |
|---|---|
| `Services/Integration/` | All communication with the Engine (HTTP calls, SignalR intercom) |
| `Services/Theming/` | Visual appearance — dark/light mode, colour palette, corner radii |
| `Components/Universe/` | Hub grid, hero card, media item cards |
| `Components/Bento/` | The asymmetric card-grid layout system |
| `Components/Navigation/` | Command palette, navigation menu |
| `Models/ViewDTOs/` | The data shapes used by the Dashboard (separate from the Engine's data shapes) |
| `Shared/` | Top-level layout, navigation shell, shared imports |

---

## 3. Technical Strategy & Key Features

### 3.1 — The Watch Folder (Ingestion Engine)

**Plain English:** You designate a folder on your computer as the "inbox". Anything you drop into that folder is automatically processed.

**What happens when a file lands in the inbox:**

1. **Settle** — The Engine waits briefly to make sure the file has finished copying (prevents reading half-written files).
2. **Lock check** — It confirms no other program is currently using the file.
3. **Fingerprint** — It computes a unique hash of the file's contents (like a barcode). This is the file's permanent identity — it survives renaming and moving.
4. **Scan** — The appropriate Scanner (book/video/comic) opens the file and reads all embedded metadata.
5. **Identify** — The Analyst runs the Weighted Voter to assign the file to a Hub (or create a new one).
6. **Move** — The file is moved from the inbox to a clean, organised folder structure in the library.

**Why this matters to the business:** Once configured, the library manages itself. Drop files in; Tanaste does the rest.

### 3.2 — The Weighted Voter (Intelligence Engine)

**Plain English:** Different information sources disagree about a file's title, author, or year. The Weighted Voter holds an election to determine the truth.

Each piece of metadata (e.g. the title "Dune") is a **Claim**. Claims come from multiple sources:

| Claim source | Example | Trust weight |
|---|---|---|
| File's internal metadata (OPF/ID3) | `title = "Dune"` | High (0.9) |
| Filename | `dune_part1.epub` | Medium (0.5) |
| External metadata provider | `Dune (Frank Herbert, 1965)` | Configurable |

The Voter tallies all Claims for each metadata field. The winning Claim becomes the **Canonical Value** — the single trusted answer used by the Dashboard.

If two Claims are too close to pick a clear winner, the field is flagged as **Conflicted** and surfaced to the user for manual resolution.

**Why this matters to the business:**
- **No human help needed** for well-tagged files — the library builds itself.
- **Transparent conflicts** — the user is only bothered when the machine genuinely cannot decide.
- **Provenance preserved** — every Claim is kept forever (append-only). History is never lost.

### 3.3 — Security: Secret Store & Guest Keys

**Plain English:** The Engine protects sensitive information and controls who can talk to it.

**Secret Store**
Private API keys (e.g. for external metadata providers like TMDB or MusicBrainz) are encrypted at rest using the operating system's built-in protection layer. They are never stored as plain text in the configuration file.

**Guest Key System**
Any application that wants to talk to the Engine must present a valid API key. Keys are:
- Generated inside the Engine
- Labelled (e.g. "Radarr integration", "Mobile app")
- Revocable individually without affecting other keys

**Why this matters to the business:**
- **Privacy:** No third-party app gets free access. Each connection is authorised and audited.
- **Maintenance:** A compromised key can be revoked in seconds. The rest of the system is unaffected.

### 3.4 — Dashboard UI Features

**Bento Grid**
The Hub overview uses an asymmetric card layout inspired by Japanese bento boxes — wider cards for recently accessed Hubs, narrower cards for the rest. This creates a visually rich layout that naturally draws attention to what matters most, without the user having to configure anything.

**Automotive Mode** *(planned)*
A high-contrast, large-button display mode designed for use at a distance (e.g. on a media room TV or tablet mounted in a vehicle). All text is enlarged, all touch targets are oversized, and non-essential interface elements are hidden. Activated by a single toggle.

**Real-time updates (Intercom)**
The Dashboard is connected to the Engine via a persistent live channel (the "Intercom"). When a new file is ingested or processing progresses, the Dashboard updates instantly — no manual refresh, no polling. The progress bar is live.

---

## 4. Product Owner Communication Rules

> Claude must apply these rules in every single message to the Product Owner. There are no exceptions.

### 4.1 — Mandatory vocabulary

Always use the plain-English term. Never use the technical term in conversation.

| ❌ Never say | ✅ Always say |
|---|---|
| Backend / Frontend | Engine / Dashboard |
| API / Endpoint | Engine connection point / Action |
| Database / Schema | Data store / Data structure |
| Deploy / Ship | Publish / Release |
| Refactor | Reorganise / Clean up |
| Repository / Commit | Code history / Save point |
| Dependency / Package | Tool / Library |
| DI container / Service registration | App's ingredient list |
| Null reference / Exception | Missing value / Unexpected error |
| Compile / Build | Assemble / Verify |
| Pull request / Branch | Proposed change / Parallel work stream |
| csproj / props file | Project configuration file |
| namespace / class | Code module / Blueprint |

### 4.2 — Always explain the "Why" in business terms

Every technical choice must be justified using one or more of these business goals:

| Goal | Meaning |
|---|---|
| **Maintenance** | Makes the product easier and cheaper to change in the future |
| **Extensibility** | Allows new features or external tools to be added without breaking existing ones |
| **Privacy** | Keeps user data on the user's machine; nothing leaves without explicit action |
| **Reliability** | Reduces the chance of errors or data loss |
| **Performance** | Makes the product faster or more responsive for the user |

*Example:* Instead of "We're separating the Engine and Dashboard for loose coupling," say "We're separating the Engine and the Dashboard so that future tools like a mobile app can connect to the Engine directly — **Extensibility**."

### 4.3 — Plan before coding

Before writing any code, Claude must present a plain-English plan using this exact format:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 PLAN: [Feature name]
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
 What I'm going to build:
   [1–3 plain-English sentences]

 Why this serves your goals:
   [Cite Maintenance / Extensibility / Privacy / Reliability / Performance]

 What I'll create:
   [plain list of new items]

 What I'll change:
   [plain list of modified items]

 New tools needed:
   [name + license] or "None"

 Trade-offs to know about:
   [any limitations or risks] or "None"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

**Do not proceed until the Product Owner says "go ahead" or equivalent.**

### 4.4 — Error reporting

When the build fails or a quality check breaks, Claude must:
1. Say **what went wrong** in plain English (no error codes as the primary explanation).
2. Say **what will be done** to fix it.
3. Fix it immediately — errors during an approved task do not need a second sign-off.

### 4.5 — Honest about uncertainty

Never guess silently. If Claude is unsure about an approach, it must say so:
> *"I'm not certain which approach is best here — I see two options. Here's the trade-off: [explain]. Which matters more to you?"*

---

## 5. Compliance & Workflow

### 5.1 — License: AGPLv3

> **⚠️ This project is licensed under the GNU Affero General Public License v3.0 (AGPLv3). This cannot be changed without the Product Owner's explicit decision.**

**What this means in practice:**
- AGPLv3 is a "share-alike" license. If Tanaste is ever run as a network service (even privately), any modifications must also be released under AGPLv3.
- **Every new tool added to the project must have a compatible license.** Claude must check the license before adding anything.

**Compatibility guide:**

| License | Compatible? | Notes |
|---|---|---|
| MIT | ✅ Safe | Most common open-source license |
| Apache 2.0 | ✅ Safe | Used by many Microsoft packages |
| BSD 2/3-clause | ✅ Safe | Permissive |
| LGPL v2.1 / v3 | ✅ Safe | Common for libraries |
| GPL v3 / AGPL v3 | ✅ Safe | Same family |
| GPL v2 (no "or later") | ⚠️ Check | May be incompatible — ask before adding |
| SSPL | ❌ Block | Not OSI-approved; incompatible |
| Commons Clause | ❌ Block | Restricts commercial use |
| Proprietary / commercial | ❌ Block | Incompatible by definition |

**If Claude is unsure about a tool's license**, it must stop, say so, and ask the Product Owner before proceeding.

**Current approved tools and their licenses:**

| Tool | License | Added in |
|---|---|---|
| Microsoft.Data.Sqlite | MIT | Phase 4 (Storage) |
| Microsoft.Extensions.* | MIT | Phase 4 |
| Microsoft.AspNetCore.* | MIT | Phase 4 |
| Microsoft.AspNetCore.SignalR.Client | MIT | Deliverable 2 (UI) |
| MudBlazor 9 | MIT | Deliverable 1 (UI) |
| VersOne.Epub | MIT | Phase 5 (Processors) |
| Swashbuckle.AspNetCore | MIT | Phase 4 (API) |
| xUnit 2 | Apache 2.0 | Phase 4 (Tests) |
| coverlet | MIT | Phase 4 (Tests) |

### 5.2 — Mandatory Workflow

Claude must follow every step of this workflow for every piece of work, without exception.

---

**Step 1 — Read before touching anything**

Read `CLAUDE.md` (this file), `README.md`, and every file relevant to the task before proposing changes. Never assume the current state of the code — always verify.

---

**Step 2 — Present the plan and wait for sign-off**

Use the plan format in Section 4.3. Do not write a single line of code until the Product Owner approves.

---

**Step 3 — Assemble and verify**

After writing code, run:
```bash
dotnet build
```
The result must be **0 errors, 0 warnings** before moving to the next step.
Warnings are not acceptable — they indicate future problems and must be fixed immediately.

---

**Step 4 — Update the project memory**

After every completed feature, update documentation:

| Document | Update when… |
|---|---|
| `README.md` (repo root) | The feature changes how the product is installed, configured, or used |
| `CLAUDE.md` §3 (Tool inventory) | A new tool or library was added |
| `CLAUDE.md` §5.1 (License table) | A new dependency was approved |
| `MEMORY.md` (`.claude/projects/.../memory/`) | A new architectural decision was made that future sessions need to know |

---

**Step 5 — Commit to GitHub automatically**

After documentation is updated:

```bash
# Stage only specific, relevant files — never use git add -A or git add .
git add <file1> <file2> ...

# Write a clear, useful commit message
git commit -m "Short imperative summary (≤72 chars)

Optional body: explain the WHY, not the WHAT.

Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"

git push
```

**Files that must never be committed:**

| File / Pattern | Reason |
|---|---|
| `tanaste_master.json` | Contains local paths and secrets — gitignored |
| `*.db` | Local database file — user data, not code |
| `bin/`, `obj/` | Assembled output — regenerated automatically |
| `.vs/`, `.idea/` | Editor preferences — personal, not shared |
| `appsettings.*.json` containing real keys | Contains secrets — only example files go in git |

---

## 6. Structure Reference — Feature-Sliced Dashboard Layout

All Dashboard code in `src/Tanaste.Web/` follows the **Feature-Sliced** pattern. Every new piece of UI code must go into the correct slice. Claude must respect this layout and never mix responsibilities between slices.

```
src/Tanaste.Web/
│
├── Services/
│   ├── Integration/          ← ALL communication with the Engine lives here
│   │   ├── ITanasteApiClient.cs        Contract for HTTP calls to the Engine
│   │   ├── TanasteApiClient.cs         Implementation: makes HTTP calls, maps raw JSON
│   │   ├── UniverseStateContainer.cs   Per-session cache: hubs, universe view, progress
│   │   ├── UIOrchestratorService.cs    Orchestrator: bridges HTTP + SignalR + state cache
│   │   ├── UniverseMapper.cs           Maps Engine data → flat Dashboard view model
│   │   └── IntercomEvents.cs           SignalR event shapes (MediaAdded, IngestionProgress)
│   │
│   └── Theming/              ← ALL visual configuration lives here
│       └── ThemeService.cs             Dark/light mode, colour palette, corner radii
│
├── Components/
│   ├── Universe/             ← Hub-related visual components
│   │   ├── HubHero.razor               "Last Journey" hero card (most recently active Hub)
│   │   └── UniverseStack.razor         Asymmetric bento grid of all Hub cards
│   │
│   ├── Bento/                ← The layout grid system (reusable)
│   │   ├── BentoGrid.razor             CSS grid container
│   │   └── BentoItem.razor             Single grid cell with configurable span
│   │
│   ├── Navigation/           ← Navigation and search components
│   │   └── CommandPalette.razor        Ctrl+K global search and navigation
│   │
│   └── Pages/                ← Full-page views (routed)
│       ├── Home.razor                  Library overview page
│       └── NotFound.razor              404 page
│
├── Models/
│   └── ViewDTOs/             ← Data shapes used ONLY by the Dashboard (never shared with Engine)
│       ├── HubViewModel.cs             Hub for display: DisplayName, WorkCount, MediaTypes
│       ├── WorkViewModel.cs            Work for display: Title, Author, Year helpers
│       ├── UniverseViewModel.cs        Flattened cross-media library view + DominantHexColor
│       ├── SystemStatusViewModel.cs    Engine health probe result
│       └── ScanResultViewModel.cs      Dry-run scan result (pending file operations)
│
└── Shared/                   ← Top-level layout shell (used by every page)
    ├── MainLayout.razor                App chrome: AppBar, Drawer, dark-mode toggle
    ├── NavMenu.razor                   Sidebar navigation links
    └── _Imports.razor                  Namespace imports for all Shared components
```

**Rules for adding new code to the Dashboard:**

| New code type | Where it goes |
|---|---|
| A new call to the Engine | `Services/Integration/TanasteApiClient.cs` + `ITanasteApiClient.cs` |
| A new data shape for the Dashboard | `Models/ViewDTOs/` |
| A new reusable visual component | `Components/<FeatureName>/` |
| A new full page | `Components/Pages/` |
| A new layout wrapper | `Shared/` |
| A new visual theme setting | `Services/Theming/ThemeService.cs` |

---

## 7. Project Contacts

| Role | Detail |
|---|---|
| Product Owner | Shaya |
| Repository | [github.com/shyfaruqi/tanaste](https://github.com/shyfaruqi/tanaste) |
| License | AGPLv3 |
| Engine base URL (local dev) | `http://localhost:61495` |
| Dashboard URL (local dev) | `http://localhost:5016` |
