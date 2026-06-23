using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPSystem.Core.Services
{
    /// <summary>
    /// Request model for streaming chat completions.
    /// Adds stream: true parameter to ChatRequest.
    /// </summary>
    public class StreamChatRequest : ChatRequest
    {
        public StreamChatRequest()
        {
            Stream = true;
        }

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }
    }

    // --- STREAMING RESPONSE MODELS ---

    /// <summary>
    /// Represents a single streaming chunk from Server-Sent Events (SSE).
    /// Each chunk contains partial content as it's generated.
    /// Format: data: {"id":"...","object":"chat.completion.chunk",...}
    /// </summary>
    public class StreamChunk
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("object")]
        public string Object { get; set; } = "chat.completion.chunk";

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("choices")]
        public List<StreamChoice> Choices { get; set; } = new List<StreamChoice>();

        /// <summary>
        /// Check if this is the final chunk (stream completion marker)
        /// </summary>
        public bool IsDone => Choices.Count > 0 && Choices[0].FinishReason != null;
    }

    /// <summary>
    /// Represents a choice in a streaming chunk.
    /// </summary>
    public class StreamChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("delta")]
        public StreamDelta Delta { get; set; } = new StreamDelta();

        [JsonPropertyName("finish_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FinishReason { get; set; }

        /// <summary>
        /// Check if this chunk contains content delta
        /// </summary>
        public bool HasContent => !string.IsNullOrEmpty(Delta?.Content);

        /// <summary>
        /// Check if this is the final chunk
        /// </summary>
        public bool IsFinal => FinishReason != null;
    }

    /// <summary>
    /// Represents incremental updates in a streaming chunk.
    /// Content is delivered token by token in this field.
    /// </summary>
    public class StreamDelta
    {
        [JsonPropertyName("role")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }

        /// <summary>
        /// Check if this delta has any content
        /// </summary>
        public bool HasContent => !string.IsNullOrEmpty(Content);
    }

    /// <summary>
    /// Result type for streaming operations.
    /// Either contains a content chunk or indicates completion.
    /// </summary>
    public class StreamResult
    {
        /// <summary>
        /// Content delta for this chunk (partial text)
        /// </summary>
        public string? Content { get; set; }

        /// <summary>
        /// Whether the stream is complete
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Finish reason when stream is complete (e.g., "stop", "length")
        /// </summary>
        public string? FinishReason { get; set; }

        /// <summary>
        /// Error message if streaming failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Create a content result
        /// </summary>
        public static StreamResult ContentChunk(string content)
        {
            return new StreamResult
            {
                Content = content,
                IsComplete = false
            };
        }

        /// <summary>
        /// Create a completion result
        /// </summary>
        public static StreamResult Complete(string finishReason = "stop")
        {
            return new StreamResult
            {
                IsComplete = true,
                FinishReason = finishReason
            };
        }

        /// <summary>
        /// Create an error result
        /// </summary>
        public static StreamResult CreateError(string error)
        {
            return new StreamResult
            {
                IsComplete = true,
                Error = error
            };
        }
    }

    /// <summary>
    /// SSE parsing utilities for handling Server-Sent Events.
    /// </summary>
    public static class SseParser
    {
        /// <summary>
        /// Parses an SSE line and extracts the JSON data.
        /// Returns null if line is empty or is [DONE] marker.
        /// </summary>
        public static string? ParseSseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Remove "data: " prefix
            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                // Check for [DONE] marker
                if (data == "[DONE]")
                    return null;
                return data;
            }

            return null;
        }

        /// <summary>
        /// Attempts to deserialize an SSE line into a StreamChunk.
        /// Returns null if parsing fails or line is [DONE].
        /// </summary>
        public static StreamChunk? TryParseChunk(string line)
        {
            var jsonData = ParseSseLine(line);
            if (jsonData == null)
                return null;

            try
            {
                return JsonSerializer.Deserialize<StreamChunk>(jsonData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts the content delta from a stream chunk if available.
        /// Returns null if no content in this chunk.
        /// </summary>
        public static string? ExtractContent(StreamChunk chunk)
        {
            return chunk?.Choices?.Count > 0
                ? chunk.Choices[0].Delta?.Content
                : null;
        }

        /// <summary>
        /// Extracts the finish reason from a stream chunk if this is the final chunk.
        /// Returns null if stream is not complete.
        /// </summary>
        public static string? ExtractFinishReason(StreamChunk chunk)
        {
            return chunk?.Choices?.Count > 0 && chunk.Choices[0].IsFinal
                ? chunk.Choices[0].FinishReason
                : null;
        }

        /// <summary>
        /// Checks if a line is the SSE [DONE] marker
        /// </summary>
        public static bool IsDoneMarker(string line)
        {
            var trimmed = line.Trim();
            return trimmed == "data: [DONE]" || trimmed == "[DONE]";
        }
    }
}
