using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phi.Charting
{
    public class Chart
    {
        [JsonProperty("offset")]
        public float Offset { get; set; }
        
        [JsonProperty("numOfNotes")]
        public int NoteCount { get; set; }
        
        [JsonProperty("judgeLineList")]
        public List<JudgeLine> JudgeLines { get; set; }

        public static Chart Deserialize(string json)
        {
            var data = JsonConvert.DeserializeObject<JObject>(json);
            if (data == null) return null;
            
            var formatVersion = data["formatVersion"]?.Value<int>() ?? 0;
            if (formatVersion < 1) return null;

            var chart = new Chart
            {
                Offset = data["offset"]?.Value<float>() ?? 0,
                NoteCount = data["numOfNotes"]?.Value<int>() ?? 0,
                JudgeLines = ((JArray) data["judgeLineList"])?.ToObject<List<JudgeLine>>() ?? new List<JudgeLine>()
            };

            foreach (var line in chart.JudgeLines)
            {
                line.ProcessEvents(formatVersion);
            }
            
            chart.ResolveSiblings();
            return chart;
        }

        private void ResolveSiblings()
        {
            foreach (var line in JudgeLines)
            {
                foreach (var n in line.NotesAbove)
                {
                    var t = n.Time;
                    
                    foreach (var l2 in JudgeLines)
                    {
                        foreach (var n2 in l2.NotesAbove.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
                        {
                            n2.HasSibling = true;
                            n.HasSibling = true;
                        }

                        foreach (var n2 in l2.NotesAbove.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
                        {
                            n2.HasSibling = true;
                            n.HasSibling = true;
                        }
                    }
                }
                
                foreach (var n in line.NotesBelow)
                {
                    var t = n.Time;
                    
                    foreach (var l2 in JudgeLines)
                    {
                        foreach (var n2 in l2.NotesAbove.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
                        {
                            n2.HasSibling = true;
                            n.HasSibling = true;
                        }

                        foreach (var n2 in l2.NotesAbove.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
                        {
                            n2.HasSibling = true;
                            n.HasSibling = true;
                        }
                    }
                }
            }
        }
    }
}