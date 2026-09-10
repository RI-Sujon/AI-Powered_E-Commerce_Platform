using System.ComponentModel.DataAnnotations;

namespace Project.Object.Requests
{
    /// <summary>
    /// Optional knobs for the admin "generate product description" feature. An empty body is valid.
    /// </summary>
    public class GenerateProductDescriptionRequest
    {
        /// <summary>Writing tone, e.g. "premium", "playful", "technical". Defaults to friendly &amp; professional.</summary>
        [MaxLength(40)]
        public string? Tone { get; set; }

        /// <summary>Approximate upper bound on length. Clamped to 20-200. Defaults to 80.</summary>
        [Range(20, 200)]
        public int? MaxWords { get; set; }
    }
}
