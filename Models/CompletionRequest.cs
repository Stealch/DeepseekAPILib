using System.Collections.Generic;


namespace DeepseekAPILib.Models
{
    /// <summary>
    /// Request for completion API
    /// </summary>
    public class CompletionRequest
    {
        /// <summary>
        /// Model to use for completion
        /// </summary>
        public string Model { get; set; } = "deepseek-coder";

        /// <summary>
        /// Prompt text
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// Maximum number of tokens to generate
        /// </summary>
        public int MaxTokens { get; set; } = 100;

        /// <summary>
        /// Sampling temperature (0.0 to 2.0)
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Top-p sampling parameter
        /// </summary>
        public double TopP { get; set; } = 1.0;

        /// <summary>
        /// Number of completions to generate
        /// </summary>
        public int N { get; set; } = 1;

        /// <summary>
        /// Whether to stream the response
        /// </summary>
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Stop sequences
        /// </summary>
        public List<string> Stop { get; set; }

        /// <summary>
        /// Presence penalty
        /// </summary>
        public double PresencePenalty { get; set; } = 0.0;

        /// <summary>
        /// Frequency penalty
        /// </summary>
        public double FrequencyPenalty { get; set; } = 0.0;
    }
}