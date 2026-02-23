using System.Text.Json.Serialization;

namespace DeepVision.Api.Models;

public class DeepSeekRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "deepseek-chat";

    [JsonPropertyName("messages")]
    public List<DeepSeekMessage> Messages { get; set; } = [];

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.1;
}

public class DeepSeekMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;
}

public class DeepSeekContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeepSeekImageUrl? ImageUrl { get; set; }
}

public class DeepSeekImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class DeepSeekResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<DeepSeekChoice> Choices { get; set; } = [];

    [JsonPropertyName("usage")]
    public DeepSeekUsage? Usage { get; set; }
}

public class DeepSeekChoice
{
    [JsonPropertyName("message")]
    public DeepSeekResponseMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = string.Empty;
}

public class DeepSeekResponseMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class DeepSeekUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}
