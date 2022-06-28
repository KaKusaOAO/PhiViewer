using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Phi.Charting.Events;
using Phi.Charting.Notes;

namespace Phi.Charting
{
    public class JudgeLine
    {
        [JsonPropertyName("bpm")]
        public float Bpm { get; set; }
        
        [JsonPropertyName("speedEvents")]
        public List<SpeedEvent> SpeedEvents { get; set; }
        
        [JsonPropertyName("notesAbove")]
        public List<Note> NotesAbove { get; set; }
        
        [JsonPropertyName("notesBelow")]
        public List<Note> NotesBelow { get; set; }
        
        [JsonPropertyName("judgeLineDisappearEvents")]
        public List<LineFadeEvent> LineFadeEvents { get; set; }

        [JsonPropertyName("judgeLineMoveEvents")]
        public List<LineMoveEvent> LineMoveEvents { get; set; }

        [JsonPropertyName("judgeLineRotateEvents")]
        public List<LineRotateEvent> LineRotateEvents { get; set; }
        
        [JsonPropertyName("numOfNotesAbove")]
        public int NotesCountAbove { get; set; }
        
        [JsonPropertyName("numOfNotesBelow")]
        public int NotesCountBelow { get; set; }
        
        [JsonPropertyName("numOfNotes")]
        public int NotesCount { get; set; }

        internal void ProcessEvents(int formatVersion)
        {
            if (formatVersion != 1) return;

            var posY = 0f;
            foreach (var ev in SpeedEvents)
            {
                ev.FloorPosition = posY;
                posY += ev.Value * (ev.EndTime - ev.StartTime) / Bpm * 1.875f;
            }
            
            foreach (var ev in LineMoveEvents)
            {
                var xCenter = 440f;
                var yCenter = 260f;

                var startX = MathF.Floor(ev.Start / 1000f);
                var startY = MathF.Round(ev.Start % 1000);

                var endX = MathF.Floor(ev.End / 1000f);
                var endY = MathF.Round(ev.End % 1000);

                ev.Start = startX / xCenter / 2;
                ev.Start2 = startY / yCenter / 2;
                ev.End = endX / xCenter / 2;
                ev.End2 = endY / yCenter / 2;
            }
        }
    }
}