using System.Collections.Generic;
using System.Linq;
using Phi.Charting;

namespace Phi.Viewer.View
{
    public class ChartView
    {
        public Chart Model { get; }
        
        public List<JudgeLineView> JudgeLines { get; }

        public ChartView(Chart model)
        {
            Model = model;
            JudgeLines = model.JudgeLines.Select(line => new JudgeLineView(line)).ToList();
        }
    }
}