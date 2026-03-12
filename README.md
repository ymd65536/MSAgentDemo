# MSAgentDemo

.NET Aspire を使って **Azure AI Foundry (Azure OpenAI)** と **Azure Functions**、**Azure AI Agent Service** を統合するデモプロジェクトです。

## プロジェクト構成

```
MSAgentDemo.sln
├── MSAgentDemo.AppHost/              # Aspire AppHost（オーケストレーター）
├── MSAgentDemo.Functions/            # Azure Functions (HTTP トリガー)
├── MSAgentDemo.DurableFunctions/     # Durable Functions (Orchestrator/Activity)
├── MSAgentDemo.ChatApp/             # Microsoft Agent Framework チャット Web アプリ
├── MSAgentDemo.AgentConsole/        # Azure AI Agent Service コンソールアプリ
└── MSAgentDemo.ServiceDefaults/     # Aspire 共通サービス設定
```

| プロジェクト | 役割 |
|---|---|
| **MSAgentDemo.AppHost** | 全リソースのオーケストレーション（Azure OpenAI、Storage エミュレータ、Functions、ChatApp） |
| **MSAgentDemo.Functions** | HTTP POST で Azure OpenAI にチャットリクエストを送る関数 |
| **MSAgentDemo.DurableFunctions** | Durable Functions（Orchestrator/Activity パターン）でエージェントツールを実装 |
| **MSAgentDemo.ChatApp** | Microsoft Agent Framework (`Microsoft.Agents.AI.OpenAI`) を使った Web チャットアプリ |
| **MSAgentDemo.AgentConsole** | Azure AI Agent Service (Persistent Agents) を使ったコンソールアプリ |
| **MSAgentDemo.ServiceDefaults** | OpenTelemetry 等の共通設定 |

## 前提条件

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)（`az login` 済み）
- Azure OpenAI リソース（gpt-4o デプロイ済み）

### Azure Functions Core Tools のインストール（macOS）

```bash
brew tap azure/functions
brew install azure-functions-core-tools@4
```

## リソース定義（AppHost）

`MSAgentDemo.AppHost/AppHost.cs` で全リソースの接続を定義しています。

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Azure AI Foundry (Azure OpenAI)
var openai = builder.AddAzureOpenAI("openai");
openai.AddDeployment(
    name: "chat",
    modelName: "gpt-4o",
    modelVersion: "2024-11-20");

// Durable Task バックエンド（ローカルでは Azurite エミュレータ）
var storage = builder.AddAzureStorage("storage").RunAsEmulator();

// Azure Functions (既存の Chat Function)
var functions = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_Functions>("functions")
    .WithReference(openai)
    .WaitFor(openai);

// Durable Functions プロジェクト
var durableFunc = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_DurableFunctions>("durable-functions")
    .WithHostStorage(storage)
    .WaitFor(storage);

// Agent Console
builder.AddProject<Projects.MSAgentDemo_AgentConsole>("agent-console")
    .WithReference(openai)
    .WithReference(durableFunc)
    .WithEnvironment("DURABLE_FUNC_URL", durableFunc.GetEndpoint("http"))
    .WaitFor(openai)
    .WaitFor(durableFunc);

// Agent Framework Chat App
builder.AddProject<Projects.MSAgentDemo_ChatApp>("chat-app")
    .WithReference(openai)
    .WaitFor(openai);

builder.Build().Run();
```

- `RunAsEmulator()` により、ローカル開発では Azurite ストレージエミュレータが自動起動します
- `WithHostStorage(storage)` で Durable Functions のバックエンドストレージが接続されます
- `WithReference(openai)` で Azure OpenAI の接続情報が各プロジェクトに自動注入されます

## 主要パッケージ

### AppHost

| パッケージ | バージョン |
|---|---|
| `Aspire.Hosting.Azure.CognitiveServices` | 13.1.2 |
| `Aspire.Hosting.Azure.Functions` | 13.1.2 |
| `Aspire.Hosting.Azure.Storage` | 13.1.2 |

### Functions

| パッケージ | バージョン |
|---|---|
| `Aspire.Azure.AI.OpenAI` | 13.1.2-preview.1.26125.13 |
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |

### DurableFunctions

| パッケージ | バージョン |
|---|---|
| `Microsoft.Azure.Functions.Worker` | 2.51.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.DurableTask` | 1.3.0 |
| `Microsoft.Azure.Functions.Worker.Extensions.Http` | 3.3.0 |
| `Microsoft.Azure.Functions.Worker.Sdk` | 2.0.7 |

### ChatApp

| パッケージ | バージョン |
|---|---|
| `Microsoft.Agents.AI.OpenAI` | 1.0.0-rc3 |
| `Aspire.Azure.AI.OpenAI` | 13.1.2-preview.1.26125.13 |
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |
| `Azure.Identity` | 1.18.0 |

### AgentConsole

| パッケージ | バージョン |
|---|---|
| `Azure.AI.Agents.Persistent` | 1.1.0 |
| `Azure.Identity` | 1.18.0 |

