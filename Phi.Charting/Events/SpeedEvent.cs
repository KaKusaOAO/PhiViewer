using System.Text.Json.Serialization;

namespace Phi.Charting.Events
{
    public class SpeedEvent : AbstractLineEvent
    {
        [JsonPropertyName("value")]
        public float Value { get; set; }
    }
}