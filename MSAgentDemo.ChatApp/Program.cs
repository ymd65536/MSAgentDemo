using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// local.settings.json から設定を読み込む
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

builder.AddServiceDefaults();

// AppHost の WithReference(openai) で注入された接続情報を使用
builder.AddAzureOpenAIClient("openai");

var app = builder.Build();

app.MapDefaultEndpoints();

// チャット API エンドポイント（セッションなし — Learn サンプル準拠）
app.MapPost("/api/chat", async (ChatRequest request, AzureOpenAIClient openAIClient) =>
{
    var deploymentName = builder.Configuration["OPENAI_DEPLOYMENT_NAME"] ?? "chat";

    var agent = openAIClient
        .GetChatClient(deploymentName)
        .AsAIAgent(
            instructions: "あなたは親切な日本語アシスタントです。簡潔かつ丁寧に回答してください。",
            name: "ChatAgent");

    var response = await agent.RunAsync(request.Message);

    return Results.Ok(new ChatResponse(response.Text));
});

// ストリーミング チャット API エンドポイント（セッションなし）
app.MapPost("/api/chat/stream", async (ChatRequest request, AzureOpenAIClient openAIClient, HttpContext httpContext) =>
{
    var deploymentName = builder.Configuration["OPENAI_DEPLOYMENT_NAME"] ?? "chat";

    var agent = openAIClient
        .GetChatClient(deploymentName)
        .AsAIAgent(
            instructions: "あなたは親切な日本語アシスタントです。簡潔かつ丁寧に回答してください。",
            name: "ChatAgent");

    httpContext.Response.ContentType = "text/event-stream";
    httpContext.Response.Headers["Cache-Control"] = "no-cache";

    await foreach (var update in agent.RunStreamingAsync(request.Message))
    {
        var data = JsonSerializer.Serialize(new { content = update.Text });
        await httpContext.Response.WriteAsync($"data: {data}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    await httpContext.Response.WriteAsync("data: [DONE]\n\n");
    await httpContext.Response.Body.FlushAsync();
});

// シンプルなチャット UI
app.MapGet("/", () => Results.Content(ChatHtml.Page, "text/html"));

app.Run();

// --- レコード定義 ---
record ChatRequest(string Message);
record ChatResponse(string Message);

// --- 埋め込み HTML ---
static class ChatHtml
{
    public const string Page = """
    <!DOCTYPE html>
    <html lang="ja">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>Agent Framework Chat</title>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #f5f5f5; height: 100vh; display: flex; flex-direction: column; }
            header { background: #0078d4; color: white; padding: 16px 24px; font-size: 18px; font-weight: 600; }
            #chat { flex: 1; overflow-y: auto; padding: 24px; display: flex; flex-direction: column; gap: 12px; }
            .msg { max-width: 70%; padding: 12px 16px; border-radius: 12px; line-height: 1.5; white-space: pre-wrap; word-break: break-word; }
            .user { align-self: flex-end; background: #0078d4; color: white; }
            .assistant { align-self: flex-start; background: white; border: 1px solid #e0e0e0; }
            #input-area { display: flex; gap: 8px; padding: 16px 24px; background: white; border-top: 1px solid #e0e0e0; }
            #message { flex: 1; padding: 12px; border: 1px solid #ccc; border-radius: 8px; font-size: 14px; outline: none; }
            #message:focus { border-color: #0078d4; }
            button { padding: 12px 24px; background: #0078d4; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 14px; }
            button:hover { background: #106ebe; }
            button:disabled { background: #ccc; cursor: not-allowed; }
        </style>
    </head>
    <body>
        <header>Microsoft Agent Framework Chat</header>
        <div id="chat"></div>
        <div id="input-area">
            <input id="message" type="text" placeholder="メッセージを入力..." autocomplete="off" />
            <button id="send" onclick="sendMessage()">送信</button>
        </div>
        <script>
            const chat = document.getElementById('chat');
            const input = document.getElementById('message');
            const btn = document.getElementById('send');

            input.addEventListener('keydown', e => { if (e.key === 'Enter' && !e.isComposing) sendMessage(); });

            async function sendMessage() {
                const msg = input.value.trim();
                if (!msg) return;
                input.value = '';
                btn.disabled = true;
                appendMsg('user', msg);

                const assistantDiv = appendMsg('assistant', '');

                try {
                    const res = await fetch('/api/chat/stream', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ message: msg })
                    });

                    const reader = res.body.getReader();
                    const decoder = new TextDecoder();
                    let buffer = '';

                    while (true) {
                        const { done, value } = await reader.read();
                        if (done) break;
                        buffer += decoder.decode(value, { stream: true });
                        const lines = buffer.split('\n');
                        buffer = lines.pop();
                        for (const line of lines) {
                            if (line.startsWith('data: ') && line !== 'data: [DONE]') {
                                const json = JSON.parse(line.slice(6));
                                assistantDiv.textContent += json.content;
                                chat.scrollTop = chat.scrollHeight;
                            }
                        }
                    }
                } catch (e) {
                    assistantDiv.textContent = 'エラーが発生しました: ' + e.message;
                }
                btn.disabled = false;
                input.focus();
            }

            function appendMsg(role, text) {
                const div = document.createElement('div');
                div.className = 'msg ' + role;
                div.textContent = text;
                chat.appendChild(div);
                chat.scrollTop = chat.scrollHeight;
                return div;
            }
        </script>
    </body>
    </html>
    """;
}

