using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Phi.Charting;

namespace Phi.Viewer.View
{
    public class ChartView : IDisposable
    {
        public Chart Model { get; }
        
        public List<JudgeLineView> JudgeLines { get; }

        public ChartView(Chart model)
        {
            Model = model;
            JudgeLines = model.JudgeLines.Select(line => new JudgeLineView(line)).ToList();
        }

        public static async Task<ChartView> CreateFromModelAsync(Chart model)
        {
            await Task.Yield();
            return new ChartView(model);
        }

        public void Dispose()
        {
        }
    }
}