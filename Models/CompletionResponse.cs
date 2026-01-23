using System.Collections.Generic;

namespace DeepseekAPILib.Models
{
    /// <summary>
    /// Response from completion API
    /// </summary>
    public class CompletionResponse
    {
        /// <summary>
        /// Unique identifier for the completion
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
        /// List of completion choices
        /// </summary>
        public List<CompletionChoice> Choices { get; set; }

        /// <summary>
        /// Usage statistics
        /// </summary>
        public CompletionUsage Usage { get; set; }
    }

    /// <summary>
    /// Completion choice
    /// </summary>
    public class CompletionChoice
    {
        /// <summary>
        /// Generated text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Index of the choice
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Log probabilities
        /// </summary>
        public object Logprobs { get; set; }

        /// <summary>
        /// Finish reason
        /// </summary>
        public string FinishReason { get; set; }
    }

    /// <summary>
    /// Usage statistics
    /// </summary>
    public class CompletionUsage
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