"""
═══════════════════════════════════════════════════════════════════════════════
Microsoft Foundry Agent Service — Control Plane Demo (Python)

Demonstrates the full agent workflow using Entra ID authentication.

  Package : azure-ai-projects
  Client  : AIProjectClient  →  project_client.agents
  Auth    : DefaultAzureCredential  (Entra ID — REQUIRED for agent management)
            No API key alternative exists for the control plane.

Pre-requisites (one of):
  • az login                                  (developer workstation)
  • AZURE_CLIENT_ID + AZURE_TENANT_ID + AZURE_CLIENT_SECRET  (service principal)
  • Managed identity on an Azure host

Environment variables:
  PROJECT_ENDPOINT   https://<hub>.services.ai.azure.com
  AZURE_AI_MODEL     deployment / model name  (e.g. gpt-4o)

Run:
  python control_entra.py
═══════════════════════════════════════════════════════════════════════════════
"""

from __future__ import annotations

import os

from azure.ai.projects import AIProjectClient
from azure.ai.projects.models import MessageRole
from azure.identity import DefaultAzureCredential

# ─────────────────────────────────────────────────────────────────────────────
# Optional: load .env file for local development
# ─────────────────────────────────────────────────────────────────────────────
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass


def _require(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Environment variable '{name}' is not set. "
                           f"Copy .env.example to .env and fill in your values.")
    return value


def run() -> None:
    print("CONTROL PLANE — Foundry AgentsClient (Python)")
    print("Package : azure-ai-projects")
    print("Auth    : DefaultAzureCredential  (Entra ID — required for agents)")
    print("Client  : AIProjectClient → project_client.agents")
    print()

    endpoint = _require("PROJECT_ENDPOINT")
    model    = os.environ.get("AZURE_AI_MODEL", "gpt-4o")

    # ── Create project client (Entra ID) ──────────────────────────────────────
    #    AIProjectClient is the entry point for all Foundry control-plane
    #    operations. DefaultAzureCredential resolves credentials in order:
    #      1. Environment variables  (AZURE_CLIENT_ID + AZURE_TENANT_ID + ...)
    #      2. Workload / managed identity  (when hosted on Azure)
    #      3. az login  (developer workstation)
    #
    #    There is NO API key equivalent for AIProjectClient — the control plane
    #    (agent create / manage / run) is Entra-ID-only by Foundry design.
    project_client = AIProjectClient(
        endpoint=endpoint,
        credential=DefaultAzureCredential())

    # Shorthand to the agents operations namespace
    agents = project_client.agents

    # ── Step 1: Create an agent ────────────────────────────────────────────────
    #    Replaces: client.beta.assistants.create()  (legacy openai SDK)
    print("Creating agent...")
    agent = agents.create_agent(
        model=model,
        name="foundry-demo-agent",
        instructions="You are a concise assistant. Keep answers under 50 words.")
    print(f"  Agent  : {agent.id}  ({agent.name})")

    # ── Step 2: Create a conversation thread ───────────────────────────────────
    #    Replaces: client.beta.threads.create()
    thread = agents.create_thread()
    print(f"  Thread : {thread.id}")

    # ── Step 3: Add a user message ─────────────────────────────────────────────
    #    Replaces: client.beta.threads.messages.create()
    question = "Explain the Pythagorean theorem in one sentence."
    agents.create_message(
        thread_id=thread.id,
        role=MessageRole.USER,
        content=question)
    print(f"  Message: {question}")

    # ── Step 4: Create and wait for a run ─────────────────────────────────────
    #    create_and_process_run() is a convenience wrapper that creates the run
    #    and polls until it reaches a terminal state (completed / failed / etc.)
    #    Replaces: client.beta.threads.runs.create() + polling loop
    print("Running ", end="", flush=True)
    run = agents.create_and_process_run(
        thread_id=thread.id,
        agent_id=agent.id)
    print(f"  {run.status}")

    if run.status != "completed":
        print(f"Run did not complete: {run.last_error}")
        agents.delete_agent(agent.id)
        return

    # ── Step 5: Retrieve the agent's reply ────────────────────────────────────
    #    Messages are returned newest-first; the first agent/assistant message
    #    is the reply to the user's question.
    #    Replaces: client.beta.threads.messages.list()
    messages = agents.list_messages(thread_id=thread.id)
    for msg in messages.data:
        # Role may surface as "agent" or "assistant" depending on SDK version
        if msg.role in ("agent", "assistant"):
            for content_block in msg.content:
                if hasattr(content_block, "text"):
                    print(f"\nAgent reply: {content_block.text.value}")
            break

    # ── Cleanup ────────────────────────────────────────────────────────────────
    agents.delete_agent(agent.id)
    print("\nAgent deleted (cleanup complete).")


if __name__ == "__main__":
    run()
