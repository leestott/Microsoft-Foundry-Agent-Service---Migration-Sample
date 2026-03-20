// ═══════════════════════════════════════════════════════════════════════════════
// Microsoft Foundry Agent Service — Migration Sample (C#)
//
// Demonstrates the supported split-auth model:
//
//   CONTROL PLANE  → Entra ID (DefaultAzureCredential)
//                    Client: AIProjectClient + AgentsClient    [Azure.AI.Projects]
//                    Replaces: AzureOpenAIClient + AssistantClient (legacy)
//
//   RUNTIME PLANE  → API Key (no Entra required)
//                    Client A: ChatCompletionsClient            [Azure.AI.Inference]
//                    Client B: AzureOpenAIClient → ChatClient  [Azure.AI.OpenAI]
//
// Usage:
//   dotnet run -- apikey-inference    ← Pattern A: API key, Azure AI Inference SDK
//   dotnet run -- apikey-openai       ← Pattern B: API key, Azure.AI.OpenAI SDK
//   dotnet run -- entra-agent         ← Pattern C: Entra ID, full agent workflow
// ═══════════════════════════════════════════════════════════════════════════════

using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Chat;

var demo = args.FirstOrDefault() ?? "apikey-inference";

Console.WriteLine($"Running demo: {demo}");
Console.WriteLine(new string('─', 60));
Console.WriteLine();

await (demo switch
{
    "entra-agent"   => EntraAgentDemo.RunAsync(),
    "apikey-openai" => ApiKeyOpenAIDemo.RunAsync(),
    _               => ApiKeyInferenceDemo.RunAsync(),
});


// ─────────────────────────────────────────────────────────────────────────────
// LEGACY REFERENCE — what your old code looked like (kept as a comment)
// ─────────────────────────────────────────────────────────────────────────────
// The following is the OLD pattern using Azure.AI.OpenAI v1.x Assistants API.
// AssistantClient was removed in Azure.AI.OpenAI v2 and has NO direct
// replacement in that package — use AgentsClient from Azure.AI.Projects instead.
//
//  var azureClient = new Azure.AI.OpenAI.AzureOpenAIClient(
//      new Uri(endpoint),
//      new ApiKeyCredential(apiKey));
//
//  var assistantClient = azureClient.GetAssistantClient();   // ← removed in v2
//
//  var assistant = await assistantClient.CreateAssistantAsync("gpt-4o", new()
//  {
//      Name         = "My Assistant",
//      Instructions = "You are a helpful assistant.",
//  });
//  var thread = await assistantClient.CreateThreadAsync();
//  await assistantClient.CreateMessageAsync(thread.Value.Id, MessageRole.User, "Hello");
//  var run    = await assistantClient.CreateRunAsync(thread.Value.Id, assistant.Value.Id);
//  // ... poll run.Status, fetch messages
// ─────────────────────────────────────────────────────────────────────────────


