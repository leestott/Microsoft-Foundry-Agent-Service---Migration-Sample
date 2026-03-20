"""
═══════════════════════════════════════════════════════════════════════════════
Microsoft Foundry Agent Service — Runtime Plane Demo (Python)

Demonstrates API-key-based model inference WITHOUT DefaultAzureCredential.

Two options selectable at runtime:

  Option A  azure-ai-inference SDK  →  Azure AI Foundry inference endpoint
            Client : ChatCompletionsClient
            Auth   : AzureKeyCredential (API key)
            Env    : AZURE_AI_ENDPOINT, AZURE_AI_API_KEY, AZURE_AI_MODEL

  Option B  openai SDK              →  Azure OpenAI resource endpoint
            Client : AzureOpenAI
            Auth   : api_key parameter (API key)
            Env    : AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY,
                     AZURE_OPENAI_DEPLOYMENT

Run:
  python runtime_apikey.py          ← Option A (default)
  python runtime_apikey.py b        ← Option B
═══════════════════════════════════════════════════════════════════════════════
"""

from __future__ import annotations

import os
import sys

# ─────────────────────────────────────────────────────────────────────────────
# Optional: load .env file for local development
# ─────────────────────────────────────────────────────────────────────────────
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass  # python-dotenv is optional


# ─────────────────────────────────────────────────────────────────────────────
# LEGACY REFERENCE — what the old code looked like (kept as a comment)
# ─────────────────────────────────────────────────────────────────────────────
#
# from openai import AzureOpenAI
#
# client = AzureOpenAI(
#     azure_endpoint = os.environ["AZURE_OPENAI_ENDPOINT"],
#     api_key        = os.environ["AZURE_OPENAI_API_KEY"],
#     api_version    = "2024-02-15-preview")        # ← Assistants preview
#
# assistant = client.beta.assistants.create(
#     model        = "gpt-4o",
#     name         = "My Assistant",
#     instructions = "You are a helpful assistant.")
#
# thread = client.beta.threads.create()
# client.beta.threads.messages.create(thread.id, role="user", content="...")
# run    = client.beta.threads.runs.create(thread.id, assistant_id=assistant.id)
#
# The `beta.assistants` / `beta.threads` surface is replaced by Foundry Agents.
# See control_entra.py for the new control-plane pattern.
# ─────────────────────────────────────────────────────────────────────────────


def _require(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Environment variable '{name}' is not set. "
                           f"Copy .env.example to .env and fill in your values.")
    return value


# ═══════════════════════════════════════════════════════════════════════════════
# OPTION A — Azure AI Inference SDK
# ═══════════════════════════════════════════════════════════════════════════════
#
#  Package : azure-ai-inference
#  Client  : ChatCompletionsClient
#  Auth    : AzureKeyCredential   ← API key, no Entra ID required
#  Endpoint: https://<hub>.services.ai.azure.com/models
#
#  This is the preferred API-key path for Foundry runtime inference.
#  The Azure AI Foundry inference endpoint is model-agnostic — it serves all
#  models deployed to the hub via a single OpenAI-compatible REST surface.
# ═══════════════════════════════════════════════════════════════════════════════
def run_option_a() -> None:
    from azure.ai.inference import ChatCompletionsClient
    from azure.ai.inference.models import SystemMessage, UserMessage
    from azure.core.credentials import AzureKeyCredential

    print("OPTION A — azure-ai-inference SDK")
    print("Package : azure-ai-inference")
    print("Auth    : AzureKeyCredential  (API key — no Entra ID required)")
    print("Client  : ChatCompletionsClient")
    print()

    endpoint = _require("AZURE_AI_ENDPOINT")
    api_key  = _require("AZURE_AI_API_KEY")
    model    = os.environ.get("AZURE_AI_MODEL", "gpt-4o")

    # ── Create client ─────────────────────────────────────────────────────────
    #    Targets https://<hub>.services.ai.azure.com/models
    #    AzureKeyCredential sends the key in the api-key header — no Entra flow.
    client = ChatCompletionsClient(
        endpoint=endpoint,
        credential=AzureKeyCredential(api_key))

    # ── Send request ──────────────────────────────────────────────────────────
    question = "Explain the Pythagorean theorem in one sentence."
    print(f"Request  : {question}")

    response = client.complete(
        model=model,
        messages=[
            SystemMessage("You are a concise, helpful assistant."),
            UserMessage(question),
        ],
        max_tokens=128)

    choice = response.choices[0]
    print(f"Model    : {response.model}")
    print(f"Response : {choice.message.content}")
    print(f"Tokens   : {response.usage.total_tokens} total "
          f"(prompt {response.usage.prompt_tokens} / "
          f"completion {response.usage.completion_tokens})")


# ═══════════════════════════════════════════════════════════════════════════════
# OPTION B — openai SDK with Azure OpenAI endpoint
# ═══════════════════════════════════════════════════════════════════════════════
#
#  Package : openai
#  Client  : AzureOpenAI
#  Auth    : api_key parameter   ← API key, no Entra ID required
#  Endpoint: https://<resource>.openai.azure.com/
#
#  Use this when targeting an Azure OpenAI resource directly, or when you
#  prefer the openai SDK surface (same as the OpenAI Python library).
#
#  Note: in the Foundry migration docs, "OpenAI client obtained from the project"
#  refers to project_client.get_azure_openai_client() — which uses Entra ID.
#  Constructing AzureOpenAI with api_key is the API-key equivalent for pure
#  inference, without going through the project client at all.
# ═══════════════════════════════════════════════════════════════════════════════
def run_option_b() -> None:
    from openai import AzureOpenAI

    print("OPTION B — openai SDK")
    print("Package : openai")
    print("Auth    : api_key parameter  (API key — no Entra ID required)")
    print("Client  : AzureOpenAI")
    print()

    endpoint   = _require("AZURE_OPENAI_ENDPOINT")
    api_key    = _require("AZURE_OPENAI_API_KEY")
    deployment = os.environ.get("AZURE_OPENAI_DEPLOYMENT", "gpt-4o")

    # ── Create client ─────────────────────────────────────────────────────────
    #    api_version selects the Azure OpenAI REST API surface.
    #    Use "2024-12-01-preview" or later for Responses API support.
    client = AzureOpenAI(
        azure_endpoint=endpoint,
        api_key=api_key,
        api_version="2024-12-01-preview")

    # ── Send request ──────────────────────────────────────────────────────────
    question = "Explain the Pythagorean theorem in one sentence."
    print(f"Request  : {question}")

    response = client.chat.completions.create(
        model=deployment,
        messages=[
            {"role": "system", "content": "You are a concise, helpful assistant."},
            {"role": "user",   "content": question},
        ],
        max_tokens=128)

    choice = response.choices[0]
    print(f"Model    : {response.model}")
    print(f"Response : {choice.message.content}")
    print(f"Tokens   : {response.usage.total_tokens} total "
          f"(prompt {response.usage.prompt_tokens} / "
          f"completion {response.usage.completion_tokens})")


# ─────────────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    option = sys.argv[1].lower() if len(sys.argv) > 1 else "a"
    if option == "b":
        run_option_b()
    else:
        run_option_a()
