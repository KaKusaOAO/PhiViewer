using Newtonsoft.Json;

namespace Phi.Charting.Events
{
    public abstract class AbstractLineEvent
    {
        [JsonProperty("startTime")]
        public float StartTime { get; set; }

        [JsonProperty("endTime")]
        public float EndTime { get; set; }
    }
}