> **注意**: `Azure.AI.OpenAI` は `OpenAI` パッケージとのバージョン競合を防ぐため明示的に `2.8.0-beta.1` を指定しています。

## 起動方法

### Aspire AppHost 経由（推奨）

```bash
cd MSAgentDemo.AppHost
dotnet run
```

AppHost が Azure OpenAI の接続情報を自動注入するため、`local.settings.json` の設定は不要です。

### Functions 単体起動

```bash
cd MSAgentDemo.Functions
dotnet run
```

単体で起動する場合は `local.settings.json` に接続情報を設定してください。

```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
        "ConnectionStrings__openai": "https://<your-resource-name>.openai.azure.com/",
        "OPENAI_DEPLOYMENT_NAME": "gpt-4o"
    }
}
```

認証は `DefaultAzureCredential` を使用します。`az login` でログイン済みであればローカルから認証が通ります。

## 動作確認

Functions 起動後、別ターミナルから POST リクエストを送信します。

```bash
curl -s -X POST http://localhost:7071/api/Chat \
  -H "Content-Type: text/plain" \
  -d "こんにちは！自己紹介をしてください。"
```

### レスポンス例

```json
{
  "reply": "こんにちは！私はAIアシスタントです。..."
}
```

## AgentConsole の使い方

Azure AI Agent Service (Persistent Agents) を使ってエージェントの作成・会話・削除を行うコンソールアプリです。

### 前提条件

- Azure AI Foundry プロジェクトが作成済みであること
- プロジェクトにモデル（gpt-4o 等）がデプロイ済みであること

### 単体起動

`local.settings.json` にエンドポイントを設定して起動します。

```json
{
    "FOUNDRY_ENDPOINT": "https://<your-resource>.services.ai.azure.com/api/projects/<your-project>",
    "OPENAI_DEPLOYMENT_NAME": "gpt-4o"
}
```

```bash
cd MSAgentDemo.AgentConsole
dotnet run
```

環境変数でも指定可能です（環境変数が `local.settings.json` より優先されます）。

```bash
export FOUNDRY_ENDPOINT="https://<your-resource>.services.ai.azure.com/api/projects/<your-project>"
cd MSAgentDemo.AgentConsole
dotnet run
```

### Aspire 経由

AppHost が `FOUNDRY_ENDPOINT` を環境変数経由で渡します。

```bash
cd MSAgentDemo.AppHost
dotnet run
```

### 動作例

```
エージェントを作成中...
エージェント作成完了: asst_xxxx
スレッドを作成中...
スレッド作成完了: thread_xxxx

ユーザー: Azure AI Foundryとは何ですか？簡潔に教えてください。
実行中...

アシスタントの応答:
Azure AI Foundry は、Microsoft が提供する AI アプリケーション開発プラットフォームです。...

エージェントを削除中...
完了
```

## 環境変数

| 変数名 | 説明 | 必須 |
|---|---|---|
| `ConnectionStrings__openai` | Azure OpenAI のエンドポイント URL | AppHost 経由なら自動注入 |
| `OPENAI_DEPLOYMENT_NAME` | Azure OpenAI のデプロイ名（デフォルト: `gpt-4o`） | 任意 |
| `FOUNDRY_ENDPOINT` | Azure AI Foundry プロジェクトのエンドポイント | AgentConsole で必須 |
| `DURABLE_FUNC_URL` | Durable Functions の HTTP エンドポイント | AppHost 経由なら自動注入 |

## Durable Functions

### 概要

`MSAgentDemo.DurableFunctions` は Azure Durable Functions の **Orchestrator/Activity パターン** を実装したプロジェクトです。Azurite ストレージエミュレータをバックエンドに使い、ローカルで完結して動作します。

### アーキテクチャ

```
HTTP POST /api/workflow/start
    → StartWorkflow (HTTP Starter)
        → ComplexWorkflow (Orchestrator)
            → Tool_Search (Activity) → Tool_Process (Activity)
        → 結果を返却
```

### 動作確認

```bash
# ワークフローを開始
curl -s -X POST http://localhost:<port>/api/workflow/start \
  -H "Content-Type: text/plain" \
  -d "Azure Durable Functionsについて"
```

レスポンス (HTTP 202) にステータス確認 URL が含まれます。

### macOS ARM64 での注意事項

Durable Functions が使用する `Grpc.Core` NuGet パッケージには macOS ARM64 (Apple Silicon) 用のネイティブバイナリが含まれていません。このプロジェクトでは gRPC v1.46.6 のソースコードからビルドした `libgrpc_csharp_ext.arm64.dylib` を `native/osx-arm64/` に配置し、MSBuild のポストビルドターゲットで `.azurefunctions/runtimes/osx-arm64/native/` にコピーしています。

## ChatApp（Microsoft Agent Framework）

### 概要

