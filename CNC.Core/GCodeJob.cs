using CNC.Core.Geometry;
/*
 * GCodeJob.cs - part of CNC Controls library
 *
 * v0.47 / 2026-02-13 / Io Engineering (Terje Io)
 *
 */

/*

Copyright (c) 2018-2026, Io Engineering (Terje Io)
All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

� Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

� Redistributions in binary form must reproduce the above copyright notice, this
list of conditions and the following disclaimer in the documentation and/or
other materials provided with the distribution.

� Neither the name of the copyright holder nor the names of its contributors may
be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Diagnostics;
using CNC.GCode;

namespace CNC.Core
{
    public enum Action
    {
        New,
        Add,
        End
    }

    public class GCodeBlock : ViewModelBase
    {
        private bool _break;
        private string _data, _sent = string.Empty;

        public GCodeBlock(uint lineNum, string block, int length, bool isComment, bool programEnd, int row)
        {
            LineNum = lineNum;
            Data = block;
            Length = length;
            IsComment = isComment;
            ProgramEnd = programEnd;
            Row = row;
        }

        public uint LineNum { get; set; }
        /// <summary>1-based row index in the program list (for display).</summary>
        public int Row { get; }
        public int Length { get; set; }
        public string Data { get { return _data; } set { _data = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayData)); } }

        /// <summary>G-code text without the leading program line number prefix (N�).</summary>
        public string DisplayData => StripLineNumberPrefix(_data, LineNum);
        public string Sent {
            get { return _sent; }
            set { _sent = BreakAt ? "BRK " + value : value; OnPropertyChanged(); }
        }
        public bool File { get; set; }
        public bool IsComment { get; set; }
        public bool BreakAt
        {
            get { return _break; }
            set {
                _break = value;
                Sent = _sent.Replace("BRK ", string.Empty);
                Length += _break ? 3 : -3;
            }
        }
        public bool ProgramEnd { get; set; }
        public bool Ok { get; set; }

        static string StripLineNumberPrefix(string data, uint lineNum)
        {
            if (string.IsNullOrEmpty(data) || data[0] != 'N')
                return data;

            var i = 1;
            while (i < data.Length && char.IsDigit(data[i]))
                i++;

            if (i <= 1 || !uint.TryParse(data.AsSpan(1, i - 1), out var parsed) || parsed != lineNum)
                return data;

            return data.Substring(i);
        }
    }

    public class GCodeJob
    {
        uint LineNumber = 1;

        private string filename = string.Empty;
        public ObservableCollection<GCodeBlock> blocks = new ObservableCollection<GCodeBlock>();

        public Queue<string> commands = new Queue<string>();

        public delegate bool ToolChangedHandler(int toolNumber);
        public event ToolChangedHandler ToolChanged = null;

        public delegate void FileChangedHandler(string filename);
        public event FileChangedHandler FileChanged = null;

        public GCodeJob()
        {
            Reset();

            Parser.ToolChanged += Parser_ToolChanged;
        }

        private bool Parser_ToolChanged(int toolNumber)
        {
            return ToolChanged == null ? true : ToolChanged(toolNumber);
        }

        public ObservableCollection<GCodeBlock> Blocks { get { return blocks; } }
        public bool Loaded { get { return blocks.Count > 0; } }
        public bool HeightMapApplied { get; set; }

        public List<GCodeToken> Tokens { get { return Parser.Tokens; } }
        public GcodeBoundingBox BoundingBox { get; private set; } = new GcodeBoundingBox();
        public GCodeParser Parser { get; private set; } = new GCodeParser();

        public double min_feed { get; private set; }
        public double max_feed { get; private set; }

        public bool LoadFile(string filename, bool addLineNumber = false, bool raiseFileChanged = true)
        {
            bool ok = true, isComment;
            uint ln;

            FileInfo file = new FileInfo(filename);
            var staged = new List<GCodeBlock>(EstimateBlockCapacity(file.Length));

            Reset();
            commands.Clear();
            this.filename = filename;

            var loadWatch = Stopwatch.StartNew();
            if (TryLoadFast(file, staged, addLineNumber))
            {
#if DEBUG
                var swapWatch = Stopwatch.StartNew();
#endif
                ReplaceBlocks(staged);
                BoundingBox.Conclude();
#if DEBUG
                swapWatch.Stop();
#endif
                NotifyFileChanged(filename, raiseFileChanged);
#if DEBUG
                Trace.WriteLine($"G-code fast load completed in {loadWatch.ElapsedMilliseconds} ms; swap={swapWatch.ElapsedMilliseconds} ms: {filename}");
#endif
                return true;
            }

#if DEBUG
            Trace.WriteLine($"G-code fast load rejected after {loadWatch.ElapsedMilliseconds} ms: {filename}");
            var fallbackWatch = Stopwatch.StartNew();
#endif
            Reset();
            commands.Clear();
            staged.Clear();
            this.filename = filename;

            using StreamReader sr = file.OpenText();

            string? block = sr.ReadLine();

            while (block != null)
            {
                try
                {
                    block = block.Trim();
                    var tokenStart = Parser.Tokens.Count;
                    if (Parser.ParseBlock(ref block, false, out ln, out isComment))
                    {
                        if (ln > 0)
                        {
                            LineNumber = ln;
                            addLineNumber = false;
                        }
                        else if (addLineNumber)
                        {
                            LineNumber += 10;
                            block = "N" + LineNumber.ToString() + block;
                        }
                        else
                            LineNumber++;

                        SetParsedTokenLineNumbers(tokenStart, LineNumber);
                        staged.Add(new GCodeBlock(LineNumber, block, block.Length + 1, isComment, Parser.ProgramEnd, staged.Count + 1));
                        while (commands.Count > 0)
                        {
                            block = commands.Dequeue();
                            LineNumber++;
                            if (addLineNumber)
                                block = "N" + (LineNumber).ToString() + block;
                            staged.Add(new GCodeBlock(LineNumber, block, block.Length + 1, false, false, staged.Count + 1));
                        }
                    }

                    block = sr.ReadLine();
                }
                catch (Exception e)
                {
                    if ((ok = GrblUi.AskYesNo(string.Format(LibStrings.FindResource("LoadError").Replace("\\n", "\r"), e.Message, LineNumber, block), "ioSender")))
                        block = sr.ReadLine();
                    else
                        block = null;
                }
            }

            if (ok)
            {
#if DEBUG
                fallbackWatch.Stop();
                var swapWatch = Stopwatch.StartNew();
#endif
                ReplaceBlocks(staged);
#if DEBUG
                swapWatch.Stop();
                Trace.WriteLine($"G-code fallback parse completed in {fallbackWatch.ElapsedMilliseconds} ms; swap={swapWatch.ElapsedMilliseconds} ms: {filename}");
#endif
                FinalizeLoadedProgram(filename, raiseFileChanged);
            }
            else
            {
                CloseFile();
            }

            return ok;
        }

        bool TryLoadFast(FileInfo file, List<GCodeBlock> staged, bool addLineNumber)
        {
            if (GrblInfo.LatheUVWModeEnabled)
                return false;

            using var sr = file.OpenText();
            var fast = new NativeFastLoad(Parser, BoundingBox);
            string? block;

            while ((block = sr.ReadLine()) != null)
            {
                block = block.Trim();
                if (!fast.TryParseBlock(block, staged.Count + 1, ref LineNumber, ref addLineNumber, out var parsed))
                    return false;

                if (parsed != null)
                    staged.Add(parsed);

                if (fast.ProgramEnd)
                    break;
            }

            return true;
        }

        void ReplaceBlocks(IReadOnlyList<GCodeBlock> staged)
        {
            blocks = staged is List<GCodeBlock> list
                ? new ObservableCollection<GCodeBlock>(list)
                : new ObservableCollection<GCodeBlock>(staged);
        }

        static int EstimateBlockCapacity(long fileLength)
        {
            if (fileLength <= 0)
                return 0;

            var estimate = fileLength / 32;
            if (estimate > int.MaxValue)
                return int.MaxValue;
            return Math.Max(1024, (int)estimate);
        }

        void FinalizeLoadedProgram(string filename, bool raiseFileChanged)
        {
            try
            {
#if DEBUG
                var boundsWatch = Stopwatch.StartNew();
#endif
                BoundingBox.Reset();
                GCodeEmulator emu = new GCodeEmulator(true, syncMachineState: false);

                foreach (var cmd in emu.Execute(Tokens))
                {
                    if (cmd.Token is GCArc)
                        BoundingBox.AddBoundingBox((cmd.Token as GCArc).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCCubicSpline)
                        BoundingBox.AddBoundingBox((cmd.Token as GCCubicSpline).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCQuadraticSpline)
                        BoundingBox.AddBoundingBox((cmd.Token as GCQuadraticSpline).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                    else if (cmd.Token is GCAxisCommand9)
                    {
                        if (GrblInfo.LatheUVWModeEnabled)
                            BoundingBox.AddBoundingBox((cmd.Token as GCAxisCommand9).GetBoundingBox(emu.Plane, new double[] { cmd.Start.X, cmd.Start.Y, cmd.Start.Z }, emu.DistanceMode == DistanceMode.Incremental));
                        else
                            BoundingBox.AddPoint(cmd.End, (cmd.Token as GCAxisCommand9).AxisFlags);
                    }
                }

                BoundingBox.Conclude();
#if DEBUG
                boundsWatch.Stop();
                Trace.WriteLine($"G-code bounds completed in {boundsWatch.ElapsedMilliseconds} ms; tokens={Tokens.Count}: {filename}");
#endif
            }
            catch (Exception ex)
            {
                GrblUi.ShowError($"Could not compute program bounds: {ex.Message}", "ioSender");
            }

            NotifyFileChanged(filename, raiseFileChanged);
        }

        void NotifyFileChanged(string filename, bool raiseFileChanged)
        {
            if (raiseFileChanged)
                FileChanged?.Invoke(filename);
        }

        public void AddBlock(string block, Action action)
        {
            if (action == Action.New)
            {
                if (Loaded)
                    blocks.Clear();

                Reset();
                commands.Clear();

                filename = block;

            }
            else if (block != null && block.Trim().Length > 0) try
            {
                bool isComment;
                uint ln;

                block = block.Trim();
                var tokenStart = Parser.Tokens.Count;
                if (Parser.ParseBlock(ref block, false, out ln, out isComment))
                {
                    if(GrblInfo.UseLinenumbers)
                    {
                        LineNumber += 10;
                        block = "N" + LineNumber.ToString() + block;
                    } else
                        LineNumber++;
 
                    SetParsedTokenLineNumbers(tokenStart, LineNumber);
                    blocks.Add(new GCodeBlock(LineNumber, block, block.Length + 1, isComment, Parser.ProgramEnd, blocks.Count + 1));
                    while (commands.Count > 0)
                    {
                        block = commands.Dequeue();
                        LineNumber++;
                        if (GrblInfo.UseLinenumbers)
                            block = "N" + (LineNumber).ToString() + block;
                        blocks.Add(new GCodeBlock(LineNumber, block, block.Length + 1, false, false, blocks.Count + 1));
                    }
                }
            }
            catch //(Exception e)
            {
                // 
            }

            if (action == Action.End)
                FinalizeLoadedProgram(filename, true);
        }

        public void AddBlock(string block)
        {
            AddBlock(block, Action.Add);
        }

        void SetParsedTokenLineNumbers(int tokenStart, uint lineNumber)
        {
            for (var i = tokenStart; i < Parser.Tokens.Count; i++)
                Parser.Tokens[i].LineNumber = lineNumber;
        }

        public void CloseFile()
        {
            if (Loaded)
                blocks.Clear();

            commands.Clear();

            Reset();

            filename = "";

            FileChanged?.Invoke(filename);
        }

        private void Reset()
        {
            min_feed = double.MaxValue;
            max_feed = double.MinValue;
            BoundingBox.Reset();
            LineNumber = 0;
            HeightMapApplied = false;
            Parser.Reset();
        }
    }

    sealed class NativeFastLoad
    {
        readonly GCodeParser parser;
        readonly GcodeBoundingBox bounds;
        readonly double[] machine = new double[9];
        readonly GCPlane[] planes =
        [
            new GCPlane(Commands.G17, 0, false),
            new GCPlane(Commands.G18, 0, false),
            new GCPlane(Commands.G19, 0, false)
        ];

        Commands motion = Commands.Undefined;
        GCPlane plane;
        DistanceMode distanceMode = DistanceMode.Absolute;
        IJKMode ijkMode = IJKMode.Incremental;
        bool imperial;
        double feedrate;

        public NativeFastLoad(GCodeParser parser, GcodeBoundingBox bounds)
        {
            this.parser = parser;
            this.bounds = bounds;
            plane = planes[0];
        }

        public bool ProgramEnd { get; private set; }

        public bool TryParseBlock(string raw, int row, ref uint lineNumber, ref bool addLineNumber, out GCodeBlock? parsed)
        {
            parsed = null;
            if (ProgramEnd)
                return true;

            var block = NormalizeBlock(raw);
            if (block.Length == 0)
                return true;

            var isComment = IsComment(block);
            if (isComment)
            {
                AdvanceLineNumber(block, 0, ref lineNumber, ref addLineNumber, out var display);
                parsed = new GCodeBlock(lineNumber, display, display.Length + 1, true, false, row);
                return true;
            }

            if (block.IndexOfAny(['#', '[', ']', '=', ':']) >= 0)
                return false;
            if (block.Contains('(') || block.Contains(';'))
                return false;

            var pos = 0;
            var explicitLine = 0u;
            var axis = AxisFlags.None;
            var ijk = IJKFlags.None;
            var values = new double[9];
            var ijkValues = new double[3];
            var r = 0d;
            var hasR = false;
            var hasMotionWord = false;
            var sawSupportedWord = false;
            var tokensStart = parser.Tokens.Count;

            while (pos < block.Length)
            {
                var letter = char.ToUpperInvariant(block[pos++]);
                if (!TryReadNumber(block, ref pos, out var value))
                    return false;

                switch (letter)
                {
                    case 'N':
                        if (explicitLine != 0 || value < 0d || value > uint.MaxValue)
                            return false;
                        explicitLine = (uint)value;
                        sawSupportedWord = true;
                        break;

                    case 'G':
                        if (!TryApplyG(value, lineNumber, out var motionChanged, out var token))
                            return false;
                        if (token != null)
                            parser.Tokens.Add(token);
                        hasMotionWord |= motionChanged;
                        sawSupportedWord = true;
                        break;

                    case 'M':
                        if (value is 2d or 30d)
                        {
                            ProgramEnd = true;
                            sawSupportedWord = true;
                            break;
                        }
                        return false;

                    case 'F':
                        feedrate = imperial ? value * 25.4d : value;
                        parser.Tokens.Add(new GCFeedrate(Commands.Feedrate, lineNumber, feedrate, false));
                        sawSupportedWord = true;
                        break;

                    case 'X':
                    case 'Y':
                    case 'Z':
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'U':
                    case 'V':
                    case 'W':
                        var axisIndex = AxisIndex(letter);
                        if (axisIndex < 0)
                            return false;
                        axis |= GCodeParser.AxisFlag[axisIndex];
                        values[axisIndex] = imperial ? value * 25.4d : value;
                        sawSupportedWord = true;
                        break;

                    case 'I':
                    case 'J':
                    case 'K':
                        var ijkIndex = letter - 'I';
                        ijk |= GCodeParser.IjkFlag[ijkIndex];
                        ijkValues[ijkIndex] = imperial ? value * 25.4d : value;
                        sawSupportedWord = true;
                        break;

                    case 'R':
                        r = imperial ? value * 25.4d : value;
                        hasR = true;
                        sawSupportedWord = true;
                        break;

                    case 'P':
                        return false;

                    default:
                        return false;
                }
            }

            if (!sawSupportedWord)
                return false;

            AdvanceLineNumber(block, explicitLine, ref lineNumber, ref addLineNumber, out var displayBlock);
            SetTokenLineNumbers(tokensStart, lineNumber);

            if (!TryEmitMotion(lineNumber, axis, values, ijk, ijkValues, hasR, r, hasMotionWord))
                return false;

            parsed = new GCodeBlock(lineNumber, displayBlock, displayBlock.Length + 1, false, ProgramEnd, row);
            return true;
        }

        static string NormalizeBlock(string block)
        {
            if (block.Length == 0)
                return block;

            var buffer = new char[block.Length];
            var count = 0;
            var inComment = false;
            var keep = false;

            foreach (var c in block)
            {
                switch (c)
                {
                    case '\t':
                    case ' ':
                        if (inComment || keep)
                            buffer[count++] = ' ';
                        break;
                    case '(':
                        inComment = true;
                        buffer[count++] = c;
                        break;
                    case ')':
                        inComment = false;
                        buffer[count++] = c;
                        break;
                    case ';':
                        keep = true;
                        buffer[count++] = c;
                        break;
                    default:
                        buffer[count++] = char.ToUpperInvariant(c);
                        break;
                }
            }

            return new string(buffer[..count]);
        }

        static bool IsComment(string block) =>
            block[0] == ';' || block[0] == '(' && block.LastIndexOf(')') == block.Length - 1;

        static bool TryReadNumber(string block, ref int pos, out double value)
        {
            value = default;
            var start = pos;
            while (pos < block.Length)
            {
                var c = block[pos];
                if (char.IsLetter(c) || c == '(' || c == ';')
                    break;
                if (c is '[' or ']' or '#')
                    break;
                pos++;
            }

            return pos > start
                && double.TryParse(block.AsSpan(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        bool TryApplyG(double value, uint lineNumber, out bool motionChanged, out GCodeToken? token)
        {
            motionChanged = false;
            token = null;
            var iv = (int)Math.Floor(value);
            var fv = (int)Math.Round((value - iv) * 10d, 0);

            switch (iv)
            {
                case 0 when fv == 0:
                    motion = Commands.G0;
                    motionChanged = true;
                    return true;
                case 1 when fv == 0:
                    motion = Commands.G1;
                    motionChanged = true;
                    return true;
                case 2 when fv == 0:
                    motion = Commands.G2;
                    motionChanged = true;
                    return true;
                case 3 when fv == 0:
                    motion = Commands.G3;
                    motionChanged = true;
                    return true;
                case 17 when fv == 0:
                    plane = planes[0];
                    token = new GCPlane(Commands.G17, lineNumber, false);
                    return true;
                case 18 when fv == 0:
                    plane = planes[1];
                    token = new GCPlane(Commands.G18, lineNumber, false);
                    return true;
                case 19 when fv == 0:
                    plane = planes[2];
                    token = new GCPlane(Commands.G19, lineNumber, false);
                    return true;
                case 20 when fv == 0:
                    imperial = true;
                    token = new GCUnits(Commands.G20, lineNumber, false);
                    return true;
                case 21 when fv == 0:
                    imperial = false;
                    token = new GCUnits(Commands.G21, lineNumber, false);
                    return true;
                case 90 when fv == 0:
                    distanceMode = DistanceMode.Absolute;
                    token = new GCDistanceMode(Commands.G90, lineNumber, false);
                    return true;
                case 91 when fv == 0:
                    distanceMode = DistanceMode.Incremental;
                    token = new GCDistanceMode(Commands.G91, lineNumber, false);
                    return true;
                case 90 when fv == 1:
                    ijkMode = IJKMode.Absolute;
                    token = new GCIJKMode(Commands.G90_1, lineNumber, false);
                    return true;
                case 91 when fv == 1:
                    ijkMode = IJKMode.Incremental;
                    token = new GCIJKMode(Commands.G91_1, lineNumber, false);
                    return true;
                default:
                    return false;
            }
        }

        bool TryEmitMotion(uint lineNumber, AxisFlags axis, double[] values, IJKFlags ijk, double[] ijkValues, bool hasR, double r, bool hasMotionWord)
        {
            if (axis == AxisFlags.None && ijk == IJKFlags.None && !hasR)
                return true;

            if (motion == Commands.Undefined)
                return false;

            if (motion == Commands.G1 && feedrate == 0d)
                return false;

            if (motion is Commands.G0 or Commands.G1)
            {
                if (ijk != IJKFlags.None || hasR)
                    return false;

                var token = new GCLinearMotion(motion, lineNumber, values, axis, false);
                parser.Tokens.Add(token);
                ApplyLinearBounds(values, axis);
                return true;
            }

            if (motion is Commands.G2 or Commands.G3)
            {
                if (axis == AxisFlags.None || (ijk == IJKFlags.None) == !hasR)
                    return false;

                if (hasR)
                {
                    ijkValues[0] = ijkValues[1] = ijkValues[2] = double.NaN;
                    ijk = IJKFlags.None;
                }
                else
                {
                    for (var i = 0; i < 3; i++)
                    {
                        if (!ijk.HasFlag(GCodeParser.IjkFlag[i]))
                            ijkValues[i] = 0d;
                    }
                }

                var start = new[] { machine[0], machine[1], machine[2] };
                var token = new GCArc(motion, lineNumber, values, axis, ijkValues, ijk, r, 0, ijkMode, false);
                parser.Tokens.Add(token);
                bounds.AddBoundingBox(token.GetBoundingBox(plane, start, distanceMode == DistanceMode.Incremental));
                ApplyEnd(values, axis);
                return true;
            }

            return hasMotionWord;
        }

        void ApplyLinearBounds(double[] values, AxisFlags axis)
        {
            ApplyEnd(values, axis);
            bounds.AddPoint(new Point3D(machine[0], machine[1], machine[2]), axis);
        }

        void ApplyEnd(double[] values, AxisFlags axis)
        {
            foreach (var i in axis.ToIndices())
            {
                machine[i] = distanceMode == DistanceMode.Incremental
                    ? machine[i] + values[i]
                    : values[i];
            }
        }

        static void AdvanceLineNumber(string block, uint explicitLine, ref uint lineNumber, ref bool addLineNumber, out string displayBlock)
        {
            displayBlock = block;
            if (explicitLine > 0)
            {
                lineNumber = explicitLine;
                addLineNumber = false;
            }
            else if (addLineNumber)
            {
                lineNumber += 10;
                displayBlock = "N" + lineNumber.ToString(CultureInfo.InvariantCulture) + block;
            }
            else
            {
                lineNumber++;
            }
        }

        void SetTokenLineNumbers(int tokenStart, uint lineNumber)
        {
            for (var i = tokenStart; i < parser.Tokens.Count; i++)
                parser.Tokens[i].LineNumber = lineNumber;
        }

        static int AxisIndex(char axis) => axis switch
        {
            'X' => 0,
            'Y' => 1,
            'Z' => 2,
            'A' => 3,
            'B' => 4,
            'C' => 5,
            'U' => 6,
            'V' => 7,
            'W' => 8,
            _ => -1
        };
    }

    public class ProgramLimits : ViewModelBase
    {
        public ProgramLimits()
        {
            init();
        }

        public ProgramLimits(ProgramLimits limits, double scaleFactor)
        {
            for (var i = 0; i < MinValues.Length; i++)
            {
                MinValues[i] = limits.MinValues[i] * scaleFactor;
                MaxValues[i] = limits.MaxValues[i] * scaleFactor;
            }

            MinValues.PropertyChanged += MinValues_PropertyChanged;
            MaxValues.PropertyChanged += MaxValues_PropertyChanged;
        }

        private void init()
        {
            Clear();

            MinValues.PropertyChanged += MinValues_PropertyChanged;
            MaxValues.PropertyChanged += MaxValues_PropertyChanged;
        }

        public void Clear()
        {
            for (var i = 0; i < MinValues.Length; i++)
            {
                MinValues[i] = double.NaN;
                MaxValues[i] = double.NaN;
            }
        }

        public void Scale(double factor)
        {
            for (var i = 0; i < MinValues.Length; i++)
            {
                MinValues[i] *= factor;
                MaxValues[i] *= factor;
            }
        }

        public bool SuspendNotifications
        {
            get { return MinValues.SuspendNotifications; }
            set { MinValues.SuspendNotifications = MaxValues.SuspendNotifications = value; }
        }

        private void MinValues_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("Min" + e.PropertyName);
        }
        private void MaxValues_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged("Max" + e.PropertyName);
        }

        public CoordinateValues<double> MinValues { get; private set; } = new CoordinateValues<double>();
        public double MinX { get { return MinValues[0]; } set { MinValues[0] = value; } }
        public double MinY { get { return MinValues[1]; } set { MinValues[1] = value; } }
        public double MinZ { get { return MinValues[2]; } set { MinValues[2] = value; } }
        public double MinA { get { return MinValues[3]; } set { MinValues[3] = value; } }
        public double MinB { get { return MinValues[4]; } set { MinValues[4] = value; } }
        public double MinC { get { return MinValues[5]; } set { MinValues[5] = value; } }
        public double MinU { get { return MinValues[6]; } set { MinValues[6] = value; } }
        public double MinV { get { return MinValues[7]; } set { MinValues[7] = value; } }
        public double MinW { get { return MinValues[8]; } set { MinValues[8] = value; } }

        public CoordinateValues<double> MaxValues { get; private set; } = new CoordinateValues<double>();
        public double MaxX { get { return MaxValues[0]; } set { MaxValues[0] = value; } }
        public double MaxY { get { return MaxValues[1]; } set { MaxValues[1] = value; } }
        public double MaxZ { get { return MaxValues[2]; } set { MaxValues[2] = value; } }
        public double MaxA { get { return MaxValues[3]; } set { MaxValues[3] = value; } }
        public double MaxB { get { return MaxValues[4]; } set { MaxValues[4] = value; } }
        public double MaxC { get { return MaxValues[5]; } set { MaxValues[5] = value; } }
        public double MaxU { get { return MaxValues[6]; } set { MaxValues[6] = value; } }
        public double MaxV { get { return MaxValues[7]; } set { MaxValues[7] = value; } }
        public double MaxW { get { return MaxValues[8]; } set { MaxValues[8] = value; } }

        public double SizeX { get { return MaxX - MinX; } }
        public double SizeY { get { return MaxY - MinY; } }
        public double SizeZ { get { return MaxZ - MinZ; } }
        public double MaxSize { get { return Math.Max(Math.Max(SizeX, SizeY), SizeZ); } }
    }

    public class GcodeBoundingBox
    {
        public double[] Min = new double[9];
        public double[] Max = new double[9];
        public double[] Size = new double[9];

        public GcodeBoundingBox()
        {
            Reset();
        }

        public void Reset()
        {
            for (int i = 0; i < Min.Length; i++)
            {
                Min[i] = double.MaxValue;
                Max[i] = double.MinValue;
            }
        }

        public void Conclude()
        {
            for (int i = 0; i < Min.Length; i++)
            {
                if (Max[i] == double.MinValue)
                    Min[i] = Max[i] = 0.0;
                Size[i] = Math.Abs(Max[i] - Min[i]);
            }
        }

        private void AddPoint(double x, double y, double z)
        {
            Min[0] = Math.Min(Min[0], x);
            Max[0] = Math.Max(Max[0], x);

            Min[1] = Math.Min(Min[1], y);
            Max[1] = Math.Max(Max[1], y);

            Min[2] = Math.Min(Min[2], z);
            Max[2] = Math.Max(Max[2], z);
        }

        public void AddPoint(GCPlane plane, double x, double y, double z)
        {
            Min[plane.Axis0] = Math.Min(Min[plane.Axis0], x);
            Max[plane.Axis0] = Math.Max(Max[plane.Axis0], x);

            Min[plane.Axis1] = Math.Min(Min[plane.Axis1], y);
            Max[plane.Axis1] = Math.Max(Max[plane.Axis1], y);

            Min[plane.AxisLinear] = Math.Min(Min[plane.AxisLinear], z);
            Max[plane.AxisLinear] = Math.Max(Max[plane.AxisLinear], z);
        }
        public void AddPoint(GCPlane plane, Point3D point)
        {
            Min[plane.Axis0] = Math.Min(Min[plane.Axis0], point.X);
            Max[plane.Axis0] = Math.Max(Max[plane.Axis0], point.X);

            Min[plane.Axis1] = Math.Min(Min[plane.Axis1], point.Y);
            Max[plane.Axis1] = Math.Max(Max[plane.Axis1], point.Y);

            Min[plane.AxisLinear] = Math.Min(Min[plane.AxisLinear], point.Z);
            Max[plane.AxisLinear] = Math.Max(Max[plane.AxisLinear], point.Z);
        }

        public void AddPoint(Point3D point)
        {
            Min[0] = Math.Min(Min[0], point.X);
            Max[0] = Math.Max(Max[0], point.X);

            Min[1] = Math.Min(Min[1], point.Y);
            Max[1] = Math.Max(Max[1], point.Y);

            Min[2] = Math.Min(Min[2], point.Z);
            Max[2] = Math.Max(Max[2], point.Z);
        }
        public void AddPoint(Point3D point, AxisFlags axisflags)
        {
            if (axisflags.HasFlag(AxisFlags.X))
            {
                Min[0] = Math.Min(Min[0], point.X);
                Max[0] = Math.Max(Max[0], point.X);
            }

            if (axisflags.HasFlag(AxisFlags.Y))
            { 
                Min[1] = Math.Min(Min[1], point.Y);
                Max[1] = Math.Max(Max[1], point.Y);
            }

            if (axisflags.HasFlag(AxisFlags.Z))
            {
                Min[2] = Math.Min(Min[2], point.Z);
                Max[2] = Math.Max(Max[2], point.Z);
            }
        }

        public void AddBoundingBox(GcodeBoundingBox bbox)
        {
            AddPoint(bbox.Min[0], bbox.Min[1], bbox.Min[2]);
            AddPoint(bbox.Max[0], bbox.Max[1], bbox.Max[2]);
        }
    }
}
