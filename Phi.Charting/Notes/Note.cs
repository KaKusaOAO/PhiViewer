using Newtonsoft.Json;

namespace Phi.Charting.Notes
{
    public class Note
    {
        [JsonProperty("type")]
        public NoteType Type { get; set; }
        
        [JsonProperty("time")]
        public float Time { get; set; }
        
        [JsonProperty("positionX")]
        public float PosX { get; set; }
        
        [JsonProperty("speed")]
        public float Speed { get; set; }
        
        [JsonProperty("floorPosition")]
        public float FloorPosition { get; set; }
        
        [JsonProperty("holdTime")]
        public float HoldTime { get; set; }
        
        [JsonIgnore]
        public bool HasSibling { get; set; }
    }
}