`MSAgentDemo.ChatApp` は [Microsoft Agent Framework](https://learn.microsoft.com/azure/ai-services/agents/) の `Microsoft.Agents.AI.OpenAI` パッケージを使った Web チャットアプリケーションです。ブラウザ上でストリーミング応答付きのチャット UI を提供します。

### 使用パターン

[Learn サンプル](https://learn.microsoft.com/azure/ai-services/agents/)と同じステートレスなパターンを採用しています:

```csharp
var agent = openAIClient
    .GetChatClient(deploymentName)
    .AsAIAgent(instructions: "...", name: "ChatAgent");

var response = await agent.RunAsync("メッセージ");
Console.WriteLine(response.Text);
```

### エンドポイント

| メソッド | パス | 説明 |
|---|---|---|
| `GET` | `/` | チャット UI (HTML) |
| `POST` | `/api/chat` | 通常のチャット API（JSON レスポンス） |
| `POST` | `/api/chat/stream` | ストリーミングチャット API（Server-Sent Events） |

### リクエスト例

```bash
# 通常チャット
curl -s -X POST http://localhost:<port>/api/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "こんにちは！"}'

# ストリーミングチャット
curl -N -X POST http://localhost:<port>/api/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Azure AI について教えて"}'
```

### 設定

`local.settings.json` にデプロイメント名を設定できます:

```json
{
    "OPENAI_DEPLOYMENT_NAME": "gpt-4o"
}
```

Aspire 経由で起動する場合は `ConnectionStrings__openai` が自動注入されるため、追加設定は不要です。

---

## 🚧 作業ログ（WIP）

### ChatApp の作成（Microsoft Agent Framework 対応チャットアプリ）

[Microsoft Agent Framework](https://learn.microsoft.com/ja-jp/agent-framework/overview/?pivots=programming-language-csharp) に基づき、Aspire 対応の Web チャットアプリ `MSAgentDemo.ChatApp` を新規プロジェクトとして追加した。

#### 実施内容

1. **プロジェクト作成**
   - `dotnet new web -n MSAgentDemo.ChatApp -f net10.0` で ASP.NET Core Web プロジェクトを作成
   - ソリューションに追加 (`dotnet sln add`)

2. **NuGet パッケージの追加**
   - `Microsoft.Agents.AI.OpenAI` (1.0.0-rc3) — Microsoft Agent Framework の OpenAI プロバイダー
   - `Aspire.Azure.AI.OpenAI` (13.1.2-preview) — Aspire の Azure OpenAI 統合
   - `Azure.AI.OpenAI` (2.8.0-beta.1)
   - `Azure.Identity` (1.18.0)
   - `MSAgentDemo.ServiceDefaults` への参照

3. **チャット API の実装** (`Program.cs`)
   - `builder.AddAzureOpenAIClient("openai")` で Aspire 経由の DI 登録
   - `ChatClient.AsAIAgent()` 拡張メソッドで `ChatClientAgent` を生成
   - `POST /api/chat` — 通常レスポンス（`AgentResponse.Text`）
   - `POST /api/chat/stream` — Server-Sent Events によるストリーミング応答（`AgentResponseUpdate.Text`）
   - `GET /` — ブラウザ用チャット UI（埋め込み HTML）
   - `AgentSession` によるセッション管理（マルチターン会話対応）

4. **Aspire AppHost への登録**
   - `AppHost.cs` に `builder.AddProject<Projects.MSAgentDemo_ChatApp>("chat-app")` を追加
   - `.WithReference(openai).WaitFor(openai)` で Azure OpenAI の接続情報を自動注入
   - `MSAgentDemo.AppHost.csproj` にプロジェクト参照を追加

5. **ビルド確認**
   - `dotnet build MSAgentDemo.ChatApp/MSAgentDemo.ChatApp.csproj` — ビルド成功
   - `dotnet run` (AppHost 経由) — 起動確認済み

#### 技術的なポイント

- `Microsoft.Agents.AI.OpenAI` パッケージの `AsAIAgent()` 拡張メソッドは `OpenAI.Chat.ChatClient` と `OpenAI.Responses.ResponsesClient` の両方に定義されている
- `RunAsync()` の戻り値は `AgentResponse` 型で、テキスト取得には `.Text` プロパティを使用
- `RunStreamingAsync()` は `IAsyncEnumerable<AgentResponseUpdate>` を返し、各チャンクの `.Text` でストリーミングテキストを取得
- `ChatClientAgent.CreateSessionAsync()` でセッションを生成し、`RunAsync()` / `RunStreamingAsync()` の第2引数に渡すことで会話履歴が維持される

#### 参考リンク

- [Microsoft Agent Framework 概要](https://learn.microsoft.com/ja-jp/agent-framework/overview/?pivots=programming-language-csharp)
- [Step 1: Your First Agent](https://learn.microsoft.com/en-us/agent-framework/get-started/your-first-agent?pivots=programming-language-csharp)
- [Session（会話状態管理）](https://learn.microsoft.com/en-us/agent-framework/agents/conversations/session?pivots=programming-language-csharp)
