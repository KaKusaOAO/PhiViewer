using Newtonsoft.Json;

namespace Phi.Charting.Events
{
    public class SpeedEvent : AbstractLineEvent
    {
        [JsonProperty("value")]
        public float Value { get; set; }
    }
}