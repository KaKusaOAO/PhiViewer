using System.Text.Json.Serialization;

namespace Phi.Charting.Events
{
    public abstract class AbstractLineEvent
    {
        [JsonPropertyName("startTime")]
        public float StartTime { get; set; }

        [JsonPropertyName("endTime")]
        public float EndTime { get; set; }
    }
}