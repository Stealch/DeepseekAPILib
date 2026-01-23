using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepseekAPILib
{
    /// <summary>
    /// Represents a message in a chat conversation
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// Role of the message sender (system, user, assistant)
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Content of the message
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Creates a new chat message
        /// </summary>
        /// <param name="role">Role of the sender</param>
        /// <param name="content">Message content</param>
        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        /// <summary>
        /// Creates a system message
        /// </summary>
        public static ChatMessage CreateSystemMessage(string content) => new ChatMessage("system", content);

        /// <summary>
        /// Creates a user message
        /// </summary>
        public static ChatMessage CreateUserMessage(string content) => new ChatMessage("user", content);

        /// <summary>
        /// Creates an assistant message
        /// </summary>
        public static ChatMessage CreateAssistantMessage(string content) => new ChatMessage("assistant", content);
    }
}