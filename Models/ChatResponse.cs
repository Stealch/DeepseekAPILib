using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DeepseekAPILib.Models
{
    /// <summary>
    /// Response from chat completion API
    /// </summary>
    public class ChatResponse
    {
        /// <summary>
        /// Unique identifier for the chat completion
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Object type
        /// </summary>
        public string Object { get; set; }

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public long Created { get; set; }

        /// <summary>
        /// Model used
        /// </summary>
        public string Model { get; set; }

        /// <summary>
        /// List of chat choices
        /// </summary>
        public List<ChatChoice> Choices { get; set; }

        /// <summary>
        /// Usage statistics
        /// </summary>
        public ChatUsage Usage { get; set; }
    }

    /// <summary>
    /// Chat choice
    /// </summary>
    public class ChatChoice
    {
        /// <summary>
        /// Index of the choice
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Message from the assistant
        /// </summary>
        public DeepseekAPILib.ChatMessage Message { get; set; }

        /// <summary>
        /// Finish reason
        /// </summary>
        public string FinishReason { get; set; }
    }

    /// <summary>
    /// Usage statistics for chat
    /// </summary>
    public class ChatUsage
    {
        /// <summary>
        /// Number of prompt tokens
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Number of completion tokens
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Total number of tokens
        /// </summary>
        public int TotalTokens { get; set; }
    }
}
