using System.Text.Json.Serialization;

namespace Phi.Charting.Notes
{
    public class Note
    {
        [JsonPropertyName("type")]
        public NoteType Type { get; set; }
        
        [JsonPropertyName("time")]
        public float Time { get; set; }
        
        [JsonPropertyName("positionX")]
        public float PosX { get; set; }
        
        [JsonPropertyName("speed")]
        public float Speed { get; set; }
        
        [JsonPropertyName("floorPosition")]
        public float FloorPosition { get; set; }
        
        [JsonPropertyName("holdTime")]
        public float HoldTime { get; set; }
        
        [JsonIgnore]
        public bool HasSibling { get; set; }
    }
}