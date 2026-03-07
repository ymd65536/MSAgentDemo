# MSAgentDemo

.NET Aspire を使って **Azure AI Foundry (Azure OpenAI)** と **Azure Functions**、**Azure AI Agent Service** を統合するデモプロジェクトです。

## プロジェクト構成

```
MSAgentDemo.sln
├── MSAgentDemo.AppHost/          # Aspire AppHost（オーケストレーター）
├── MSAgentDemo.Functions/        # Azure Functions (HTTP トリガー)
├── MSAgentDemo.AgentConsole/     # Azure AI Agent Service コンソールアプリ
└── MSAgentDemo.ServiceDefaults/  # Aspire 共通サービス設定
```

| プロジェクト | 役割 |
|---|---|
| **MSAgentDemo.AppHost** | Azure OpenAI と Azure Functions のリソース定義・接続設定 |
| **MSAgentDemo.Functions** | HTTP POST で Azure OpenAI にチャットリクエストを送る関数 |
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

`MSAgentDemo.AppHost/AppHost.cs` で Azure OpenAI と Azure Functions の接続を定義しています。

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Azure AI Foundry (Azure OpenAI)
var openai = builder.AddAzureOpenAI("openai");
openai.AddDeployment(
    name: "chat",
    modelName: "gpt-4o",
    modelVersion: "2024-11-20");

// Azure Functions
var functions = builder.AddAzureFunctionsProject<Projects.MSAgentDemo_Functions>("functions")
    .WithReference(openai)
    .WaitFor(openai);

builder.Build().Run();
```

- `WithReference(openai)` により、Functions の環境変数に `ConnectionStrings__openai` が自動注入されます。
- `WaitFor(openai)` により、OpenAI リソースの準備完了後に Functions が起動します。

## 主要パッケージ

### AppHost

| パッケージ | バージョン |
|---|---|
| `Aspire.Hosting.Azure.CognitiveServices` | 13.1.2 |
| `Aspire.Hosting.Azure.Functions` | 13.1.2 |

### Functions

| パッケージ | バージョン |
|---|---|
| `Aspire.Azure.AI.OpenAI` | 13.1.2-preview.1.26125.13 |
| `Azure.AI.OpenAI` | 2.8.0-beta.1 |

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
