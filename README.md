# WPF to Blazor Migration Tool — Backend API

Backend API for industrializing WPF to Blazor migrations using AI.
Built for the RSVZ context (Team Black, reusable building blocks).

## Tech Stack

- .NET 9 / ASP.NET Core Minimal APIs
- Supabase Postgres (Npgsql + Dapper)
- Supabase JWT authentication
- OpenAI / Azure OpenAI (configurable)
- Background processing via Channel + BackgroundService

## Setup

### 1. Clone and configure

```bash
cp .env.example .env
# Edit .env with your credentials
```

### 2. Create Supabase tables

Run the SQL migrations in your Supabase SQL Editor:
1. `src/Api/Migrations/001_create_tables.sql`
2. `src/Api/Migrations/002_rls_policies.sql`

### 3. Run locally

```bash
cd src/Api
dotnet run
```

Or with Docker:

```bash
docker compose up --build
```

The API will be available at `http://localhost:8080`.

### 4. Run tests

```bash
dotnet test
```

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `SUPABASE_URL` | Yes | Supabase project URL |
| `SUPABASE_ANON_KEY` | Yes | Supabase anonymous key |
| `SUPABASE_SERVICE_ROLE_KEY` | Yes | Supabase service role key (for storage) |
| `SUPABASE_JWT_SECRET` | Yes | JWT secret from Supabase Settings > API |
| `SUPABASE_DB_PASSWORD` | Yes* | Database password (*or use DATABASE_URL) |
| `DATABASE_URL` | No | Full Postgres connection string (overrides auto-build) |
| `AI_PROVIDER` | No | `openai` (default) or `azure` |
| `OPENAI_API_KEY` | Yes* | OpenAI API key (*if provider=openai) |
| `OPENAI_MODEL` | No | Model name (default: `gpt-4o`) |
| `AZURE_OPENAI_ENDPOINT` | Yes* | Azure endpoint (*if provider=azure) |
| `AZURE_OPENAI_KEY` | Yes* | Azure API key (*if provider=azure) |
| `AZURE_OPENAI_DEPLOYMENT` | Yes* | Azure deployment name (*if provider=azure) |
| `FRONTEND_ORIGIN` | No | CORS origin (default: `http://localhost:5173`) |
| `USE_SUPABASE_STORAGE` | No | `true`/`false` (default: `false`) |
| `SUPABASE_STORAGE_BUCKET` | No | Bucket name (default: `wpf-blazor-files`) |
| `MAX_FILE_BYTES` | No | Max file size in bytes (default: 1048576) |
| `MAX_FILES_PER_JOB` | No | Max files per job (default: 50) |
| `ENABLE_SWAGGER` | No | `true`/`false` (default: `false`, `true` in Development) |

## API Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| GET | `/health` | No | Health check |
| POST | `/api/jobs` | Yes | Create a migration job with WPF files |
| GET | `/api/jobs` | Yes | List your jobs |
| GET | `/api/jobs/{id}` | Yes | Get job details |
| POST | `/api/jobs/{id}/analyze` | Yes | Trigger AI analysis |
| POST | `/api/jobs/{id}/convert` | Yes | Trigger AI conversion |
| GET | `/api/jobs/{id}/outputs` | Yes | List converted output files |
| POST | `/api/jobs/{id}/playbook?lang=fr\|nl` | Yes | Generate migration playbook |
| POST | `/api/jobs/{id}/training?lang=fr\|nl` | Yes | Generate training kit |
| GET | `/api/jobs/{id}/download` | Yes | Download outputs as ZIP |
| GET | `/api/jobs/{id}/logs` | Yes | Get processing logs |

## Workflow

```
1. POST /api/jobs          → Create job with WPF source files
2. POST /api/jobs/{id}/analyze  → AI analyzes WPF patterns, complexity, controls
3. POST /api/jobs/{id}/convert  → AI converts WPF to Blazor components
4. GET  /api/jobs/{id}/outputs  → View converted files
5. POST /api/jobs/{id}/playbook?lang=fr → Generate migration playbook (FR/NL)
6. POST /api/jobs/{id}/training?lang=nl → Generate 90-min training kit (FR/NL)
7. GET  /api/jobs/{id}/download → Download everything as ZIP
```

## Example curl requests

### Create a job

```bash
curl -X POST http://localhost:8080/api/jobs \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MyWpfApp",
    "target": "BlazorServer",
    "files": [
      {
        "path": "MainWindow.xaml",
        "content": "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Grid><Button Content=\"Click me\"/></Grid></Window>"
      },
      {
        "path": "MainWindow.xaml.cs",
        "content": "using System.Windows;\nnamespace MyApp {\n  public partial class MainWindow : Window {\n    public MainWindow() { InitializeComponent(); }\n  }\n}"
      }
    ]
  }'
```

### Trigger analysis

```bash
curl -X POST http://localhost:8080/api/jobs/{JOB_ID}/analyze \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

### Trigger conversion

```bash
curl -X POST http://localhost:8080/api/jobs/{JOB_ID}/convert \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

### Generate playbook in French

```bash
curl -X POST "http://localhost:8080/api/jobs/{JOB_ID}/playbook?lang=fr" \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

### Generate training kit in Dutch

```bash
curl -X POST "http://localhost:8080/api/jobs/{JOB_ID}/training?lang=nl" \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

### Download outputs as ZIP

```bash
curl -O -J http://localhost:8080/api/jobs/{JOB_ID}/download \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

### Check logs

```bash
curl http://localhost:8080/api/jobs/{JOB_ID}/logs \
  -H "Authorization: Bearer YOUR_SUPABASE_JWT"
```

## Project Structure

```
WpfToBlazor/
├── src/Api/
│   ├── Program.cs              — Entry point, DI, middleware, routing
│   ├── Settings.cs             — Typed configuration classes
│   ├── Models/                 — Job, JobFile, JobLog entities
│   ├── Dtos/                   — Request/Response DTOs
│   ├── Auth/                   — Supabase JWT validation
│   ├── Middleware/              — Error handling (problem+json)
│   ├── Data/                   — Repository + Storage service
│   ├── Ai/                     — AI client abstraction + prompt templates
│   ├── Workers/                — Background job queue + worker
│   ├── Endpoints/              — Minimal API endpoint definitions
│   ├── Validation/             — File validation logic
│   └── Migrations/             — SQL DDL + RLS policies
├── tests/Api.Tests/            — Integration tests
├── docker-compose.yml
├── .env.example
└── README.md
```

## Limitations

- WPF to Blazor conversion is AI-assisted and **not perfect** — manual review and adjustments are always needed
- Complex custom controls, third-party libraries, and deep WPF-specific features (RoutedEvents, Adorners, complex triggers) require manual migration
- The AI output quality depends on the model used; GPT-4o is recommended
- File contents are stored as text in Postgres by default; enable Supabase Storage for large files
- Background processing is in-process (Channel); for production scale, consider a dedicated message queue
- The tool provides a starting point and industrialized approach, not a fully automated migration
