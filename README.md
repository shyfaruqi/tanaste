# Tanaste

A local-first media library backend for EPUB books, videos, and comics.
Tanaste ingests files from a watched directory, extracts metadata, deduplicates
assets by content hash, and organises them into a Hub/Work/Edition hierarchy
stored in a local SQLite database.

## Architecture

```
Tanaste/
├── src/
│   ├── Tanaste.Domain/        Pure domain — zero external dependencies
│   ├── Tanaste.Storage/       SQLite persistence (ORM-less, raw SQL)
│   ├── Tanaste.Intelligence/  Metadata scoring, conflict resolution, Hub arbiter
│   ├── Tanaste.Processors/    File processors: EPUB, video, comic, generic
│   ├── Tanaste.Ingestion/     File-system watcher, debounce queue, background worker
│   └── Tanaste.Api/           ASP.NET Core minimal API + SignalR hub
└── tests/
    ├── Tanaste.Domain.Tests/
    ├── Tanaste.Intelligence.Tests/
    ├── Tanaste.Processors.Tests/
    ├── Tanaste.Ingestion.Tests/
    └── Tanaste.Storage.Tests/
```

**Dependency flow** (no upward references):

```
Tanaste.Domain
    └── Tanaste.Storage
            └── Tanaste.Intelligence
                    └── Tanaste.Processors
                            └── Tanaste.Ingestion
                                    └── Tanaste.Api
```

## Getting Started

**Prerequisites:** .NET 10 SDK

```bash
# Clone
git clone https://github.com/shyfaruqi/tanaste.git
cd tanaste

# Configure
cp tanaste_master.example.json tanaste_master.json
# Edit tanaste_master.json — set database_path and data_root for your environment

# Build
dotnet build

# Run the API
dotnet run --project src/Tanaste.Api

# Run all tests
dotnet test
```

## Configuration

Copy `tanaste_master.example.json` → `tanaste_master.json` (gitignored) and adjust:

| Field | Description |
|-------|-------------|
| `database_path` | Path to the SQLite database file |
| `data_root` | Root directory for media files |
| `scoring.auto_link_threshold` | Confidence threshold for automatic Hub linking (0–1, default 0.85) |
| `scoring.conflict_threshold` | Confidence threshold for flagging metadata conflicts (0–1, default 0.60) |
| `scoring.stale_claim_decay_days` | Days before a metadata claim is considered stale (default 90) |
| `maintenance.vacuum_on_startup` | Run SQLite `VACUUM` on startup (default false) |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10 / C# |
| Database | SQLite via `Microsoft.Data.Sqlite` (raw SQL, no ORM) |
| API | ASP.NET Core minimal APIs + SignalR |
| EPUB parsing | VersOne.Epub |
| OpenAPI | Swashbuckle.AspNetCore |
| Testing | xUnit 2, coverlet |

## Key Design Decisions

- **Hash dominance** — `content_hash` is `UNIQUE` on `media_assets`; duplicate files are detected and deduplicated without re-processing.
- **Append-only claims** — `metadata_claims` rows are never deleted, preserving full provenance history.
- **Hub deletion safety** — `works.hub_id` uses `ON DELETE SET NULL`; orphaned works are assigned to the System-Default Hub.
- **Embedded schema** — `schema.sql` is an embedded assembly resource so the binary self-initialises without external files.
- **WAL mode** — enabled programmatically on every SQLite connection open for better read concurrency.
