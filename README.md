# AgentFrameworkSolution

An image analysis web application built with **Clean Architecture**, **ASP.NET Core 10**, **Angular 21**, and **Ollama** (local LLM). Drop an image in the browser and receive an AI-generated summary, insights, and tags powered by the `gemma4:e4b` multimodal vision model.

---

## Architecture

```
src/
├── domain/           # Value objects, domain errors — no dependencies
├── application/      # CQRS handlers (MediatR), interfaces, DTOs
├── infrastructure/   # Ollama HTTP client, DI wiring
└── presentation/     # ASP.NET Core Web API + Angular 21 SPA
    └── ClientApp/    # Angular app (Tailwind CSS v4, standalone components)
```

---

## Prerequisites

| Tool | Version | Notes |
|------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0+ | `dotnet --version` |
| [Node.js](https://nodejs.org/) | 22+ LTS | `node --version` |
| [npm](https://www.npmjs.com/) | 11+ | Bundled with Node |
| [Angular CLI](https://angular.io/cli) | 21+ | `npm i -g @angular/cli` |
| [Ollama](https://ollama.com/) | Latest | Running locally |

### Pull the required Ollama model

```bash
ollama pull gemma4:e4b
```

Verify Ollama is running and the model is available:

```bash
ollama list
# should show gemma4:e4b
```

---

## Running in Development

Two terminals are required — one for the backend API, one for the Angular dev server.

### Quick start (PowerShell)

From the repository root, you can launch backend and frontend in separate PowerShell windows:

```powershell
.\start-dev.ps1
```

To also open the browser for the frontend dev server:

```powershell
.\start-dev.ps1 -OpenFrontend
```

### Terminal 1 — Backend (ASP.NET Core)

```bash
cd src/presentation
dotnet run
```

The API starts on `http://localhost:5193` (HTTP) and `https://localhost:7062` (HTTPS).  
Swagger UI is available at `http://localhost:5193/swagger`.

### Terminal 2 — Frontend (Angular dev server)

```bash
cd src/presentation/ClientApp
npm install        # only needed the first time
npx ng serve
```

The Angular dev server starts on `http://localhost:4200`.  
API calls to `/api/*` are proxied to `http://localhost:5193` via `proxy.conf.json`.

Open **`http://localhost:4200`** in your browser.

The model dropdown in the upload screen lists only **vision-capable** Ollama models discovered from your local Ollama instance.
The role dropdown is loaded from backend configuration and is required before running analysis.

---

## Building for Production

### 1. Build the Angular app

```bash
cd src/presentation/ClientApp
npx ng build --configuration production
```

Output is written to `src/presentation/ClientApp/dist/client-app/browser/`.

### 2. Build and publish the backend

```bash
cd src/presentation
dotnet publish -c Release -o ./publish
```

The published output includes the compiled API. In production mode, `Program.cs` serves static files from the app content root and falls back to `index.html` for client-side routing.

---

## Testing

Unit tests are implemented using **xUnit** and **Moq** for isolated component testing.

### Run all tests

```bash
dotnet test
```

### Run tests with verbose output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run tests for a specific project

```bash
dotnet test tests/AgentFrameworkSolution.Domain.Tests
```

```bash
dotnet test tests/AgentFrameworkSolution.Presentation.Tests
```

```bash
dotnet test tests/AgentFrameworkSolution.Infrastructure.Tests
```

```bash
dotnet test tests/AgentFrameworkSolution.Application.Tests
```

### Test Coverage

| Test Suite | Location | Tests | Coverage |
|-----------|----------|-------|----------|
| **Domain Tests** | `tests/AgentFrameworkSolution.Domain.Tests/` | 13 | SupportedLanguage enum helpers, ImageAnalysisResult defaults/value semantics, domain error construction |
| **Presentation Tests** | `tests/AgentFrameworkSolution.Presentation.Tests/` | 9 | Global exception handling middleware |
| **Infrastructure Tests** | `tests/AgentFrameworkSolution.Infrastructure.Tests/` | 9 | Ollama adapter behavior + DI wiring |
| **Application Tests** | `tests/AgentFrameworkSolution.Application.Tests/` | 6 | AnalyzeImage command handler validation + mapping |

#### Domain Tests (13 tests)

Verifies the current domain-layer behavior directly:

- ✅ `SupportedLanguage.GetLanguageName()` maps every supported enum value
- ✅ `SupportedLanguage.TryParse()` handles valid names case-insensitively
- ✅ Null, empty, and whitespace language inputs default to English
- ✅ Invalid language values fail parsing predictably
- ✅ `ImageAnalysisResult.Empty` returns an empty English result
- ✅ `ImageAnalysisResult` preserves constructor values and default language
- ✅ Domain errors expose consistent `Code`, `Message`, and base exception state

**Run domain tests:**

```bash
dotnet test tests/AgentFrameworkSolution.Domain.Tests --logger "console;verbosity=normal"
```

#### GlobalExceptionHandlingMiddlewareTests (9 tests)

Verifies centralized error handling, logging, and response sanitization:

- ✅ Domain errors return 400 Bad Request
- ✅ Application errors return 500 Internal Server Error
- ✅ Generic exceptions are sanitized (production) vs. detailed (development)
- ✅ Stack traces are logged internally but never exposed to clients
- ✅ Responses are valid JSON with consistent error format
- ✅ Logging occurs at appropriate levels (Warning/Error)

**Run presentation tests:**

```bash
dotnet test tests/AgentFrameworkSolution.Presentation.Tests --logger "console;verbosity=normal"
```

#### OllamaImageAnalyzerTests (9 tests)

Verifies infrastructure adapter behavior and registration:

- Configured/default model and temperature usage
- Model override behavior
- Error handling for non-success responses and empty payloads
- JSON parsing fallback behavior
- Vision model detection and sorting via `/api/tags` and `/api/show`
- DI registration (`IImageAnalyzer`) with configured/default base URL and timeout

**Run infrastructure tests:**

```bash
dotnet test tests/AgentFrameworkSolution.Infrastructure.Tests --logger "console;verbosity=normal"
```

#### AnalyzeImageHandlerTests (6 tests)

Verifies application-layer command handler behavior:

- Input validation (empty data, oversized payload, unsupported format)
- Language defaulting to English when omitted
- Dependency orchestration to `IImageAnalyzer`
- Failure behavior when analyzer returns empty summary
- DTO mapping output on successful analysis

**Run application tests:**

```bash
dotnet test tests/AgentFrameworkSolution.Application.Tests --logger "console;verbosity=normal"
```

---

## Configuration

Backend configuration lives in `src/presentation/appsettings.json`:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "Model": "gemma4:e4b",
    "Temperature": 0.2
  },
  "Analysis": {
    "Roles": [
      "Digital Forensic Analyst",
      "Computer Vision Specialist",
      "UX/UI Designer",
      "Radiologist / Medical Imaging Technician",
      "Art Critic or Curator"
    ]
  }
}
```

Override via environment variables (ASP.NET Core convention):

```bash
# Example: point to a remote Ollama instance
Ollama__BaseUrl=http://my-ollama-host:11434
Ollama__Model=gemma4:e4b
Ollama__Temperature=0.2

# Analysis roles (array index based)
Analysis__Roles__0=Digital Forensic Analyst
Analysis__Roles__1=Computer Vision Specialist
```

---

## API Reference

### `POST /api/imageanalysis`

Analyzes an uploaded image.

**Request** — `multipart/form-data`

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `file` | File | Yes | JPEG, PNG, WebP, or GIF. Max 10 MB. |
| `model` | string | No | Optional Ollama model override. |
| `language` | string | No | Defaults to `English` if omitted. |
| `role` | string | Yes | Must match one configured role from `Analysis:Roles`. |

**Response `200 OK`**

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fileName": "photo.jpg",
  "summary": "A scenic mountain landscape at sunset...",
  "insights": ["Strong warm color palette", "Golden hour lighting"],
  "tags": ["landscape", "mountains", "sunset", "nature"],
  "language": "English",
  "role": "Art Critic or Curator",
  "analyzedAt": "2026-04-20T12:00:00Z"
}
```

**Error responses**

| Status | Cause |
|--------|-------|
| `400` | Missing file, unsupported format, or file exceeds 10 MB |
| `500` | Ollama unreachable or model returned an unexpected response |

### `GET /api/imageanalysis/models`

Returns the list of installed **vision-capable** Ollama models used by the UI dropdown.

**Response `200 OK`**

```json
[
  "gemma4:e4b",
  "llava:latest"
]
```

If no vision-capable models are installed, the endpoint returns an empty array.

### `GET /api/imageanalysis/roles`

Returns the configured role list used by the role dropdown in the upload screen.

**Response `200 OK`**

```json
[
  "Digital Forensic Analyst",
  "Computer Vision Specialist",
  "UX/UI Designer",
  "Radiologist / Medical Imaging Technician",
  "Art Critic or Curator"
]
```

---

## Project Structure

```
AgentFrameworkSolution/
├── AgentFrameworkSolution.slnx          # Solution file (Visual Studio 2022+)
├── README.md
└── src/
    ├── domain/
    │   ├── Errors/                      # DomainError, InvalidImageError, …
    │   └── ValueObjects/                # ImageAnalysisResult (record)
    ├── application/
    │   ├── Commands/AnalyzeImage/       # AnalyzeImageCommand + Handler
    │   ├── DTOs/                        # ImageAnalysisDto
    │   ├── Errors/                      # ApplicationError, AnalysisFailedError
    │   └── Interfaces/                  # IImageAnalyzer
    ├── infrastructure/
    │   ├── Extensions/                  # ServiceCollectionExtensions
    │   └── Services/                    # OllamaImageAnalyzer
    └── presentation/
        ├── Controllers/                 # ImageAnalysisController
        ├── Program.cs
        ├── appsettings.json
        └── ClientApp/                   # Angular 21 SPA
            └── src/app/
                ├── components/
                │   ├── upload/          # Drag-drop image upload
                │   └── result-card/     # Analysis result display
                └── services/            # ImageAnalysisService
```

---

## Troubleshooting

**Ollama connection refused**  
Ensure `ollama serve` is running before starting the backend. The default URL is `http://localhost:11434`.

**Model not found**  
Run `ollama pull gemma4:e4b` and wait for the download to complete.

**No vision models available**  
The model dropdown only shows vision-capable models. Install one and restart the backend if needed:

```bash
ollama pull gemma4:e4b
ollama list
```

**Angular proxy not working**  
Make sure you start Angular with `npx ng serve` (not `npx ng build`). The proxy only applies to the dev server.

**`dotnet build` fails with `MSB1009`**  
Use the `.slnx` file: `dotnet build AgentFrameworkSolution.slnx`. The solution uses the newer `.slnx` format (Visual Studio 2022 17.12+).
