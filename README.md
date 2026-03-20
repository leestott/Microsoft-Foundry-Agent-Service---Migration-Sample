# Microsoft Foundry Agent Service — Migration Sample

Demonstrates migrating from the legacy `Azure.AI.OpenAI` **Assistants API** to the
**Microsoft Foundry Agent Service**, with concrete C#, Python, and JavaScript examples
for both the Entra-ID control plane and the API-key runtime plane.

Reference: [Migrate to Foundry Agents — learn.microsoft.com](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/migrate)

---

## The Split-Auth Model

Foundry uses a strict two-plane architecture. Understanding this split is the key to
migration:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    Microsoft Foundry Agent Service                        │
├───────────────────────────────┬──────────────────────────────────────────┤
│        CONTROL PLANE          │           RUNTIME PLANE                  │
│     (Agent Management)        │        (Model Inference)                 │
├───────────────────────────────┼──────────────────────────────────────────┤
│ • Create / update agents      │ • Chat completions                       │
│ • Thread management           │ • Streaming responses                    │
│ • Run orchestration           │ • Embeddings                             │
│ • Tool / file configuration   │ • Direct model invocation                │
│ • Agent versioning            │ • Responses API                          │
├───────────────────────────────┼──────────────────────────────────────────┤
│ Auth : Entra ID  ── ONLY ──   │ Auth : API Key  ✓  OR  Entra ID  ✓      │
│ Client: AIProjectClient       │ Client: ChatCompletionsClient            │
│         AgentsClient          │         AzureOpenAIClient                │
└───────────────────────────────┴──────────────────────────────────────────┘
```

> **Key insight:** `AIProjectClient` has **no `ApiKeyCredential` equivalent**.
> The control plane (agent create/manage/run) is Entra-ID–only by design.
> For API-key workloads, target the OpenAI-compatible **runtime inference endpoint** directly.

---

## Migration Map

| Legacy pattern | Foundry replacement | Auth |
|---|---|---|
| `AzureOpenAIClient(endpoint, apiKey)` + `GetAssistantClient()` | `AIProjectClient(endpoint, DefaultAzureCredential)` + `GetAgentsClient()` | **Entra ID** |
| `assistantClient.CreateAssistantAsync()` | `agentsClient.CreateAgentAsync()` | **Entra ID** |
| `assistantClient.CreateThreadAsync()` | `agentsClient.CreateThreadAsync()` | **Entra ID** |
| `assistantClient.CreateMessageAsync()` | `agentsClient.CreateMessageAsync()` | **Entra ID** |
| `assistantClient.CreateRunAsync()` + poll | `agentsClient.CreateRunAsync()` + poll | **Entra ID** |
| `AzureOpenAIClient(endpoint, apiKey)` → chat only | `ChatCompletionsClient(endpoint, AzureKeyCredential)` | **API Key ✓** |
| `AzureOpenAIClient(endpoint, apiKey)` → chat only | `AzureOpenAIClient(endpoint, ApiKeyCredential)` → `GetChatClient()` | **API Key ✓** |

---

## Recommended Auth Split

```
┌─────────────────────────────────────────────────────────────────────┐
│  Use case              │ Auth        │ Client                       │
├────────────────────────┼─────────────┼──────────────────────────────┤
│ Create / manage agents │ Entra ID    │ AIProjectClient + AgentsClient│
│ Run agent threads      │ Entra ID    │ AgentsClient                 │
│ Chat / Responses API   │ API Key ✓   │ ChatCompletionsClient        │
│ Chat / Responses API   │ API Key ✓   │ AzureOpenAIClient (v2)       │
│ Chat via project       │ Entra ID    │ projectClient.GetOpenAIClient│
└────────────────────────┴─────────────┴──────────────────────────────┘
```

This split is **recommended and supported** by Foundry going forward.

---

## Project Structure

```
apikey/
├── README.md
├── .env.example                   ← environment variable template
│
├── csharp/
│   ├── FoundryMigration.csproj
│   └── Program.cs                 ← all three C# patterns in one file
│
├── python/
│   ├── requirements.txt
│   ├── runtime_apikey.py          ← API-key inference (Options A & B)
│   └── control_entra.py           ← Entra ID agent management
│
└── javascript/
    ├── package.json
    ├── runtime_apikey.mjs         ← API-key inference (Options A & B)
    └── control_entra.mjs          ← Entra ID agent management
```

---

## Quick Start

### Prerequisites

1. Copy `.env.example` to `.env` and fill in your values.
2. For **control-plane demos**: authenticate with `az login` (or set service-principal env vars).
3. For **runtime demos**: only an API key is needed — no `az login` required.

### C#

```bash
cd csharp
dotnet run -- apikey-inference      # Azure AI Inference SDK  (API key)
dotnet run -- apikey-openai         # Azure.AI.OpenAI SDK     (API key)
dotnet run -- entra-agent           # Foundry AgentsClient    (Entra ID)
```

### Python

```bash
cd python
pip install -r requirements.txt

python runtime_apikey.py            # Option A: azure-ai-inference  (API key)
python runtime_apikey.py b          # Option B: openai SDK           (API key)
python control_entra.py             # Foundry agents                 (Entra ID)
```

### JavaScript

```bash
cd javascript
npm install

node runtime_apikey.mjs             # Option A: openai npm package   (API key)
node runtime_apikey.mjs b           # Option B: @azure/openai        (API key)
node control_entra.mjs              # Foundry agents                 (Entra ID)
```

---

## Packages

### C# (NuGet)

| Package | Plane | Notes |
|---|---|---|
| `Azure.AI.Projects` | Control | `AIProjectClient`, `AgentsClient` |
| `Azure.AI.Inference` | Runtime | `ChatCompletionsClient` — API key via `AzureKeyCredential` |
| `Azure.AI.OpenAI` | Runtime | `AzureOpenAIClient` — API key via `ApiKeyCredential` |
| `Azure.Identity` | Auth | `DefaultAzureCredential` for Entra ID |

### Python (pip)

| Package | Plane | Notes |
|---|---|---|
| `azure-ai-projects` | Control | `AIProjectClient`, `.agents` namespace |
| `azure-ai-inference` | Runtime | `ChatCompletionsClient` — API key via `AzureKeyCredential` |
| `openai` | Runtime | `AzureOpenAI` — API key via `api_key` param |
| `azure-identity` | Auth | `DefaultAzureCredential` |

### JavaScript (npm)

| Package | Plane | Notes |
|---|---|---|
| `@azure/ai-projects` | Control | `AIProjectClient`, `.agents` namespace |
| `openai` | Runtime | `AzureOpenAI` — API key via `apiKey` property |
| `@azure/openai` | Runtime | `AzureOpenAI` — API key via `AzureKeyCredential` |
| `@azure/identity` | Auth | `DefaultAzureCredential` |

---

## Environment Variables

| Variable | Plane | Required for |
|---|---|---|
| `PROJECT_ENDPOINT` | Control | All Entra / agent demos |
| `AZURE_AI_MODEL` | Control | Agent model name (e.g. `gpt-4o`) |
| `AZURE_AI_ENDPOINT` | Runtime A | Azure AI Foundry inference endpoint |
| `AZURE_AI_API_KEY` | Runtime A | API key for Foundry inference |
| `AZURE_OPENAI_ENDPOINT` | Runtime B | Azure OpenAI resource endpoint |
| `AZURE_OPENAI_API_KEY` | Runtime B | API key for Azure OpenAI |
| `AZURE_OPENAI_DEPLOYMENT` | Runtime B | Deployment / model name |

For Entra auth in CI/CD, set `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_CLIENT_SECRET`.
