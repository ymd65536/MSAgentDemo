var builder = DistributedApplication.CreateBuilder(args);

// --- Azure AI Foundry (Azure OpenAI) ---
var openai = builder.AddAzureOpenAI("openai");
openai.AddDeployment(
    name: "chat",
    modelName: "gpt-4o",
    modelVersion: "2024-11-20");

// --- Durable Task バックエンド（ローカルでは Azurite エミュレータ）---
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// --- Azure Functions (既存の Chat Function) ---
var functions = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_Functions>("functions")
    .WithReference(openai)
    .WaitFor(openai);

// --- Durable Functions プロジェクト ---
var durableFunc = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_DurableFunctions>("durable-functions")
    .WithHostStorage(storage)
    .WaitFor(storage);

// --- Agent Console ---
builder.AddProject<Projects.MSAgentDemo_AgentConsole>("agent-console")
    .WithReference(openai)
    .WithReference(durableFunc)
    .WithEnvironment("DURABLE_FUNC_URL", durableFunc.GetEndpoint("http"))
    .WaitFor(openai)
    .WaitFor(durableFunc);

// --- Agent Framework Chat App ---
builder.AddProject<Projects.MSAgentDemo_ChatApp>("chat-app")
    .WithReference(openai)
    .WaitFor(openai);

builder.Build().Run();
