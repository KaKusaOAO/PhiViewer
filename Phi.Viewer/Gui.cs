using System;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Phi.Charting.Events;
using Phi.Charting.Notes;
using Phi.Viewer.View;
using Veldrid;

namespace Phi.Viewer
{
    public class Gui
    {
        private PhiViewer viewer;
        
        private ImGuiRenderer renderer;

        private Stopwatch _stopwatch = new Stopwatch();

        public Gui(PhiViewer viewer)
        {
            this.viewer = viewer;
            var window = viewer.Host.Window;
            renderer = new ImGuiRenderer(window.GraphicsDevice, 
                window.GraphicsDevice.MainSwapchain.Framebuffer.OutputDescription, 
                (int) window.Width, (int) window.Height);

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
            
            _stopwatch.Start();
        }

        public void Update(InputSnapshot snapshot)
        {
            var delta = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            
            var window = viewer.Host.Window;
            renderer.WindowResized((int) window.Width, (int) window.Height);
            renderer.Update((float) delta, snapshot);
        }

        private object _inspectingObject = null;

        private void DisplayChartComponentWindow()
        {
            if (ImGui.Begin("Chart Components"))
            {
                if (ImGui.BeginMenuBar())
                {
                    if (ImGui.BeginMenu("Edit"))
                    {
                        if (ImGui.MenuItem("Sort Notes by Time"))
                        {
                            foreach (var line in viewer.Chart.JudgeLines)
                            {
                                line.NotesAbove.Sort((a, b) => (int) (a.Model.Time - b.Model.Time));
                                line.NotesBelow.Sort((a, b) => (int) (a.Model.Time - b.Model.Time));
                            }
                        }
                        
                        ImGui.EndMenu();
                    }
                    
                    ImGui.EndMenuBar();
                }
                
                if (ImGui.BeginTable("chart_comp_2col", 2))
                {
                    ImGui.TableSetupColumn("Hierachy");
                    ImGui.TableSetupColumn("Components");
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    
                    if (ImGui.BeginChild("chart_comp_list_child"))
                    {
                        if (ImGui.BeginTable("chart_comp_list", 1,
                                ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersInnerV))
                        {
                            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.NoHide);
                            ImGui.TableHeadersRow();

                            void DisplayNoteDetail(AbstractNoteView note)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();
                                ImGui.PushID(note.GetHashCode());

                                if (ImGui.Selectable($"{note.Model.Type}Note", _inspectingObject == note))
                                {
                                    _inspectingObject = note;
                                }
                                
                                ImGui.PopID();
                            }
                            
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            var chartOpen = ImGui.TreeNodeEx($"{viewer.Chart.GetHashCode()}", 
                                    _inspectingObject == viewer.Chart ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None,
                                "Chart");
                            
                            if (ImGui.IsItemClicked() && ImGui.IsKeyDown(ImGuiKey.ModShift))
                            {
                                _inspectingObject = viewer.Chart;
                            }

                            if (chartOpen)
                            {
                                foreach (var line in viewer.Chart.JudgeLines)
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();
                                    var open = ImGui.TreeNodeEx($"{line.GetHashCode()}", 
                                        _inspectingObject == line ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None,
                                        "JudgeLine");

                                    if (ImGui.IsItemClicked() && ImGui.IsKeyDown(ImGuiKey.ModShift))
                                    {
                                        _inspectingObject = line;
                                    }

                                    if (open)
                                    {
                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();

                                        var aboveOpen = ImGui.TreeNodeEx($"{line.NotesAbove.GetHashCode()}",
                                            ImGuiTreeNodeFlags.None, $"Notes Above: {line.NotesAbove.Count} note(s)");

                                        if (aboveOpen)
                                        {
                                            foreach (var note in line.NotesAbove)
                                            {
                                                DisplayNoteDetail(note);
                                            }

                                            ImGui.TreePop();
                                        }

                                        ImGui.TableNextRow();
                                        ImGui.TableNextColumn();

                                        var belowOpen = ImGui.TreeNodeEx($"{line.NotesBelow.GetHashCode()}",
                                            ImGuiTreeNodeFlags.None, $"Notes Below: {line.NotesBelow.Count} note(s)");

                                        if (belowOpen)
                                        {
                                            foreach (var note in line.NotesBelow)
                                            {
                                                DisplayNoteDetail(note);
                                            }

                                            ImGui.TreePop();
                                        }

                                        ImGui.TreePop();
                                    }
                                }
                                ImGui.TreePop();
                            }
                            
                            ImGui.EndTable();
                        }
                        ImGui.EndChild();
                    }
                    ImGui.TableNextColumn();

