var builder = DistributedApplication.CreateBuilder(args);

// --- Azure AI Foundry (Azure OpenAI) ---
var openai = builder.AddAzureOpenAI("openai");
openai.AddDeployment(
    name: "chat",
    modelName: "gpt-4o",
    modelVersion: "2024-11-20");

// --- Azure Functions ---
var functions = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_Functions>("functions")
    .WithReference(openai)
    .WaitFor(openai);

// --- Agent Console ---
builder.AddProject<Projects.MSAgentDemo_AgentConsole>("agent-console")
    .WithReference(openai)
    .WaitFor(openai);

builder.Build().Run();
