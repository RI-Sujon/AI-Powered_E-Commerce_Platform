using System.ComponentModel.DataAnnotations;

namespace Project.Object.Requests
{
    /// <summary>
    /// A shopping-assistant chat turn. The client keeps the running conversation and sends it
    /// each time (the server is stateless). Only the most recent turns are actually used.
    /// </summary>
    public class AssistantChatRequest
    {
        [Required]
        [MinLength(1)]
        public List<AssistantMessage> Messages { get; set; } = new();
    }

    public class AssistantMessage
    {
        /// <summary>"user" or "assistant".</summary>
        [Required]
        public string Role { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Content { get; set; } = string.Empty;
    }
}
