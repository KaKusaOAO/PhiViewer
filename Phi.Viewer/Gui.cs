using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ImGuiNET;
using KaLib.Utils;
using ManagedBass;
using Newtonsoft.Json;
using Phi.Charting;
using Phi.Charting.Events;
using Phi.Charting.Notes;
using Phi.Viewer.Graphics;
using Phi.Viewer.Utils;
using Phi.Viewer.View;
using Veldrid;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Phi.Viewer
{
    public class Gui
    {
        private PhiViewer _viewer;
        private BetterImGuiRenderer _renderer;
        private Stopwatch _stopwatch = new Stopwatch();
        private List<ChartEntry> _charts = new List<ChartEntry>();
        private bool _loadingChart = false;

        public Gui(PhiViewer viewer)
        {
            this._viewer = viewer;
            var window = viewer.Host.Window;
            _renderer = new BetterImGuiRenderer(window.GraphicsDevice,
                window.GraphicsDevice.MainSwapchain.Framebuffer.OutputDescription,
                (int) window.Width, (int) window.Height);

            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;

            // Reload the .ini to activate docking here
            ImGui.LoadIniSettingsFromDisk("imgui.ini");

            _stopwatch.Start();
            LoadChartTable();
        }

        private void LoadChartTable()
        {
            _charts.Clear();

            try
            {
                _charts.AddRange(
                    JsonSerializer.Deserialize<List<ChartEntry>>(
                        File.ReadAllText(@"D:\AppServ\www\phigros\assets\charts\db.json")) ?? new List<ChartEntry>());
            }
            catch (IOException)
            {
                // Don't panic
            }
        }

        public void Update(InputSnapshot snapshot)
        {
            var delta = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();

            var window = _viewer.Host.Window;
            _renderer.WindowResized((int) window.Width, (int) window.Height);
            _renderer.EnhancedUpdate((float) delta, snapshot);
        }

        private object _inspectingObject = null;

        private void DisplayChartComponentContent()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.MenuItem("Sort Notes by Time"))
                    {
                        foreach (var line in _viewer.Chart.JudgeLines)
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

                        var chartOpen = ImGui.TreeNodeEx($"{_viewer.Chart.GetHashCode()}",
                            _inspectingObject == _viewer.Chart ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None,
                            "Chart");

                        if (ImGui.IsItemClicked() && ImGui.IsKeyDown(ImGuiKey.ModShift))
                        {
                            _inspectingObject = _viewer.Chart;
                        }

                        if (chartOpen)
                        {
                            foreach (var line in _viewer.Chart.JudgeLines)
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
                }
                else
                {
                    if (_inspectingObject is AbstractNoteView note)
                    {
                        note.IsInspectorHighlightedOnNextDraw = true;
                        ImGui.Text($"{note.Model.Type}Note");

                        if (ImGui.BeginCombo("Note Type", $"{note.Model.Type}Note"))
                        {
                            foreach (var val in Enum.GetValues(typeof(NoteType)))
                            {
                                var v = (NoteType) val;
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
                            _viewer.Chart.Model.ResolveSiblings();
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
                            var model = new Note
                            {
                                Type = NoteType.Tap
                            };
                            line.Model.NotesAbove.Add(model);
                            var newNote = AbstractNoteView.FromModel(line, model, NoteSide.Above);
                            line.NotesAbove.Add(newNote);
                            _inspectingObject = newNote;
                        }

                        if (ImGui.Button("Add Note to Below"))
                        {
                            var model = new Note
                            {
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
        }

        private void DisplayViewerContent()
        {
            var r = _viewer.Renderer;
            var tex = r.ResolvedRenderTargetTexture;
            var img = _renderer.GetOrCreateImGuiBinding(r.Factory, tex);

            var size = new Vector2(
                ImGui.GetWindowWidth(),
                (float)tex.Height / tex.Width * ImGui.GetWindowWidth()
            );
            var maxHeight = ImGui.GetWindowHeight() - ImGui.GetFrameHeight() * 1.5f;
            if (size.Y > maxHeight)
            {
                size /= size.Y / maxHeight;
            }

            var padX = (ImGui.GetWindowWidth() - size.X) / 2;
            var padY = (ImGui.GetWindowHeight() + ImGui.GetFrameHeight() - size.Y) / 2;
            var (l ,h) = (0.001f, 0.999f);
            var uv0 = new Vector2(l, r.GraphicsDevice.BackendType == GraphicsBackend.OpenGL ? h : l);
            var uv1 = new Vector2(h, r.GraphicsDevice.BackendType == GraphicsBackend.OpenGL ? l : h);
            ImGui.SetCursorPos(new Vector2(padX, padY));
            ImGui.Image(img, size, uv0, uv1);
        }

        private void DisplayControlContent()
        {

            var p = _viewer.PlaybackTime;
            ImGui.PushItemWidth(ImGui.GetWindowWidth() - 20);
            ImGui.PushID("playback_time");
            ImGui.SliderFloat("", ref p, 0, _viewer.MusicPlayer.Duration);
            if (ImGui.IsItemEdited())
            {
                _viewer.MusicPlayer.Seek(p);
                _viewer.PlaybackTime = p;
            }
            ImGui.PopID();
            ImGui.PopItemWidth();

            if (ImGui.Button(_viewer.IsPlaying ? "Pause" : "Play"))
            {
                _viewer.IsPlaying = !_viewer.IsPlaying;
                if (_viewer.IsPlaying)
                {
                    _viewer.MusicPlayer.Play(_viewer.MusicPlayer.PlaybackTime);
                }
                else
                {
                    _viewer.MusicPlayer.Stop();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Stop"))
            {
                _viewer.IsPlaying = false;
                _viewer.MusicPlayer.Stop();
                _viewer.MusicPlayer.Seek(0);
                _viewer.PlaybackTime = 0;
            }
            
            ImGui.SameLine();
            var bl = _viewer.IsLoopEnabled;
            ImGui.Checkbox("Loop", ref bl);
            _viewer.IsLoopEnabled = bl;
            
            ImGui.SameLine();
            p = _viewer.MusicPlayer.Volume;
            ImGui.PushID("playback_vol");
            ImGui.PushItemWidth(100);
            ImGui.SliderFloat("Volume", ref p, 0, 1);
            ImGui.PopItemWidth();
            if (ImGui.IsItemEdited())
            {
                _viewer.MusicPlayer.Volume = p;
            }
            ImGui.PopID();
        }

        private void DisplayChartTableContent()
        {
            if (ImGui.Button("Reload"))
            {
                LoadChartTable();
            }
            
            if (ImGui.BeginTable("_chartList", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders))
            {
                ImGui.TableSetupColumn("Title");
                ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 100);
                ImGui.TableHeadersRow();

                foreach (var entry in _charts)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.BeginGroup();
                    ImGui.Dummy(new Vector2(0, 0));
                    ImGui.Text(entry.Name + (entry.IsLegacy ? " (Legacy)" : ""));
                    ImGui.Dummy(new Vector2(0, 0));
                    ImGui.EndGroup();
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1f, 1f, 1f, 0.5f));
                    ImGui.BeginDisabled(true);
                    var lvl = entry.Difficulty.Level < 0 ? "?" : $"{entry.Difficulty.Level}";
                    ImGui.Button($"{entry.Difficulty.Type} Lv.{lvl}");
                    ImGui.EndDisabled();
                    ImGui.PopStyleColor(1);
                    
                    ImGui.TableNextColumn();
                    ImGui.PushID(entry.GetHashCode() + (entry.IsLegacy ? "L" : "M"));
                    ImGui.BeginDisabled(_loadingChart);
                    if (ImGui.Button("Load"))
                    {
                        _loadingChart = true;
                        Task.Run(async () =>
                        {
                            var pathBase = @"D:\AppServ\www\phigros\";
                            try
                            {
                                Logger.Verbose(@$"Loading chart from {pathBase}{entry.ChartPath}");
                                await using var stream = new FileStream(@$"{pathBase}{entry.ChartPath}", FileMode.Open);
                                Logger.Verbose("Deserializing JSON...");
                                var model = Chart.Deserialize(stream);
                                Logger.Verbose("Processing deserialized chart model...");
                                var chart = await ChartView.CreateFromModelAsync(model);
                                Logger.Verbose("Done!");

                                _viewer.ActionQueue.Enqueue(() =>
                                {
                                    Logger.Verbose("Changing the chart...");
                                    _viewer.Chart = chart;

                                    _viewer.Background?.Dispose();
                                    _viewer.Background =
                                        ImageLoader.LoadTextureFromPath(@$"{pathBase}{entry.BackgroundPath}");

                                    _viewer.MusicPlayer.Stop();
                                    _viewer.MusicPlayer.Seek(0);
                                    _viewer.IsPlaying = false;
                                    _viewer.PlaybackTime = 0;

                                    _viewer.MusicPlayer.Dispose();
                                    _viewer.MusicPlayer.LoadFromPath(@$"{pathBase}{entry.AudioPath}");

                                    _viewer.SongTitle = entry.Name;
                                    _viewer.DiffName = entry.Difficulty.Type.ToString();
                                    _viewer.DiffLevel = entry.Difficulty.Level;

                                    GC.Collect();
                                    _loadingChart = false;
                                });
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Failed! {ex}");
                                _loadingChart = false;
                            }
                        });
                    }
                    ImGui.EndDisabled();
                    ImGui.PopID();
                }
                
                ImGui.EndTable();
            }
        }

        private float _timelineScale = 8f;
        private JudgeLineView _animInspectorLine;
        private AbstractLineEvent _animInspectorObj;
        private bool _animScrollLock = false;

        private void DisplayAnimationContent()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("Test"))
                {
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }

            if (ImGui.BeginTable("timeline_2col", 2))
            {
                ImGui.TableSetupColumn("Timeline");
                ImGui.TableSetupColumn("Inspector", ImGuiTableColumnFlags.WidthFixed, 250);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                if (ImGui.BeginTable("timeline_table", 2,
                        ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY |
                        ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Object", ImGuiTableColumnFlags.WidthFixed, 185);
                    ImGui.TableSetupColumn("Timeline");
                    ImGui.TableSetupScrollFreeze(1, 3);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Objects");
                    ImGui.TableNextColumn();

                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetScrollX());
                    ImGui.Checkbox("Scroll Lock", ref _animScrollLock);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TableNextColumn();
                    ImGui.PushItemWidth(ImGui.GetWindowWidth() - ImGui.GetCursorPosX() - 20);
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
                    var timelineLength = _viewer.MusicPlayer.Duration;
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

                    if (_viewer.Chart != null)
                    {
                        foreach (var line in _viewer.Chart.JudgeLines)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableNextColumn();

                            var open = ImGui.TreeNodeEx($"{line.GetHashCode()}_Obj", ImGuiTreeNodeFlags.None,
                                $"JudgeLine @ 0x{line.GetHashCode():x8}");

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

                    var scrollX = ImGui.GetScrollX();
                    if (_animScrollLock && _viewer.IsPlaying)
                    {
                        scrollX = Math.Min((ImGui.GetWindowWidth() - ImGui.GetCursorPosX()) * (_timelineScale - 1),
                            Math.Max(0, _viewer.PlaybackTime * u - 50));
                        ImGui.SetScrollX(scrollX);
                    }

                    var drawList = ImGui.GetWindowDrawList();
                    var trackX = ImGui.GetWindowPos().X - scrollX + ImGui.GetCursorPosX() +
                                 _viewer.PlaybackTime * u;
                    var trackY = ImGui.GetWindowPos().Y + 57;
                    drawList.AddRectFilled(new Vector2(trackX, trackY),
                        new Vector2(trackX + 1, ImGui.GetWindowPos().Y + ImGui.GetWindowHeight()),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)));

                    ImGui.PushClipRect(
                        new Vector2(ImGui.GetWindowPos().X, ImGui.GetWindowPos().Y),
                        new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowWidth(),
                            ImGui.GetWindowPos().Y + ImGui.GetWindowHeight() + 20),
                        false);
                    trackX = ImGui.GetWindowPos().X +
                             _viewer.PlaybackTime / timelineLength * (ImGui.GetWindowWidth() - 20);
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
        }

        private void DisplayMetricsContent()
        {
            if (ImGui.CollapsingHeader("Audio"))
            {
                if (ImGui.BeginTable("_audioMetrics", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Version");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{Bass.Version}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Latency");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{Bass.Info.Latency}ms");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Handles");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{Bass.HandleCount}");
                    
                    ImGui.EndTable();
                }
            }

            if (ImGui.CollapsingHeader("Renderer"))
            {
                var m = _viewer.Renderer.Metrics;
                
                if (ImGui.BeginTable("_rendererMetrics", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("FPS");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{ImGui.GetIO().Framerate:F2}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Vertices");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{m.VerticesCount}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Indices");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"I: {m.IndicesCount}, T: {m.IndicesCount / 3}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Mesh count");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{m.MeshCount}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Cached glyphs");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{_viewer.Renderer.CachedFontGlyphs}");
                    
                    ImGui.EndTable();
                }

                if (ImGui.Button("Clear cached font glyphs"))
                {
                    _viewer.Renderer.ClearFontCache();
                }
            }
            
            if (ImGui.CollapsingHeader("Chart"))
            {
                var c = _viewer.Chart;

                if (ImGui.BeginTable("_chartMetrics", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Offset");
                    ImGui.TableNextColumn();

                    var p = c.Model.Offset;
                    ImGui.PushID("_modelOffset");
                    ImGui.DragFloat("", ref p, 0.001f);
                    ImGui.PopID();
                    c.Model.Offset = p;
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Lines");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"V: {c.JudgeLines.Count} / M: {c.Model.JudgeLines.Count}");
                    
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text("Notes");
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"V: {c.JudgeLines.Sum(v => v.NotesAbove.Count + v.NotesBelow.Count)} / " +
                                      $"M: {c.Model.JudgeLines.Sum(v => v.NotesAbove.Count + v.NotesBelow.Count)}");

                    ImGui.EndTable();
                }
            }
            
            if (ImGui.CollapsingHeader("Viewer"))
            {
                var v = _viewer;

                if (ImGui.Button("Spawn Judge Effect Particle"))
                {
                    var cw = v.WindowSize.Width;
                    var ch = v.WindowSize.Height;
                    
                    v.AnimatedObjects.Add(new JudgeEffect(cw / 2, ch / 2, 10));
                }

                if (ImGui.BeginTable("_viewerMetrics", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.PushID("_animObjectsViewMetrics");
                    var open = ImGui.TreeNodeEx("Animated Objects");
                    ImGui.PopID();
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped($"{v.AnimatedObjects.Count}");

                    if (open)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text("JudgeEffects");
                        ImGui.TableNextColumn();
                        ImGui.TextWrapped($"{v.AnimatedObjects.Count(n => n is JudgeEffect)}");
                        
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text("OneShotAudio");
                        ImGui.TableNextColumn();
                        ImGui.TextWrapped($"{v.AnimatedObjects.Count(n => n is OneShotAudio)}");
                        
                        ImGui.TreePop();
                    }

                    ImGui.EndTable();
                }
            }
        }

        private void DisplaySettingsContent()
        {
            ImGui.PushItemWidth(MathF.Min(225, ImGui.GetWindowWidth() * 0.5f));
            
            if (ImGui.CollapsingHeader("Graphics"))
            {
                var bl = _viewer.ForceRenderOffscreen;
                ImGui.Checkbox("Force Render Offscreen", ref bl);
                _viewer.ForceRenderOffscreen = bl;

                bl = _viewer.UseUniqueSpeed;
                ImGui.Checkbox("Use Unique Speed", ref bl);
                _viewer.UseUniqueSpeed = bl;
                
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If enabled, speed events won't take affect.");
                }
                
                bl = _viewer.DisableGlobalClip;
                ImGui.Checkbox("Disable Global Clip", ref bl);
                _viewer.DisableGlobalClip = bl;

                ImGui.SameLine();
                ImGui.TextColored(new Vector4(1, 1, 1, 0.5f), "(?)");
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If enabled, you will see notes and lines outside of the screen range.");
                }
                
                var vec2 = _viewer.CanvasTranslate;
                ImGui.DragFloat2("Canvas Translate", ref vec2);
                _viewer.CanvasTranslate = vec2;

                var p = _viewer.CanvasScale;
                ImGui.SliderFloat("Canvas Scale", ref p, 0.5f, 2f);
                _viewer.CanvasScale = p;

                if (ImGui.Button("Reset Translate"))
                {
                    _viewer.CanvasTranslate = Vector2.Zero;
                }

                ImGui.SameLine();

                if (ImGui.Button("Reset Scale"))
                {
                    _viewer.CanvasScale = 1;
                }

                p = _viewer.BackgroundDim;
                ImGui.SliderFloat("Background Dim", ref p, 0, 1);
                _viewer.BackgroundDim = p;
                
                p = _viewer.BackgroundBlur;
                ImGui.SliderFloat("Background Blur", ref p, 0, 20);
                _viewer.BackgroundBlur = p;
                
                bl = _viewer.EnableParticles;
                ImGui.Checkbox("Enable Click Particle", ref bl);
                _viewer.EnableParticles = bl;
            }

            if (ImGui.CollapsingHeader("Audio"))
            {
                var p = _viewer.MusicPlayer.PlaybackRate;
                ImGui.SliderFloat("Playback Rate", ref p, 0.5f, 2f);
                if (ImGui.IsItemEdited())
                {
                    _viewer.MusicPlayer.PlaybackRate = p;
                }

                if (_viewer.Chart.JudgeLines.Any())
                {
                    p = _viewer.MusicPlayer.PlaybackRate * _viewer.Chart.JudgeLines[0].Model.Bpm;
                    ImGui.DragFloat("Playback Rate (BPM)", ref p, 0.5f, 20, 512);
                    if (ImGui.IsItemEdited())
                    {
                        _viewer.MusicPlayer.PlaybackRate = p / _viewer.Chart.JudgeLines[0].Model.Bpm;
                    }
                }

                var i = (int) _viewer.MusicPlayer.PlaybackPitch;
                ImGui.SliderInt("Playback Pitch (int)", ref i, -12, 12);
                if (ImGui.IsItemEdited())
                {
                    _viewer.MusicPlayer.PlaybackPitch = i;
                }

                p = _viewer.MusicPlayer.PlaybackPitch;
                ImGui.SliderFloat("Playback Pitch (float)", ref p, -12, 12);
                if (ImGui.IsItemEdited())
                {
                    _viewer.MusicPlayer.PlaybackPitch = p;
                }

                if (ImGui.Button("Reset Rate"))
                {
                    _viewer.MusicPlayer.PlaybackRate = 1;
                }

                ImGui.SameLine();

                if (ImGui.Button("Reset Pitch"))
                {
                    _viewer.MusicPlayer.PlaybackPitch = 0;
                }

                ImGui.SameLine();

                var bl = _viewer.MusicPlayer.SyncSpeedAndPitch;
                ImGui.Checkbox("Sync Speed & Pitch", ref bl);
                _viewer.MusicPlayer.SyncSpeedAndPitch = bl;
                
                bl = _viewer.EnableClickSound;
                ImGui.Checkbox("Enable Click FX", ref bl);
                _viewer.EnableClickSound = bl;
            }

            if (ImGui.CollapsingHeader("Viewer"))
            {
                var str = _viewer.SongTitle ?? "<null>";
                ImGui.InputText("Song Title", ref str, 256);
                _viewer.SongTitle = str;
                
                str = _viewer.DiffName ?? "<null>";
                ImGui.InputText("Difficulty Name", ref str, 64);
                _viewer.DiffName = str;

                var i = _viewer.DiffLevel;
                ImGui.SliderInt("Difficulty", ref i, -1, 32);
                _viewer.DiffLevel = i;

                var p = _viewer.MaxRatio;
                ImGui.SliderFloat("Playfield Max Aspect", ref p, 0.5f, 2f);
                _viewer.MaxRatio = p;

                if (ImGui.Button("4:3")) _viewer.MaxRatio = 4f / 3f;
                ImGui.SameLine();
                if (ImGui.Button("16:9")) _viewer.MaxRatio = 16f / 9f;
            }
            
            ImGui.PopItemWidth();
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
            
            if (ImGui.Begin("Chart Component"))
            {
                DisplayChartComponentContent();
                ImGui.End();
            }
            
            if (ImGui.Begin("Animation"))
            {
                DisplayAnimationContent();
                ImGui.End();
            }
            
            if (ImGui.Begin("Control"))
            {
                DisplayControlContent();
                ImGui.End();
            }

            if (ImGui.Begin("Metrics"))
            {
                DisplayMetricsContent();
                ImGui.End();
            }

            if (ImGui.Begin("Settings"))
            {
                DisplaySettingsContent();
                ImGui.End();
            }

            if (ImGui.Begin("Viewer"))
            {
                DisplayViewerContent();
                ImGui.End();
            }
            
            if (ImGui.Begin("Chart Database"))
            {
                DisplayChartTableContent();
                ImGui.End();
            }
            
            ImGui.ShowDemoWindow();
            _renderer.Render(device, list);
        }

        private struct ChartEntry
        {
            public struct DifficultyDescriptor
            {
                [JsonPropertyName("type")]
                public ChartDifficulty Type { get; set; }
                
                [JsonPropertyName("level")]
                public int Level { get; set; }
            }
            
            [JsonPropertyName("name")]
            public string Name { get; set; }
            
            [JsonPropertyName("difficulty")]
            public DifficultyDescriptor Difficulty { get; set; }
            
            [JsonPropertyName("audio")]
            public string AudioPath { get; set; }
            
            [JsonPropertyName("chart")]
            public string ChartPath { get; set; }
            
            [JsonPropertyName("bg")]
            public string BackgroundPath { get; set; }
            
            [JsonPropertyName("legacy")]
            public bool IsLegacy { get; set; }
            
            [JsonPropertyName("Source")]
            public string Source { get; set; }
        }
    }
}