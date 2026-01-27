using System.Collections.Generic;

// Models\StreamingCompletionChunk.cs

namespace DeepseekAPILib.Models
{
    public class StreamingCompletionChunk
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<StreamingCompletionChoice> Choices { get; set; }
    }

    public class StreamingCompletionChoice
    {
        public int Index { get; set; }
        public string Text { get; set; }
        public object Logprobs { get; set; }
        public string Finish_reason { get; set; }
    }
}