                    if (_inspectingObject == null)
                    {
                        ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "<Nothing selected>");
                    } else
                    {
                        if (_inspectingObject is AbstractNoteView note)
                        {
                            ImGui.Text($"{note.Model.Type}Note");
                            
                            if (ImGui.BeginCombo("Note Type", $"{note.Model.Type}Note"))
                            {
                                foreach (var val in Enum.GetValues(typeof(NoteType)))
                                {
                                    var v = (NoteType)val;
                                    if (v == NoteType.Dummy) continue;

                                    if (ImGui.MenuItem($"{v.ToString()}Note"))
                                    {
                                        var model = note.Model;
                                        model.Type = v;

                                        var line = note.Parent;
                                        var container = note.Side == NoteSide.Above ? line.NotesAbove : line.NotesBelow;
                                        var i = container.FindIndex(n => n == note);

                                        if (i >= 0)
                                        {
                                            var newNote = AbstractNoteView.FromModel(line, model, note.Side);
                                            container[i] = newNote;
                                            _inspectingObject = newNote;
                                        }
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            
                            var f = note.Model.Time;
                            ImGui.DragFloat("Note Time", ref f);
                            note.Model.Time = f;

                            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                            {
                                viewer.Chart.Model.ResolveSiblings();
                            }

                            ImGui.BeginDisabled(!(note is HoldNoteView));
                            f = note.Model.HoldTime;
                            ImGui.DragFloat("Hold Time", ref f, 1, 0, float.MaxValue);
                            note.Model.HoldTime = f;
                            ImGui.EndDisabled();
                            
                            f = note.Model.PosX;
                            ImGui.DragFloat("PosX", ref f, 0.01f);
                            note.Model.PosX = f;

                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0, 0, 1));
                            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.5f, 0, 0, 1));
                            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0, 0, 1));
                            if (ImGui.Button("Delete?") && ImGui.IsKeyDown(ImGuiKey.ModShift))
                            {
                                var line = note.Parent;
                                var container = note.Side == NoteSide.Above ? line.NotesAbove : line.NotesBelow;
                                var i = container.FindIndex(n => n == note);

                                if (i >= 0)
                                {
                                    _inspectingObject = null;
                                    container.Remove(note);
                                    (note.Side == NoteSide.Above ? line.Model.NotesAbove : line.Model.NotesBelow)
                                        .Remove(note.Model);
                                }
                            }
                            ImGui.PopStyleColor(3);
                        }
                        else if (_inspectingObject is ChartView chart)
                        {
                            ImGui.Text("Chart");
                            
                            var f = chart.Model.Offset;
                            ImGui.DragFloat("Audio Offset", ref f);
                            chart.Model.Offset = f;

                            if (ImGui.Button("Resolve Siblings"))
                            {
                                chart.Model.ResolveSiblings();
                            }
                        }
                        else if (_inspectingObject is JudgeLineView line)
                        {
                            ImGui.Text("Judge Line");

                            if (ImGui.Button("Add Note to Above"))
                            {
                                var model = new Note {
                                    Type = NoteType.Tap
                                };
                                line.Model.NotesAbove.Add(model);
                                var newNote = AbstractNoteView.FromModel(line, model, NoteSide.Above);
                                line.NotesAbove.Add(newNote);
                                _inspectingObject = newNote;
                            }
                            
                            if (ImGui.Button("Add Note to Below"))
                            {
                                var model = new Note {
                                    Type = NoteType.Tap
                                };
                                line.Model.NotesBelow.Add(model);
                                var newNote = AbstractNoteView.FromModel(line, model, NoteSide.Below);
                                line.NotesBelow.Add(newNote);
                                _inspectingObject = newNote;
                            }
                        } 
                        else
                        {
                            ImGui.Text(_inspectingObject.GetType().FullName);
                        }
                    }
                    
                    ImGui.EndTable();
                }
                
                ImGui.End();
            }
        }

        private void DisplayControlWindow()
        {
            if (ImGui.Begin("Control"))
            {
                var p = viewer.PlaybackTime;
                ImGui.SliderFloat("Playback Time", ref p, 0, viewer.Duration);
                viewer.PlaybackTime = p;
                
                ImGui.SameLine();
                if (ImGui.Button("Play"))
                {
                    viewer.PlaybackStartTime = viewer.MillisSinceLaunch - viewer.PlaybackTime;
                    viewer.IsPlaying = true;
                }
                
                ImGui.SameLine();
                if (ImGui.Button("Stop"))
                {
                    viewer.IsPlaying = false;
                }

                var bl = viewer.ForceRenderOffscreen;
                ImGui.Checkbox("Force Render Offscreen", ref bl);
                viewer.ForceRenderOffscreen = bl;
                
                bl = viewer.UseUniqueSpeed;
                ImGui.Checkbox("Use Unique Speed", ref bl);
                viewer.UseUniqueSpeed = bl;

                bl = viewer.DisableGlobalClip;
                ImGui.Checkbox("Disable Global Clip", ref bl);
                viewer.DisableGlobalClip = bl;
                
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "(?)");

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("You will see notes and lines outside of the screen range.");
                }

                var vec2 = viewer.CanvasTranslate;
                ImGui.DragFloat2("Canvas Translate", ref vec2);
                viewer.CanvasTranslate = vec2;
                
                if (ImGui.Button("Reset Translate"))
                {
                    viewer.CanvasTranslate = Vector2.Zero;
                }

                p = viewer.CanvasScale;
                ImGui.SliderFloat("Canvas Scale", ref p, 0.5f, 2f);
                viewer.CanvasScale = p;

                if (ImGui.Button("Reset Scale"))
                {
                    viewer.CanvasScale = 1;
                }
                
                ImGui.End();
            }
        }

        private float _timelineScale = 8f;
        private JudgeLineView _animInspectorLine;
        private AbstractLineEvent _animInspectorObj;

        private void DisplayAnimationWindow()
        {
            if (ImGui.Begin("Animation"))
            {
                if (ImGui.BeginTable("timeline_2col", 2))
                {
                    ImGui.TableSetupColumn("Timeline");
                    ImGui.TableSetupColumn("Inspector", ImGuiTableColumnFlags.WidthFixed, 250);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    if (ImGui.BeginTable("timeline_table", 2,
                            ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg))
                    {
                        ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthFixed, 100);
                        ImGui.TableSetupColumn("Timeline");
                        ImGui.TableSetupScrollFreeze(1, 3);
                        ImGui.TableHeadersRow();

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();
                        ImGui.PushItemWidth(ImGui.GetWindowWidth() - ImGui.GetCursorPosX() - 5);
                        ImGui.PushID("timeline_scale_id");
                        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetScrollX());
                        ImGui.SliderFloat("", ref _timelineScale, 4, 100, "");
                        ImGui.PopID();
                        ImGui.PopItemWidth();

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.TableNextColumn();

                        var x = ImGui.GetCursorPosX();
                        var y = ImGui.GetCursorPosY();
                        var timelineLength = viewer.Duration;
                        var u = (ImGui.GetWindowWidth() - ImGui.GetCursorPosX()) / timelineLength * _timelineScale;
                        var div = 1500;
                        if (_timelineScale >= 7) div = 1000;
                        if (_timelineScale >= 13) div = 500;
                        if (_timelineScale >= 25) div = 250;
                        if (_timelineScale >= 50) div = 120;
                        if (_timelineScale >= 75) div = 100;
                        if (_timelineScale >= 100) div = 75;
                        for (var i = 0; i < timelineLength; i += div)
                        {
                            ImGui.SetCursorPos(new Vector2(x + i * u, y));
                            ImGui.Text($"{i}");
                        }

                        if (viewer.Chart != null)
                        {
                            foreach (var line in viewer.Chart.JudgeLines)
                            {
                                ImGui.TableNextRow();
                                ImGui.TableNextColumn();

                                var open = ImGui.TreeNodeEx($"{line.GetHashCode()}_Obj", ImGuiTreeNodeFlags.None,
                                    "JudgeLine");

                                ImGui.TableNextColumn();
                                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetScrollX());
                                ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "--");

                                if (open)
                                {
                                    void RenderKeyframe(AbstractLineEvent ev)
                                    {
                                        var time = line.GetRealTimeFromEventTime(ev.StartTime);
                                        if (!ImGui.IsRectVisible(new Vector2(time * u + x, y),
                                                new Vector2(time * u + x + 5, y + 5))) return;
                                        ImGui.GetWindowDrawList().AddRectFilled(
                                            new Vector2(time * u + x, y),
                                            new Vector2(time * u + x + 5, y + 5),
                                            ImGui.ColorConvertFloat4ToU32(
                                                _animInspectorObj == ev
                                                    ? new Vector4(1, 0, 0, 1)
                                                    : new Vector4(1, 1, 1, 1)));

                                        var cx = x - ImGui.GetWindowPos().X + ImGui.GetScrollX();
                                        var cy = y - ImGui.GetWindowPos().Y + ImGui.GetScrollY();
                                        ImGui.SetCursorPos(new Vector2(time * u + cx, cy));
                                        if (ImGui.InvisibleButton($"{ev.GetHashCode()}", new Vector2(5, 5)))
                                        {
                                            _animInspectorLine = line;
                                            _animInspectorObj = ev;
                                        }
                                    }

                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text("Speed");
                                    ImGui.TableNextColumn();

                                    x = ImGui.GetWindowPos().X + ImGui.GetCursorPosX() - ImGui.GetScrollX();
                                    y = ImGui.GetWindowPos().Y + ImGui.GetCursorPosY() - ImGui.GetScrollY() +
                                        ImGui.GetFrameHeight() / 4f;
                                    foreach (var ev in line.Model.SpeedEvents)
                                    {
                                        RenderKeyframe(ev);
                                    }

                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text("Move");
                                    ImGui.TableNextColumn();

                                    x = ImGui.GetWindowPos().X + ImGui.GetCursorPosX() - ImGui.GetScrollX();
                                    y = ImGui.GetWindowPos().Y + ImGui.GetCursorPosY() - ImGui.GetScrollY() +
                                        ImGui.GetFrameHeight() / 4f;
                                    foreach (var ev in line.Model.LineMoveEvents)
                                    {
                                        RenderKeyframe(ev);
                                    }

                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text("Rotate");
                                    ImGui.TableNextColumn();

                                    x = ImGui.GetWindowPos().X + ImGui.GetCursorPosX() - ImGui.GetScrollX();
                                    y = ImGui.GetWindowPos().Y + ImGui.GetCursorPosY() - ImGui.GetScrollY() +
                                        ImGui.GetFrameHeight() / 4f;
                                    foreach (var ev in line.Model.LineRotateEvents)
                                    {
                                        RenderKeyframe(ev);
                                    }

                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    ImGui.Text("Opacity");
                                    ImGui.TableNextColumn();

                                    x = ImGui.GetWindowPos().X + ImGui.GetCursorPosX() - ImGui.GetScrollX();
                                    y = ImGui.GetWindowPos().Y + ImGui.GetCursorPosY() - ImGui.GetScrollY() +
                                        ImGui.GetFrameHeight() / 4f;
                                    foreach (var ev in line.Model.LineFadeEvents)
                                    {
                                        RenderKeyframe(ev);
                                    }

                                    ImGui.TreePop();
                                }
                            }
                        }

                        var drawList = ImGui.GetWindowDrawList();
                        var trackX = ImGui.GetWindowPos().X - ImGui.GetScrollX() + ImGui.GetCursorPosX() +
                                     viewer.PlaybackTime * u;
                        var trackY = ImGui.GetWindowPos().Y + 57;
                        drawList.AddRectFilled(new Vector2(trackX, trackY),
                            new Vector2(trackX + 1, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight()),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));

                        ImGui.PushClipRect(
                            new Vector2(ImGui.GetWindowPos().X, ImGui.GetWindowPos().Y),
                            new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowWidth(), ImGui.GetWindowPos().Y + ImGui.GetWindowHeight() + 20),
                            false);
                        trackX = ImGui.GetWindowPos().X +
                                 viewer.PlaybackTime / timelineLength * (ImGui.GetWindowWidth() - 20);
                        trackY = ImGui.GetWindowPos().Y + ImGui.GetWindowHeight() - 12;
                        drawList.AddRectFilled(new Vector2(trackX, trackY),
                            new Vector2(trackX + 1, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight()),
                            ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));
                        ImGui.PopClipRect();

                        ImGui.EndTable();
                    }

                    ImGui.TableNextColumn();

                    if (_animInspectorObj is RangedBiStateLineEvent bsev)
                    {
                        var vec2 = new Vector2(bsev.Start, bsev.Start2);
                        ImGui.DragFloat2("Start Components", ref vec2);
                        bsev.Start = vec2.X;
                        bsev.Start2 = vec2.Y;

                        vec2 = new Vector2(bsev.End, bsev.End2);
                        ImGui.DragFloat2("End Components", ref vec2);
                        bsev.End = vec2.X;
                        bsev.End2 = vec2.Y;
                    }
                    else if (_animInspectorObj is SpeedEvent sp)
                    {
                        var f = sp.Value;
                        ImGui.DragFloat("Speed", ref f);
                        sp.Value = f;

                        if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                        {
                            _animInspectorLine.ClearMeter();
                        }
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "Event not selected");
                    }

                    ImGui.EndTable();
                }

                ImGui.End();
            }
        }
        
        public void Render(GraphicsDevice device, CommandList list)
        {
            list.SetFramebuffer(device.SwapchainFramebuffer);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open Folder..."))
                    {
                        ImGui.OpenPopup("Yr", ImGuiPopupFlags.MouseButtonLeft);
                    }
                    
                    ImGui.EndMenu();
                }
                
                ImGui.EndMainMenuBar();
            }

            DisplayControlWindow();
            DisplayChartComponentWindow();
            DisplayAnimationWindow();
            
            ImGui.ShowDemoWindow();
            renderer.Render(device, list);
        }
    }
}