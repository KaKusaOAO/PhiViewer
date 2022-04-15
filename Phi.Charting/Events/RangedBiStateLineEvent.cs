using System.Text.Json.Serialization;

namespace Phi.Charting.Events
{
    public abstract class RangedBiStateLineEvent : AbstractLineEvent
    {
        [JsonPropertyName("start")]
        public float Start { get; set; }
        
        [JsonPropertyName("end")]
        public float End { get; set; }
        
        [JsonPropertyName("start2")]
        public float Start2 { get; set; }
        
        [JsonPropertyName("end2")]
        public float End2 { get; set; }
    }
}