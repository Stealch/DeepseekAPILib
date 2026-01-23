using System.Collections.Generic;


namespace DeepseekAPILib.Models
{
    public class StreamingChatChunk
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<StreamingChatChoice> Choices { get; set; }
    }

    public class StreamingChatChoice
    {
        public int Index { get; set; }
        public Delta Delta { get; set; }
        public string Finish_reason { get; set; }
    }

    public class Delta
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}