using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

public class ChatFunction
{
    private readonly ILogger<ChatFunction> _logger;
    private readonly OpenAIClient _openAIClient;
    private readonly string _deploymentName;

    public ChatFunction(ILogger<ChatFunction> logger, OpenAIClient openAIClient, IConfiguration configuration)
    {
        _logger = logger;
        _openAIClient = openAIClient;
        _deploymentName = configuration["OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o";
    }

    [Function("Chat")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        string? userMessage = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new BadRequestObjectResult("リクエストボディにメッセージを入力してください。");
        }

        _logger.LogInformation("Chat request received: {Message}", userMessage);

        var chatClient = _openAIClient.GetChatClient(_deploymentName);

        var response = await chatClient.CompleteChatAsync(
            new UserChatMessage(userMessage));

        var reply = response.Value.Content[0].Text;
        return new OkObjectResult(new { reply });
    }
}
