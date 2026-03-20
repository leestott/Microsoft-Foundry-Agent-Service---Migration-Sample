/**
 * ═══════════════════════════════════════════════════════════════════════════════
 * Microsoft Foundry Agent Service — Control Plane Demo (JavaScript)
 *
 * Demonstrates the full agent workflow using Entra ID authentication.
 *
 *   Package : @azure/ai-projects
 *   Client  : AIProjectClient  →  projectClient.agents
 *   Auth    : DefaultAzureCredential  (Entra ID — REQUIRED for agent management)
 *             No API key alternative exists for the control plane.
 *
 * Pre-requisites (one of):
 *   • az login                                         (developer workstation)
 *   • AZURE_CLIENT_ID + AZURE_TENANT_ID + AZURE_CLIENT_SECRET  (service principal)
 *   • Managed identity on an Azure host
 *
 * Environment variables:
 *   PROJECT_ENDPOINT   https://<hub>.services.ai.azure.com
 *   AZURE_AI_MODEL     deployment / model name  (e.g. gpt-4o)
 *
 * Run:
 *   node control_entra.mjs
 *   npm run agent
 * ═══════════════════════════════════════════════════════════════════════════════
 */

import { AIProjectClient } from "@azure/ai-projects";
import { DefaultAzureCredential } from "@azure/identity";

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(
      `Environment variable '${name}' is not set. ` +
      `Copy .env.example to .env and fill in your values.`
    );
  }
  return value;
}

console.log("CONTROL PLANE — Foundry AgentsClient (JavaScript)");
console.log("Package : @azure/ai-projects");
console.log("Auth    : DefaultAzureCredential  (Entra ID — required for agents)");
console.log("Client  : AIProjectClient → projectClient.agents");
console.log();

const endpoint = requireEnv("PROJECT_ENDPOINT");
const model    = process.env.AZURE_AI_MODEL ?? "gpt-4o";

// ── Create project client (Entra ID) ─────────────────────────────────────────
//    AIProjectClient is the entry point for all Foundry control-plane
//    operations. DefaultAzureCredential resolves credentials in order:
//      1. Environment variables  (AZURE_CLIENT_ID + AZURE_TENANT_ID + ...)
//      2. Workload / managed identity  (when hosted on Azure)
//      3. az login  (developer workstation)
//
//    There is NO API key equivalent for AIProjectClient — the control plane
//    (agent create / manage / run) is Entra-ID-only by Foundry design.
const projectClient = new AIProjectClient(
  endpoint,
  new DefaultAzureCredential());

// Shorthand to the agents operations namespace
const agents = projectClient.agents;

// ── Step 1: Create an agent ───────────────────────────────────────────────────
//    Replaces: client.beta.assistants.create()  (legacy openai SDK)
console.log("Creating agent...");
const agent = await agents.createAgent(model, {
  name:         "foundry-demo-agent",
  instructions: "You are a concise assistant. Keep answers under 50 words.",
});
console.log(`  Agent  : ${agent.id}  (${agent.name})`);

// ── Step 2: Create a conversation thread ──────────────────────────────────────
//    Replaces: client.beta.threads.create()
const thread = await agents.createThread();
console.log(`  Thread : ${thread.id}`);

// ── Step 3: Add a user message ────────────────────────────────────────────────
//    Replaces: client.beta.threads.messages.create()
const question = "Explain the Pythagorean theorem in one sentence.";
await agents.createMessage(thread.id, {
  role:    "user",
  content: question,
});
console.log(`  Message: ${question}`);

// ── Step 4: Start a run and poll to completion ────────────────────────────────
//    Replaces: client.beta.threads.runs.create() + polling loop
console.log("Running ", "");
let run = await agents.createRun(thread.id, agent.id);

while (run.status === "queued" || run.status === "in_progress") {
  await new Promise(resolve => setTimeout(resolve, 1000));
  run = await agents.getRun(thread.id, run.id);
  process.stdout.write(".");
}
console.log(`  ${run.status}`);

if (run.status !== "completed") {
  console.error(`Run did not complete: ${JSON.stringify(run.lastError)}`);
  await agents.deleteAgent(agent.id);
  process.exit(1);
}

// ── Step 5: Retrieve the agent's reply ───────────────────────────────────────
//    Messages are returned newest-first; the first agent/assistant message
//    is the reply to the user's question.
//    Replaces: client.beta.threads.messages.list()
const messagesPage = await agents.listMessages(thread.id);

for (const msg of messagesPage.data) {
  // Role may surface as "agent" or "assistant" depending on SDK version
  if (msg.role === "agent" || msg.role === "assistant") {
    for (const block of msg.content) {
      if (block.type === "text") {
        console.log(`\nAgent reply: ${block.text.value}`);
      }
    }
    break;
  }
}

// ── Cleanup ───────────────────────────────────────────────────────────────────
await agents.deleteAgent(agent.id);
console.log("\nAgent deleted (cleanup complete).");
