using System.Text.Json.Serialization;
using System.Text.Json;
using System.Collections.Generic;

namespace RPSystem.Core.Services
{
    // --- REQUEST MODELS ---

    /// <summary>
    /// Represents the overall request payload sent to the OpenRouter chat completions endpoint.
    /// </summary>
    public class ChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatApiMessage> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 2000; // Default value
    }

    /// <summary>
    /// Represents a single message in the conversation history.
    /// The 'Content' can be a simple string (for text) or a complex object (for vision).
    /// Using JsonDocument for flexible content serialization without ArgumentException.
    /// </summary>
    public class ChatApiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";

        [JsonPropertyName("content")]
        [JsonConverter(typeof(ChatMessageContentConverter))]
        public object Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Custom JSON converter for ChatApiMessage.Content that handles both string and complex content.
    /// This prevents ArgumentException when serializing object-typed Content property.
    /// </summary>
    public class ChatMessageContentConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // For deserialization, handle both string and array/object
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<List<VisionContentPart>>(ref reader, options) ?? new List<VisionContentPart>();
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                return JsonSerializer.Deserialize<JsonDocument>(ref reader, options) ?? JsonDocument.Parse("{}");
            }

            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else if (value is string str)
            {
                writer.WriteStringValue(str);
            }
            else if (value is List<VisionContentPart> parts)
            {
                JsonSerializer.Serialize(writer, parts, options);
            }
            else if (value is JsonDocument doc)
            {
                JsonSerializer.Serialize(writer, doc, options);
            }
            else
            {
                // Fallback: serialize as JSON
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }

    // -- Specialized Content classes for Vision models --

    // FIX: Add these attributes so System.Text.Json knows about the subclasses
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentPart), typeDiscriminator: "text")]
[JsonDerivedType(typeof(ImageUrlContentPart), typeDiscriminator: "image_url")]
[JsonDerivedType(typeof(InputAudioContentPart), typeDiscriminator: "input_audio")]
    /// <summary>
    /// A content part for multi-modal requests (e.g., text and images).
    /// </summary>
    public abstract class VisionContentPart
    {
        // 'type' is handled by the JsonDerivedType attribute now,
        // but we keep the property for compatibility if needed.
        [JsonIgnore]
        public string Type { get; protected set; } = string.Empty;
    }

    public class TextContentPart : VisionContentPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        public TextContentPart(string text)
        {
            Type = "text";
            Text = text;
        }

        // parameterless constructor for deserialization
        public TextContentPart() { Type = "text"; }
    }

    public class ImageUrlContentPart : VisionContentPart
    {
        [JsonPropertyName("image_url")]
        public ImageUrlData ImageUrl { get; set; } = new();

        public ImageUrlContentPart(string url)
        {
            Type = "image_url";
            ImageUrl = new ImageUrlData { Url = url };
        }

        // parameterless constructor for deserialization
        public ImageUrlContentPart() { Type = "image_url"; }
    }

    public class ImageUrlData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    public class InputAudioContentPart : VisionContentPart
    {
        [JsonPropertyName("input_audio")]
        public InputAudioData InputAudio { get; set; } = new();

        public InputAudioContentPart(string base64Audio, string format)
        {
            Type = "input_audio";
            InputAudio = new InputAudioData
            {
                Data = base64Audio,
                Format = format
            };
        }

        public InputAudioContentPart()
        {
            Type = "input_audio";
        }
    }

    public class InputAudioData
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = "wav";
    }


    // --- RESPONSE MODELS ---

    /// <summary>
    /// Represents the top-level JSON response from the OpenRouter API.
    /// </summary>
    public class ChatResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<ResponseChoice> Choices { get; set; } = new();
    }

    /// <summary>
    /// Represents one of the possible completions returned by the model.
    /// </summary>
    public class ResponseChoice
    {
        [JsonPropertyName("message")]
        public ResponseMessage Message { get; set; } = new();
    }

    /// <summary>
    /// Contains the actual text content of the AI's reply.
    /// </summary>
    public class ResponseMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
