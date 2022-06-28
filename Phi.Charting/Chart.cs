using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Phi.Charting
{
    public class Chart
    {
        [JsonPropertyName("offset")]
        public float Offset { get; set; }
        
        [JsonPropertyName("numOfNotes")]
        public int NoteCount { get; set; }
        
        [JsonPropertyName("judgeLineList")]
        public List<JudgeLine> JudgeLines { get; set; }

        public static Chart Deserialize(Stream stream)
        {
            var data = JsonSerializer.Deserialize<JsonObject>(stream);
            if (data == null) return null;
            
            var formatVersion = data["formatVersion"]?.AsValue().GetValue<int>() ?? 0;
            if (formatVersion < 1) return null;

            var chart = new Chart
            {
                Offset = data["offset"]?.AsValue().GetValue<float>() ?? 0,
                NoteCount = data["numOfNotes"]?.AsValue().GetValue<int>() ?? 0,
                JudgeLines = ((JsonArray) data["judgeLineList"])?.Select(n => n.Deserialize<JudgeLine>()).ToList() ?? new List<JudgeLine>()
            };

            foreach (var line in chart.JudgeLines)
            {
                line.ProcessEvents(formatVersion);
            }
            
            chart.ResolveSiblings();
            return chart;
        }

        public void ResolveSiblings()
        {
            foreach (var line in JudgeLines)
            {
                foreach (var n in line.NotesAbove)
                {
                    n.HasSibling = false;
                }
                
                foreach (var n in line.NotesBelow)
                {
                    n.HasSibling = false;
                }
            }
            
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
                        foreach (var n2 in l2.NotesBelow.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
                        {
                            n2.HasSibling = true;
                            n.HasSibling = true;
                        }

                        foreach (var n2 in l2.NotesBelow.Where(n2 => !n2.HasSibling && MathF.Abs(n2.Time - t) < 1 && n2 != n))
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