// ═══════════════════════════════════════════════════════════════════════════════
// PATTERN A — API Key via Azure AI Inference SDK
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Use when: you need API-key auth against the Azure AI Foundry inference endpoint.
//  Package : Azure.AI.Inference
//  Auth    : AzureKeyCredential  (no Entra ID, no az login)
//  Endpoint: https://<hub>.services.ai.azure.com/models
//
//  This is the preferred API-key path for Foundry runtime inference.
//  The endpoint exposes an OpenAI-compatible surface for chat, completions,
//  and embeddings — across all models deployed to the Foundry hub.
//
//  Environment variables:
//    AZURE_AI_ENDPOINT   e.g. https://<hub>.services.ai.azure.com/models
//    AZURE_AI_API_KEY    your hub API key
//    AZURE_AI_MODEL      deployment / model name (e.g. gpt-4o)
// ═══════════════════════════════════════════════════════════════════════════════
static class ApiKeyInferenceDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("PATTERN A — API Key · Azure AI Inference SDK");
        Console.WriteLine("Package : Azure.AI.Inference");
        Console.WriteLine("Auth    : AzureKeyCredential  (no Entra ID required)");
        Console.WriteLine("Client  : ChatCompletionsClient");
        Console.WriteLine();

        var endpoint = new Uri(
            Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")
            ?? throw new InvalidOperationException("Set AZURE_AI_ENDPOINT"));

        var credential = new AzureKeyCredential(
            Environment.GetEnvironmentVariable("AZURE_AI_API_KEY")
            ?? throw new InvalidOperationException("Set AZURE_AI_API_KEY"));

        var model = Environment.GetEnvironmentVariable("AZURE_AI_MODEL") ?? "gpt-4o";

        // ── Create client ─────────────────────────────────────────────────────
        //    ChatCompletionsClient targets the Azure AI Foundry unified inference
        //    endpoint and accepts AzureKeyCredential — no Entra token needed.
        var client = new ChatCompletionsClient(endpoint, credential);

        // ── Build request ─────────────────────────────────────────────────────
        var options = new ChatCompletionsOptions
        {
            Model = model,
            Messages =
            {
                new ChatRequestSystemMessage("You are a concise, helpful assistant."),
                new ChatRequestUserMessage("Explain the Pythagorean theorem in one sentence."),
            },
            MaxTokens = 128,
        };

        Console.WriteLine("Request  : Explain the Pythagorean theorem in one sentence.");
        Response<ChatCompletions> response = await client.CompleteAsync(options);
        ChatCompletions completions = response.Value;

        Console.WriteLine($"Model    : {completions.Model}");
        Console.WriteLine($"Response : {completions.Choices[0].Message.Content}");
        Console.WriteLine($"Tokens   : {completions.Usage.TotalTokenCount} total " +
                          $"(prompt {completions.Usage.PromptTokenCount} / " +
                          $"completion {completions.Usage.CompletionTokenCount})");
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
// PATTERN B — API Key via Azure.AI.OpenAI SDK (OpenAI surface)
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Use when: you need API-key auth against an Azure OpenAI resource endpoint,
//            or when you prefer the OpenAI SDK surface (ChatClient, etc.).
//  Package : Azure.AI.OpenAI  (v2+)
//  Auth    : ApiKeyCredential  (no Entra ID, no az login)
//  Endpoint: https://<resource>.openai.azure.com/
//
//  Note: in the Foundry migration docs, "OpenAI client obtained from the project"
//  refers to projectClient.GetAzureOpenAIClient() — which uses Entra ID.
//  Creating AzureOpenAIClient directly with ApiKeyCredential is the 1:1
//  API-key equivalent of that pattern for pure inference workloads.
//
//  Environment variables:
//    AZURE_OPENAI_ENDPOINT    e.g. https://<resource>.openai.azure.com/
//    AZURE_OPENAI_API_KEY     your Azure OpenAI key
//    AZURE_OPENAI_DEPLOYMENT  deployment name (e.g. gpt-4o)
// ═══════════════════════════════════════════════════════════════════════════════
static class ApiKeyOpenAIDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("PATTERN B — API Key · Azure.AI.OpenAI SDK");
        Console.WriteLine("Package : Azure.AI.OpenAI  (v2)");
        Console.WriteLine("Auth    : ApiKeyCredential  (no Entra ID required)");
        Console.WriteLine("Client  : AzureOpenAIClient → ChatClient");
        Console.WriteLine();

        var endpoint = new Uri(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT"));

        var apiKey = new ApiKeyCredential(
            Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? throw new InvalidOperationException("Set AZURE_OPENAI_API_KEY"));

        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";

        // ── Create client ─────────────────────────────────────────────────────
        //    AzureOpenAIClient with ApiKeyCredential is unchanged from v1.
        //    In v2 the AssistantClient sub-client was removed; all other
        //    sub-clients (ChatClient, EmbeddingClient, etc.) remain.
        var azureClient = new AzureOpenAIClient(endpoint, apiKey);
        ChatClient chatClient = azureClient.GetChatClient(deployment);

        // ── Send request ──────────────────────────────────────────────────────
        Console.WriteLine("Request  : Explain the Pythagorean theorem in one sentence.");
        ChatCompletion result = await chatClient.CompleteChatAsync(
        [
            new SystemChatMessage("You are a concise, helpful assistant."),
            new UserChatMessage("Explain the Pythagorean theorem in one sentence."),
        ]);

        Console.WriteLine($"Response : {result.Content[0].Text}");
        Console.WriteLine($"Tokens   : {result.Usage.TotalTokenCount} total " +
                          $"(prompt {result.Usage.InputTokenCount} / " +
                          $"completion {result.Usage.OutputTokenCount})");
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
// PATTERN C — Entra ID · Full Agent Workflow (control plane)
// ═══════════════════════════════════════════════════════════════════════════════
//
//  Use when: creating, running, or managing agents and threads.
//  Package : Azure.AI.Projects
//  Auth    : DefaultAzureCredential  (Entra ID — REQUIRED, no API key alternative)
//  Endpoint: PROJECT_ENDPOINT  (https://<hub>.services.ai.azure.com)
//
//  This is the direct replacement for the legacy AssistantClient workflow.
//  The control plane (agent + thread + run management) is Entra-ID-only by
//  Foundry design — there is no ApiKeyCredential path at this layer.
//
//  Pre-requisite: az login  (local dev)  OR set AZURE_CLIENT_ID / AZURE_TENANT_ID
//                / AZURE_CLIENT_SECRET   (CI/CD service principal)
//
//  Environment variables:
//    PROJECT_ENDPOINT   e.g. https://<hub>.services.ai.azure.com
//    AZURE_AI_MODEL     model / deployment name (e.g. gpt-4o)
// ═══════════════════════════════════════════════════════════════════════════════
static class EntraAgentDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("PATTERN C — Entra ID · Foundry AgentsClient");
        Console.WriteLine("Package : Azure.AI.Projects");
        Console.WriteLine("Auth    : DefaultAzureCredential  (Entra ID — required for agents)");
        Console.WriteLine("Client  : AIProjectClient → AgentsClient");
        Console.WriteLine();

        var endpoint = new Uri(
            Environment.GetEnvironmentVariable("PROJECT_ENDPOINT")
            ?? throw new InvalidOperationException("Set PROJECT_ENDPOINT"));

        var model = Environment.GetEnvironmentVariable("AZURE_AI_MODEL") ?? "gpt-4o";

        // ── Create project client (Entra ID) ──────────────────────────────────
        //    AIProjectClient is the entry point for all Foundry control-plane
        //    operations. DefaultAzureCredential resolves credentials in order:
        //    1. Environment variables (service principal)
        //    2. Workload identity / managed identity
        //    3. az login (developer workstation)
        AIProjectClient projectClient = new(endpoint, new DefaultAzureCredential());

        // ── Get agents client ──────────────────────────────────────────────────
        //    Inherits the project's Entra credential automatically.
        AgentsClient agents = projectClient.GetAgentsClient();

        // ── Step 1: Create an agent ────────────────────────────────────────────
        //    Replaces: assistantClient.CreateAssistantAsync()
        Console.WriteLine("Creating agent...");
        Response<Agent> agentResponse = await agents.CreateAgentAsync(
            model: model,
            name: "foundry-demo-agent",
            instructions: "You are a concise assistant. Keep answers under 50 words.");
        Agent agent = agentResponse.Value;
        Console.WriteLine($"  Agent  : {agent.Id}  ({agent.Name})");

        // ── Step 2: Create a conversation thread ───────────────────────────────
        //    Replaces: assistantClient.CreateThreadAsync()
        Response<AgentThread> threadResponse = await agents.CreateThreadAsync();
        AgentThread thread = threadResponse.Value;
        Console.WriteLine($"  Thread : {thread.Id}");

        // ── Step 3: Add a user message ─────────────────────────────────────────
        //    Replaces: assistantClient.CreateMessageAsync()
        await agents.CreateMessageAsync(
            threadId: thread.Id,
            role: MessageRole.User,
            content: "Explain the Pythagorean theorem in one sentence.");
        Console.WriteLine("  Message added → user");

        // ── Step 4: Start a run and poll to completion ─────────────────────────
        //    Replaces: assistantClient.CreateRunAsync() + manual polling
        Console.Write("  Running ");
        Response<ThreadRun> runResponse = await agents.CreateRunAsync(thread.Id, agent.Id);
        ThreadRun run = runResponse.Value;

        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            run = (await agents.GetRunAsync(thread.Id, run.Id)).Value;
            Console.Write(".");
        }
        Console.WriteLine($"  {run.Status}");

        if (run.Status != RunStatus.Completed)
        {
            Console.WriteLine($"Run did not complete: {run.LastError?.Message}");
            await agents.DeleteAgentAsync(agent.Id);
            return;
        }

        // ── Step 5: Retrieve the agent's reply ────────────────────────────────
        //    Messages are returned newest-first; the first agent message is the reply.
        //    Replaces: assistantClient.GetMessagesAsync()
        AsyncPageable<ThreadMessage> messages = agents.GetMessagesAsync(thread.Id);
        await foreach (ThreadMessage message in messages)
        {
            // In Foundry the reply role is "agent"; some SDK versions surface it
            // as MessageRole.Agent while older builds use MessageRole.Assistant.
            if (message.Role == MessageRole.Agent)
            {
                foreach (MessageContent contentItem in message.ContentItems)
                {
                    if (contentItem is MessageTextContent textContent)
                        Console.WriteLine($"\nAgent reply: {textContent.Text.Value}");
                }
                break;
            }
        }

        // ── Cleanup ────────────────────────────────────────────────────────────
        await agents.DeleteAgentAsync(agent.Id);
        Console.WriteLine("\nAgent deleted (cleanup complete).");
    }
}
