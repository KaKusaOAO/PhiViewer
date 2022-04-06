using Newtonsoft.Json;

namespace Phi.Charting.Events
{
    public abstract class RangedBiStateLineEvent : AbstractLineEvent
    {
        [JsonProperty("start")]
        public float Start { get; set; }
        
        [JsonProperty("end")]
        public float End { get; set; }
        
        [JsonProperty("start2")]
        public float Start2 { get; set; }
        
        [JsonProperty("end2")]
        public float End2 { get; set; }
    }
}