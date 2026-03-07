using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Azure AI Foundry (Azure OpenAI) クライアントを DI に登録
// AppHost の WithReference(openai) で注入された接続情報を使用
builder.AddAzureOpenAIClient("openai");

builder.Build().Run();
