using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Configuration;

// local.settings.json → 環境変数 の優先順で設定を読み込む
var config = new ConfigurationBuilder()
    .AddJsonFile("local.settings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var endpoint = config["FOUNDRY_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_ENDPOINT を設定してください（local.settings.json または環境変数）。");

var modelName = config["OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";

// PersistentAgentsClient を使用（サブクライアントへのアクセスが可能）
var client = new PersistentAgentsClient(endpoint, new DefaultAzureCredential());

// 1. エージェント作成
Console.WriteLine("エージェントを作成中...");
PersistentAgent agent = await client.Administration.CreateAgentAsync(
    model: modelName,
    name: "DemoAgent",
    instructions: "あなたは親切なアシスタントです。日本語で回答してください。");
Console.WriteLine($"エージェント作成完了: {agent.Id}");

try
{
    // 2. スレッド作成
    Console.WriteLine("スレッドを作成中...");
    PersistentAgentThread thread = await client.Threads.CreateThreadAsync();
    Console.WriteLine($"スレッド作成完了: {thread.Id}");

    // 3. メッセージ送信
    var userMessage = "Azure AI Foundryとは何ですか？簡潔に教えてください。";
    Console.WriteLine($"\nユーザー: {userMessage}");
    await client.Messages.CreateMessageAsync(thread.Id, MessageRole.User, userMessage);

    // 4. 実行（Run）を作成してポーリング
    Console.WriteLine("実行中...");
    ThreadRun run = await client.Runs.CreateRunAsync(thread.Id, agent.Id);

    while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress)
    {
        await Task.Delay(1000);
        run = await client.Runs.GetRunAsync(thread.Id, run.Id);
    }

    if (run.Status == RunStatus.Failed)
    {
        Console.WriteLine($"実行失敗: {run.LastError}");
        return;
    }

    // 5. レスポンス取得
    Console.WriteLine("\nアシスタントの応答:");
    await foreach (PersistentThreadMessage message in client.Messages.GetMessagesAsync(thread.Id))
    {
        if (message.Role == MessageRole.Agent)
        {
            foreach (var content in message.ContentItems)
            {
                if (content is MessageTextContent textContent)
                {
                    Console.WriteLine(textContent.Text);
                }
            }
            break;
        }
    }
}
finally
{
    // 6. エージェント削除
    Console.WriteLine("\nエージェントを削除中...");
    await client.Administration.DeleteAgentAsync(agent.Id);
    Console.WriteLine("完了");
}
