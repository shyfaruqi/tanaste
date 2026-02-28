<div align="center">

<img src="assets/images/tanaste-logo.svg" alt="Tanaste — The Unified Media Intelligence Kernel" height="90" />

**A cross-media Hub for your digital life.**

*Tanaste automatically unifies your Ebooks, Audiobooks, Comics, TV Shows, and Movies into single intelligent Hubs — powered by a local-first engine that never touches the cloud.*

<br/>

[![License: AGPLv3](https://img.shields.io/badge/License-AGPLv3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Linux%20%7C%20macOS%20%7C%20Windows-lightgrey.svg)]()
[![Arr Compatible](https://img.shields.io/badge/Arr--Compatible-Radarr%20%7C%20Sonarr-orange.svg)]()

</div>

---

## 📖 The Name Tanaste

The name **Tanaste** is drawn from Tolkien's Quenya — the High-Elvish language constructed for *The Lord of the Rings*. In Quenya, *tanaste* carries the meaning of **"Presentation"**: the act of bringing something forward, making it known, giving it form.

That single word is the entire design philosophy of this project.

Your media collection already exists. The stories are already there — scattered across folders, split into formats, buried under inconsistent filenames. Tanaste does not create your library. It **presents** it. It takes the raw, fragmented reality of files on a hard drive and surfaces it as something coherent, beautiful, and navigable.

> *"Tanaste: to present, to bring forth, to make appear as one."*

Every feature in this project is an expression of that word:

- The **Intelligence Engine** works silently in the background so that when you finally look at your library, it is already whole — presented, not assembled.
- The **Hub** is the act of presentation made structural: the book, the film, and the audiobook of the same story do not live in separate places. They are brought forward together, as one.
- The **Bento Dashboard** is where the internal act of presentation becomes visible — the translucent, glass-surfaced interface that is the final form of everything the Engine has already understood.

The name is a quiet promise: whatever you add to this system, Tanaste will find its place, understand its context, and present it back to you as if it always belonged there.

---

## 🧠 What is Tanaste?

You have a book. Then you find the movie adaptation. Then you grab the audiobook for the commute. Three files. Three folders. Three separate apps. Zero connection between them.

**Tanaste presents them as one.**

Drop your files into a Watch Folder, and Tanaste's Intelligence Engine automatically reads the metadata inside each file, scores it for reliability, and groups everything that belongs to the same story into a single **Hub**. The Hub for *Dune* becomes the single, unified presentation of that story in your collection — your EPUB, your 4K video, your audiobook, and your comic all brought forward together into one visual tile. You navigate by story, not by file type or folder.

The Bento Dashboard is where this act of presentation reaches the screen: a glassmorphic, asymmetric grid of Hub tiles that reflects what the Intelligence Engine already knows about your library. The interface is the presentation layer. Everything behind it is inference and order.

Everything runs on your own machine. No account. No subscription. No data sent anywhere.

---

## ✨ Key Features

### 📊 The Bento Dashboard
The Bento Dashboard is the physical **Presentation layer** — the visible surface of everything the Intelligence Engine has already silently understood about your library. It requires no manual curation: the layout reflects what is actually in your collection, and updates the moment something changes.

The grid uses an asymmetric **Bento layout** — wider tiles for your most recently visited Hubs, narrower tiles for the rest — so the shape of the interface naturally mirrors how you actually use your library. Each card uses **glassmorphic styling** (a translucent glass effect with soft depth and colour glows) drawn from the dominant colour of the Hub's media. A global **Command Palette** (activated with `Ctrl+K`) lets you navigate the entire library by name without touching the mouse.

> *Live updates are pushed directly to your browser via the Intercom channel the moment a new file is detected — no page refresh, no manual sync. The Dashboard is always a real-time reflection of what the Engine knows.*

### 🤖 The Intelligence Engine (Field-Specific Weighted Voter)
Tanaste never asks you to manually enter a title, year, or author. Instead, it uses a **Field-Specific Weighted Voter** system:

- Every piece of metadata from every source (embedded file tags, filenames, external providers) is recorded as a **Claim**
- Each Claim carries a **per-field trust weight** based on how reliable its source is *for that specific kind of data* — for example, Audnexus is authoritative for audiobook narrators (weight 0.9), while Open Library excels at series data (weight 0.9) but is not a dedicated cover-art source
- The Voter tallies all Claims for each metadata field independently and elects a winner — the **Canonical Value** — using only that field's weights
- If the vote is too close to call, the conflict is surfaced in the dashboard for a single human decision — the only time you ever need to intervene
- **User-Locked Claims** — when you manually set a metadata value, that claim is locked. The engine gives it a weight of 1.0 and cannot override it on any future re-score

Provider trust levels are **never hard-coded**. Every weight lives in `tanaste_master.json` so you can tune them at any time without touching code.

All original Claims are preserved forever. Nothing is overwritten. Full audit history, always.

### 🔒 Privacy-First by Design
- **Local SQLite database** — your entire library catalogue lives in a single file on your own hard drive. No cloud sync, no telemetry
- **Secret Store** — API keys for external metadata providers (e.g. TMDB, MusicBrainz) are encrypted at rest using your OS's built-in protection layer. Never stored as plain text
- **Guest Key system** — any external tool that connects to Tanaste must present a named, revocable API key. You control exactly who has access and can revoke a key in seconds without affecting others

### 🚗 Automotive Mode *(Planned)*
A dedicated high-contrast display mode with oversized buttons and enlarged text — designed for safe, glanceable use on a media room TV or a tablet mounted in a vehicle. One toggle switches the entire dashboard into this mode; one toggle switches it back.

---

## 📸 Screenshots

> *Bento Grid dashboard screenshots will be added here once the full UI is complete.*
>
> **Coming in a future update:**
> - Universe overview (Bento Grid with Hub cards)
> - Hub detail page with Works list
> - Ingestion progress live feed
> - Command Palette overlay

---

## 🚀 Quick Start

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
# 1. Clone the repository
git clone https://github.com/shyfaruqi/tanaste.git
cd tanaste

# 2. Create your local configuration file
cp tanaste_master.example.json tanaste_master.json
```

Open `tanaste_master.json` and set these two values:

```json
{
  "database_path": "/your/path/tanaste.db",
  "data_root":     "/your/media/library"
}
```

```bash
# 3. Start the Intelligence Engine (headless, API-only mode)
dotnet run --project src/Tanaste.Api

# Engine is now running at:
#   http://localhost:61495
#   Swagger UI: http://localhost:61495/swagger

# 4. (Optional) Start the visual Dashboard
dotnet run --project src/Tanaste.Web

# Dashboard is now running at:
#   http://localhost:5016

# 5. Run the automated test suite
dotnet test
```

### Configuration Reference

`tanaste_master.json` accepts the following settings:

| Setting | What it controls | Default |
|---|---|---|
| `database_path` | Where the library database file is stored | `tanaste.db` |
| `data_root` | Root directory for organised media files | *(required)* |
| `ingestion.watch_directory` | The inbox folder Tanaste monitors for new files | *(required)* |
| `scoring.auto_link_threshold` | Confidence required to auto-assign a file to a Hub (0–1) | `0.85` |
| `scoring.conflict_threshold` | Confidence below which a metadata field is flagged for review (0–1) | `0.60` |
| `scoring.stale_claim_decay_days` | Days before a Claim's trust weight decays | `90` |
| `maintenance.vacuum_on_startup` | Compact the database on startup to reclaim space | `false` |

---

## 🏗️ Architecture

Tanaste is built on a **headless Engine + visual Dashboard** split. The two parts are completely independent.

```
┌─────────────────────────────────────────┐
│             Tanaste.Web                  │  ← Visual Dashboard (Blazor Server)
│         browser dashboard               │    Connects via HTTP + SignalR
└────────────────┬────────────────────────┘
                 │  HTTP / SignalR
┌────────────────▼────────────────────────┐
│             Tanaste.Api                  │  ← Intelligence Engine (Headless API)
│    all logic, data, file operations     │    Runs independently; no UI required
└────────┬────────────────────────────────┘
         │
   ┌─────▼──────┐   ┌───────────────┐   ┌──────────────────┐
   │  Storage   │   │  Intelligence │   │    Ingestion     │
   │  (SQLite)  │   │ (Voter/Scorer)│   │  (Watch Folder)  │
   └────────────┘   └───────────────┘   └──────────────────┘
```

**Why the split matters:**
- The Engine can run silently as a background service — no browser, no interface, no overhead
- Any app that speaks HTTP can connect to the Engine directly — see [Arr Compatibility](#-arr-compatibility-radarrsonarr) below
- The Dashboard can be redesigned or replaced without touching the Engine or the database

**Internal Engine layers** (each depends only on the one above it):

```
Tanaste.Domain          ← Business rules and data shapes (zero dependencies)
  └─ Tanaste.Storage    ← Database reads and writes
      └─ Tanaste.Intelligence  ← Scoring, deduplication, conflict resolution
          └─ Tanaste.Processors  ← File-type readers (EPUB, video, comic)
              └─ Tanaste.Ingestion  ← Watch folder, file queue, background worker
                  └─ Tanaste.Api    ← HTTP endpoints and SignalR hub
```

---

## 🗂️ Filesystem-First Philosophy

**The database is a cache of the filesystem, not the other way around.**

Every file that Tanaste organises carries its own self-describing manifest — a `tanaste.xml` sidecar written directly alongside it on disk. If the database is ever wiped or migrated, the library can be fully reconstructed from those XML files alone. Nothing is ever lost that cannot be recovered from disk.

### Hub-First Folder Structure

When AutoOrganize is enabled and a file scores above the confidence threshold (≥ 0.85) or has a user-locked metadata value, Tanaste moves it into a clean, human-readable hierarchy:

```
{Library Root}/
  Books/
    The Hobbit (1937)/
      tanaste.xml                    ← Hub sidecar — human-readable identity + metadata
      Epub - Standard/
        The Hobbit.epub
        tanaste.xml                  ← Edition sidecar — content hash, title, author, locks
        cover.jpg                    ← Cover art (always on disk, never stored in DB)
```

The top-level category (`Books`, `Comics`, `Videos`, `Audio`) is derived from the file's detected media type. The Hub name is the scored title canonical value. The format folder identifies the media type and edition. The file is then placed inside, alongside its XML sidecar and cover image.

### The tanaste.xml Sidecar

Each sidecar is a small XML file with two schemas:

- **Hub-level** (`<tanaste-hub>`) — stores the Hub's display name, year, Wikidata identifier, and franchise. Written once per Hub folder; idempotent on repeat ingestion of the same Hub.
- **Edition-level** (`<tanaste-edition>`) — stores all metadata canonical values (title, author, media type, ISBN, ASIN), the content hash (permanent file identity), the cover-art path, and a list of any user-locked claims with their lock timestamps.

### The Great Inhale — Rebuilding from Disk

If the database is deleted or corrupted, call `POST /ingestion/library-scan` from the Engine API (or via the Dashboard). Tanaste will:

1. Recursively walk every folder under the Library Root looking for `tanaste.xml` files.
2. For each **Hub sidecar**: create or update the corresponding Hub record. XML always wins on conflict.
3. For each **Edition sidecar**: find the existing MediaAsset by its content hash, then restore all canonical values and any user-locked claims. Files not yet present in the database are skipped — a normal ingestion pass is needed to create them.

The Great Inhale is a **read-only XML scan** — it reads XML only, performs no file hashing and no metadata extraction, and completes in seconds even for large libraries.

> **The design constraint:** Cover art is never stored in the database. `cover.jpg` is always read from disk. The `tanaste.xml` sidecar is the single portable source of truth.

---

## 🌐 Supported Metadata Providers

Tanaste ships with three built-in zero-key providers — no API accounts, no rate-limit quotas, no cost. They activate automatically after you enable them in `tanaste_master.json`.

| Provider | Media type | What it contributes | Throttle |
|---|---|---|---|
| **Apple Books** (ebook) | EPUB / ebooks | Cover art (600 × 600), description, rating, title | 1 req / 300 ms |
| **Apple Books** (audiobook) | Audiobooks | Cover art (600 × 600), description, rating, title | shared |
| **Audnexus** | Audiobooks | Narrator, series, series position, cover art, author | none |
| **Wikidata** | All (people) | Person headshot (Wikimedia Commons), biography, Q-identifier | 1 req / 1.1 s |

All network calls run on a **background channel** (`Channel<HarvestRequest>`, bounded 500 items, DropOldest). File ingestion never blocks waiting for network.

**Recursive Person Enrichment** — each author and narrator found in a file's embedded tags gets a `Person` record linked to the asset. Unenriched persons are automatically queued for a Wikidata lookup. When the headshot and biography arrive, a `PersonEnriched` SignalR event pops the data into the Dashboard card in real time.

**To enable providers**, set `"enabled": true` for each entry in the `providers` array of `tanaste_master.json`, and add your local URL overrides to the `provider_endpoints` section if needed:

```json
"provider_endpoints": {
    "apple_books":     "https://itunes.apple.com",
    "audnexus":        "https://api.audnexus.com",
    "wikidata_api":    "https://www.wikidata.org/w/api.php",
    "wikidata_sparql": "https://query.wikidata.org/sparql"
}
```

All URLs live in `tanaste_master.json` — changing a provider's base address requires only a config edit, never a recompile.

---

## 🔌 Arr Compatibility (Radarr / Sonarr)

Tanaste's Engine exposes a standard HTTP API secured by an **`X-Api-Key` header** — the same authentication pattern used by Radarr, Sonarr, Lidarr, and the broader \*Arr ecosystem.

**To connect an external app:**

1. Open the Tanaste Swagger UI at `http://localhost:61495/swagger`
2. Use `POST /admin/api-keys` to create a named key for your app (e.g. `"Radarr integration"`)
3. Add the key as an `X-Api-Key` header in your external app's Tanaste connection settings
4. Revoke it any time with `DELETE /admin/api-keys/{id}` — other apps are unaffected

External apps can query Hubs, trigger library scans, and resolve metadata conflicts via the Engine's full REST API without ever opening the Dashboard.

---

## 🗺️ Project Roadmap

### ✅ Completed

| Phase | What was built |
|---|---|
| **Phase 1** | Macro-architecture, bounded contexts, and core design invariants |
| **Phase 2** | Domain model — Hub, Work, Edition, MediaAsset, and all contracts |
| **Phase 3** | Metadata provider contracts and claim structure |
| **Phase 4** | SQLite storage layer — ORM-less raw SQL, WAL mode, embedded schema |
| **Phase 5** | Media processors — EPUB, Video (stub), Comic (CBZ/CBR), Generic fallback |
| **Phase 6** | Intelligence Engine — Weighted Voter, Conflict Resolver, Identity Matcher, Hub Arbiter |
| **Phase 7** | Ingestion Engine — Watch Folder, debounce queue, content hasher, background worker |
| **Phase 8** | Field-Level Arbitration — User-Locked Claims, per-field provider trust matrix |
| **Phase 9** | External Metadata Adapters — Apple Books, Audnexus, Wikidata; Recursive Person Enrichment |
| **Library Organization & Sidecar System** | Hub-first folder structure (`{Category}/{Hub} ({Year})/{Format} - {Edition}`); tanaste.xml sidecars at Hub and Edition level; confidence gate on AutoOrganize (≥0.85 or user-locked); Great Inhale (`POST /ingestion/library-scan`) to rebuild DB from XML |
| **UI Deliverable 1** | Dashboard shell — MudBlazor layout, dark mode, Bento Grid, Hub cards, Command Palette |
| **UI Deliverable 2** | State & real-time — UniverseViewModel, UniverseMapper, SignalR Intercom listener |

### 🔄 In Progress / Planned

| Milestone | Description |
|---|---|
| **UI Deliverable 3** | Full Hub detail pages, Works list, Edition drill-down |
| **UI Deliverable 4** | Live ingestion progress feed using Intercom SignalR events |
| **UI Deliverable 5** | Metadata conflict resolution UI — review and resolve flagged Claims |
| **Automotive Mode** | High-contrast, large-button display mode for TV / in-vehicle use |
| **Video metadata** | Replace stub video extractor with FFmpeg-based real extractor |
| **Open Library provider** | ISBN-based book metadata (foundation already in place; one new adapter class) |
| **TMDB provider** | Movies and TV series metadata |
| **Mobile companion** | Read-only library browser via the existing Engine API |

---

## 🛠️ Tech Stack (Full Reference)

| What it does | Technology |
|---|---|
| Language & runtime | C# / .NET 10 |
| Database | SQLite via `Microsoft.Data.Sqlite` — raw SQL, no ORM |
| Engine API | ASP.NET Core minimal APIs |
| Real-time events | SignalR (`/hubs/intercom`) |
| Dashboard | Blazor Server |
| UI components | MudBlazor 9 |
| SignalR client | `Microsoft.AspNetCore.SignalR.Client` |
| EPUB parsing | VersOne.Epub |
| HTTP client lifecycle | `Microsoft.Extensions.Http` (IHttpClientFactory, named clients) |
| API docs | Swashbuckle (`/swagger`) |
| Tests | xUnit 2, coverlet |

---

## 📄 License

Tanaste is free and open-source software, licensed under the **GNU Affero General Public License v3.0 (AGPLv3)**.

> This means you are free to use, modify, and distribute Tanaste — but if you deploy a modified version as a network service, you must also make your modifications available under the same license.

See the [`LICENSE`](LICENSE) file for the full license text.

All dependencies are MIT or Apache 2.0 licensed and are compatible with AGPLv3.

---

<div align="center">

*Built with care for people who take their media library seriously.*

[Report a Bug](https://github.com/shyfaruqi/tanaste/issues) · [Request a Feature](https://github.com/shyfaruqi/tanaste/issues) · [View the Engine API](http://localhost:61495/swagger)

</div>
