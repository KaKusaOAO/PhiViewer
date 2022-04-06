using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Phi.Charting.Events;
using Phi.Charting.Notes;

namespace Phi.Charting
{
    public class JudgeLine
    {
        [JsonProperty("bpm")]
        public float Bpm { get; set; }
        
        [JsonProperty("speedEvents")]
        public List<SpeedEvent> SpeedEvents { get; set; }
        
        [JsonProperty("notesAbove")]
        public List<Note> NotesAbove { get; set; }
        
        [JsonProperty("notesBelow")]
        public List<Note> NotesBelow { get; set; }
        
        [JsonProperty("judgeLineDisappearEvents")]
        public List<LineFadeEvent> LineFadeEvents { get; set; }

        [JsonProperty("judgeLineMoveEvents")]
        public List<LineMoveEvent> LineMoveEvents { get; set; }

        [JsonProperty("judgeLineRotateEvents")]
        public List<LineRotateEvent> LineRotateEvents { get; set; }
        
        [JsonProperty("numOfNotesAbove")]
        public int NotesCountAbove { get; set; }
        
        [JsonProperty("numOfNotesBelow")]
        public int NotesCountBelow { get; set; }
        
        [JsonProperty("numOfNotes")]
        public int NotesCount { get; set; }

        internal void ProcessEvents(int formatVersion)
        {
            if (formatVersion != 1) return;
            
            foreach (var ev in LineMoveEvents)
            {
                var xCenter = 440f;
                var yCenter = 260f;

                var startX = MathF.Floor(ev.Start / 1000f);
                var startY = MathF.Round(ev.Start % 1000);

                var endX = MathF.Floor(ev.End / 1000f);
                var endY = MathF.Round(ev.End % 1000);

                ev.Start = startX / xCenter / 2;
                ev.Start2 = startY / xCenter / 2;
                ev.End = endX / yCenter / 2;
                ev.End2 = endY / yCenter / 2;
            }
        }
    }
}