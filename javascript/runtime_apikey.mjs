/**
 * ═══════════════════════════════════════════════════════════════════════════════
 * Microsoft Foundry Agent Service — Runtime Plane Demo (JavaScript)
 *
 * Demonstrates API-key-based model inference WITHOUT DefaultAzureCredential.
 *
 * Two options selectable at runtime:
 *
 *   Option A  openai npm package   →  Azure OpenAI / Foundry endpoint
 *             Client : AzureOpenAI  (openai)
 *             Auth   : apiKey property   (API key, no Entra ID required)
 *             Env    : AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY,
 *                      AZURE_OPENAI_DEPLOYMENT
 *
 *   Option B  @azure/openai package  →  Azure OpenAI endpoint
 *             Client : AzureOpenAI  (@azure/openai)
 *             Auth   : AzureKeyCredential  (API key, no Entra ID required)
 *             Env    : AZURE_OPENAI_ENDPOINT, AZURE_OPENAI_API_KEY,
 *                      AZURE_OPENAI_DEPLOYMENT
 *
 * Run:
 *   node runtime_apikey.mjs        ← Option A (default)
 *   node runtime_apikey.mjs b      ← Option B
 *   npm run apikey                 ← Option A via npm script
 *   npm run apikey:b               ← Option B via npm script
 * ═══════════════════════════════════════════════════════════════════════════════
 */

// ─────────────────────────────────────────────────────────────────────────────
// LEGACY REFERENCE — what the old code looked like (kept as a comment)
// ─────────────────────────────────────────────────────────────────────────────
//
// import OpenAI from "openai";
//
// const client = new OpenAI({                       // ← Assistants API (beta)
//   apiKey: process.env.OPENAI_API_KEY,
// });
//
// const assistant = await client.beta.assistants.create({
//   model        : "gpt-4o",
//   name         : "My Assistant",
//   instructions : "You are a helpful assistant.",
// });
// const thread = await client.beta.threads.create();
// await client.beta.threads.messages.create(thread.id, { role: "user", content: "..." });
// const run = await client.beta.threads.runs.create(thread.id, { assistant_id: assistant.id });
//
// The beta.assistants / beta.threads surface is replaced by Foundry Agents.
// See control_entra.mjs for the new control-plane pattern.
// ─────────────────────────────────────────────────────────────────────────────

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

const option = (process.argv[2] ?? "a").toLowerCase();

if (option === "b") {
  await runOptionB();
} else {
  await runOptionA();
}


// ═══════════════════════════════════════════════════════════════════════════════
// OPTION A — openai npm package with Azure OpenAI / Foundry endpoint
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Package : openai
//  Client  : AzureOpenAI
//  Auth    : apiKey property   ← API key, no Entra ID required
//  Endpoint: https://<resource>.openai.azure.com/  OR Foundry-compatible endpoint
//
//  This is the simplest API-key path — the same openai package used for
//  OpenAI.com also works against Azure OpenAI and Foundry endpoints.
//
//  Note: in the Foundry migration docs, "OpenAI client obtained from the project"
//  refers to projectClient.agents.getOpenAIClient() — which uses Entra ID.
//  Constructing AzureOpenAI directly with apiKey is the API-key equivalent
//  for pure inference, bypassing the project client entirely.
// ═══════════════════════════════════════════════════════════════════════════════
async function runOptionA() {
  const { AzureOpenAI } = await import("openai");

  console.log("OPTION A — openai npm package");
  console.log("Package : openai");
  console.log("Auth    : apiKey property  (API key — no Entra ID required)");
  console.log("Client  : AzureOpenAI");
  console.log();

  const endpoint   = requireEnv("AZURE_OPENAI_ENDPOINT");
  const apiKey     = requireEnv("AZURE_OPENAI_API_KEY");
  const deployment = process.env.AZURE_OPENAI_DEPLOYMENT ?? "gpt-4o";

  // ── Create client ───────────────────────────────────────────────────────────
  //    apiVersion selects the Azure OpenAI REST API surface.
  //    Use "2024-12-01-preview" or later for Responses API support.
  const client = new AzureOpenAI({
    endpoint,
    apiKey,
    apiVersion:  "2024-12-01-preview",
    deployment,                           // default deployment for all calls
  });

  // ── Send request ────────────────────────────────────────────────────────────
  const question = "Explain the Pythagorean theorem in one sentence.";
  console.log(`Request  : ${question}`);

  const response = await client.chat.completions.create({
    model: deployment,
    messages: [
      { role: "system", content: "You are a concise, helpful assistant." },
      { role: "user",   content: question },
    ],
    max_tokens: 128,
  });

  const choice = response.choices[0];
  console.log(`Model    : ${response.model}`);
  console.log(`Response : ${choice.message.content}`);
  console.log(`Tokens   : ${response.usage.total_tokens} total ` +
              `(prompt ${response.usage.prompt_tokens} / ` +
              `completion ${response.usage.completion_tokens})`);
}


// ═══════════════════════════════════════════════════════════════════════════════
// OPTION B — @azure/openai package with AzureKeyCredential
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Package : @azure/openai
//  Client  : AzureOpenAI
//  Auth    : AzureKeyCredential   ← API key, no Entra ID required
//  Endpoint: https://<resource>.openai.azure.com/
//
//  Use when you prefer the Azure SDK credential pattern (AzureKeyCredential)
//  instead of the plain apiKey string.  Interchangeable with Option A for
//  inference workloads.
// ═══════════════════════════════════════════════════════════════════════════════
async function runOptionB() {
  const { AzureOpenAI } = await import("@azure/openai");
  const { AzureKeyCredential } = await import("@azure/core-auth");

  console.log("OPTION B — @azure/openai package");
  console.log("Package : @azure/openai");
  console.log("Auth    : AzureKeyCredential  (API key — no Entra ID required)");
  console.log("Client  : AzureOpenAI (@azure/openai)");
  console.log();

  const endpoint   = requireEnv("AZURE_OPENAI_ENDPOINT");
  const apiKey     = requireEnv("AZURE_OPENAI_API_KEY");
  const deployment = process.env.AZURE_OPENAI_DEPLOYMENT ?? "gpt-4o";
  const apiVersion = "2024-12-01-preview";

  // ── Create client ───────────────────────────────────────────────────────────
  const client = new AzureOpenAI(
    endpoint,
    new AzureKeyCredential(apiKey),
    apiVersion);

  // ── Send request ────────────────────────────────────────────────────────────
  const question = "Explain the Pythagorean theorem in one sentence.";
  console.log(`Request  : ${question}`);

  const response = await client.chat.completions.create({
    model: deployment,
    messages: [
      { role: "system", content: "You are a concise, helpful assistant." },
      { role: "user",   content: question },
    ],
    max_tokens: 128,
  });

  const choice = response.choices[0];
  console.log(`Model    : ${response.model}`);
  console.log(`Response : ${choice.message.content}`);
  console.log(`Tokens   : ${response.usage.total_tokens} total ` +
              `(prompt ${response.usage.prompt_tokens} / ` +
              `completion ${response.usage.completion_tokens})`);
}
