using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace MSAgentDemo.DurableFunctions;

public static class AgentTools
{
    // --- Orchestrator: エージェントから依頼された複雑な手順を実行 ---
    [Function("ComplexWorkflow")]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<string>() ?? string.Empty;

        // DTS エミュレータのおかげで、ステップ遷移が高速
        var step1 = await context.CallActivityAsync<string>("Tool_Search", input);
        var step2 = await context.CallActivityAsync<string>("Tool_Process", step1);

        return step2;
    }

    // --- Activity: 検索ツール ---
    [Function("Tool_Search")]
    public static string Search([ActivityTrigger] string query, FunctionContext context)
    {
        var logger = context.GetLogger("Tool_Search");
        logger.LogInformation("Searching for: {Query}", query);
        return $"検索結果: '{query}' に関する情報が見つかりました。";
    }

    // --- Activity: 処理ツール ---
    [Function("Tool_Process")]
    public static string Process([ActivityTrigger] string input, FunctionContext context)
    {
        var logger = context.GetLogger("Tool_Process");
        logger.LogInformation("Processing: {Input}", input);
        return $"処理完了: {input} → 最終結果を生成しました。";
    }

    // --- HTTP Starter: Orchestrator を開始する HTTP エンドポイント ---
    [Function("StartWorkflow")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflow/start")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("StartWorkflow");

        string input = await new StreamReader(req.Body).ReadToEndAsync();
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync("ComplexWorkflow", input);

        logger.LogInformation("Started orchestration with ID = '{InstanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}
