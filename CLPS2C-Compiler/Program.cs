using CommandLine;
using System.Text;
using System.Text.RegularExpressions;
using static CLPS2C_Compiler.Util;

namespace CLPS2C_Compiler
{
    class ConsoleParserOptions
    {
        [Option('i', "input", Required = false, HelpText = "Set the input file.")]
        public string? InputFilePath { get; set; }

        [Option('o', "output", Required = false, HelpText = "Set the output file.")]
        public string? OutputFilePath { get; set; }

        [Option('p', "pnach", Required = false, HelpText = "Convert output to Pnach-format.")]
        public bool UsePnachFormat { get; set; }

        [Option('d', "dtype", Required = false, HelpText = "Use D-type codes instead of E-type codes for conditionals.")]
        public bool UseDTypeCode { get; set; }
    }

    internal class Program
    {
        public static string newLine = Environment.NewLine;
        public static bool error = false;
        public static List<LocalVar_t> listSets = new();

        static string _inputFilePath = "";
        static List<Function_t> _functionList = new(); // List to store the function definitions
        static Dictionary<string, string> _commandsDict = new(); // Dictionary for the commands that have abbreviations.
        static Encoding _currentEncoding = Encoding.UTF8;
        static Regex _ifRegex = new(@"If (\w+\s*(?:\+\s*-?(0x)?[0-9A-F]{1,8})*) ([=!<>&|]|~[&|])([:.]) (\w+\s*(?:\+\s*-?(0x){1}[0-9A-F]{1,8}|\+\s*-?(?!0x)\d+)*)(?: (&&) (\w+\s*(?:\+\s*-?(0x)?[0-9A-F]{1,8})*) ([!=<>&|]|~[&|])([:.]) (\w+\s*(?:\+\s*-?(0x){1}[0-9A-F]{1,8}|\+\s*-?(?!0x)\d+)*))*$", RegexOptions.IgnoreCase);
        static EEAssembler _ee;
        static ErrorInfo_t _errorInfo = new();
        static ParserResult<ConsoleParserOptions>? _consoleParser;

        // 1) Arg1: FilePath.                   Output in same folder, with the same file name + "-Output.txt"
        // 2) Arg1: FilePath.   Arg2: FilePath. Output to Arg2
        static void Main(string[] args)
        {
            bool EnableElapsedTimer = false;
            System.Diagnostics.Stopwatch stopWatch = new();
            if (EnableElapsedTimer)
            {
                stopWatch.Start();
            }

            if (args.Length == 0)
            {
                ConsolePrintError($"No arguments were passed.\nWrite \"CLPS2C-Compiler.exe --help\" for more information.\nPress any key to exit.");
                Console.ReadKey();
                return;
            }

            _consoleParser = Parser.Default.ParseArguments<ConsoleParserOptions>(args);
            if (_consoleParser.Value == null || _consoleParser.Value.InputFilePath == null)
            {
                if (args.Contains("--help") || _consoleParser.Errors.Any())
                {
                    return;
                }
                ConsolePrintError($"Input file argument not found.\nWrite \"CLPS2C-Compiler.exe --help\" for more information.\nPress any key to exit.");
                Console.ReadKey();
                return;
            }

            _inputFilePath = _consoleParser.Value.InputFilePath;
            if (!IsFilePathValid(_inputFilePath))
            {
                ConsolePrintError($"Invalid input file: \"{_inputFilePath}\"\nWrite \"CLPS2C-Compiler.exe --help\" for more information.\nPress any key to exit.");
                Console.ReadKey();
                return;
            }

            if (_consoleParser.Value.OutputFilePath == null)
            {
                _consoleParser.Value.OutputFilePath = $"{Path.GetDirectoryName(_inputFilePath)}{Path.DirectorySeparatorChar}{Path.GetFileNameWithoutExtension(_inputFilePath)}-Output.txt";
            }

            List<string> Lines = File.ReadAllLines(_inputFilePath, Encoding.UTF8).ToList();
            PopulateAbbreviationDict();
            List<string> OutputLines = ParseLines(Lines);

            // Output
            string Output = "";
            if (error)
            {
                Output = PrintError(_errorInfo);
            }
            else if (OutputLines.Count != 0)
            {
                if (OutputLines[0].StartsWith(newLine))
                {
                    OutputLines[0] = OutputLines[0].Substring(2);
                }
                Output += string.Join("", OutputLines);

                // Convert to PNACH Format
                if (_consoleParser.Value.UsePnachFormat)
                {
                    Output = ConvertRawToPnach(Output);
                }
            }

            if (EnableElapsedTimer)
            {
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Output = $"Elapsed time: {ts.Minutes:00}:{ts.Seconds:00}.{ts.Milliseconds}{newLine}{Output}";
            }
            File.WriteAllText(_consoleParser.Value.OutputFilePath, Output);
        }

        static List<string> ParseLines(List<string> lines)
        {
            List<Command_t> Commands = new();
            List<string> OutputLines = new();
            bool InAsmScope = false; // If in assembly region. Can be changed with the ASM_START and ASM_END commands
            uint AsmStartAddress = 0; // (ADDRESS) argument for the ASM_START command
            lines = TextCleanUp(lines);
            Commands = GetListCommands(lines, _inputFilePath);

            // If no commands were found
            if (Commands.Count == 0)
            {
                return OutputLines;
            }

            ReplaceInclude(Commands);
            if (error)
            {
                return OutputLines;
            }

            GetFunctionList(Commands);
            if (error)
            {
                return OutputLines;
            }

            // Give each command an ID based on their position in the list
            Commands = Commands.Select((item, Index) => { item.ID = Index; return item; }).ToList();

            ReplaceCall(ref Commands);
            if (error)
            {
                return OutputLines;
            }

            GetListSets(Commands);
            if (error)
            {
                return OutputLines;
            }

            // Remove "SET" commands
            Commands = Commands.Where(item => item.Type != "SET").ToList();
            ApplySetsToCommands(Commands, listSets);
            List<Command_t> AsmLines = new();

            for (int i = 0; i < Commands.Count; i++)
            {
                if (InAsmScope)
                {
                    if (Commands[i].Type == "ASM_END")
                    {
                        InAsmScope = false;
                        Commands[i].Output = HandleAsmEnd(Commands[i], AsmLines, ref AsmStartAddress, out Commands[i].Weight);
                        if (error)
                        {
                            return OutputLines;
                        }

                        AsmLines.Clear();
                        AsmStartAddress = 0;
                        continue;
                    }

                    AsmLines.Add(Commands[i]);
                    continue;
                }

                Commands[i].Output = HandleCommand(Commands[i], out AsmStartAddress, out InAsmScope);
                if (error)
                {
                    return OutputLines;
                }
            }

            if (InAsmScope)
            {
                SetError(ERROR.MISS_ASM_END, Commands.Last());
                return OutputLines;
            }

            if (Commands.Any(item => item.Type == "IF"))
            {
                CorrectIf(Commands);
                if (error)
                {
                    // Miss endif
                    return OutputLines;
                }
            }

            OutputLines = Commands.Where(item => item.Output != null && item.Output != "").Select(item => item.Output).ToList()!;
            return OutputLines;
        }

        static string HandleCommand(Command_t command, out uint asmStartAddress, out bool inAsmScope)
        {
            string Output = "";
            inAsmScope = false;
            asmStartAddress = 0;

            // Switch type if abbreviation
            if (_commandsDict.TryGetValue(command.Type, out string? value))
            {
                command.Type = value;
            }

            switch (command.Type)
            {
                case "SETENCODING":
                    Output = HandleSetEncoding(command);
                    break;
                case "SENDRAW":
                    Output = HandleSendRaw(command);
                    break;
                case "SENDRAWWEIGHT":
                    Output = HandleSendRawWeight(command);
                    break;
                case "WRITE8":
                case "WRITE16":
                case "WRITE32":
                    Output = HandleWriteX(command);
                    break;
                case "WRITEFLOAT":
                    Output = HandleWriteFloat(command);
                    break;
                case "WRITESTRING":
                    Output = HandleWriteString(command);
                    break;
                case "WRITEBYTES":
                    Output = HandleWriteBytes(command);
                    break;
                case "WRITEPOINTER8":
                case "WRITEPOINTER16":
                case "WRITEPOINTER32":
                    Output = HandleWritePointerX(command);
                    break;
                case "WRITEPOINTERFLOAT":
                    Output = HandleWritePointerFloat(command);
                    break;
                case "COPYBYTES":
                    Output = HandleCopyBytes(command);
                    break;
                case "FILL8":
                case "FILL16":
                case "FILL32":
                    Output = HandleFillX(command);
                    break;
                case "INCREMENT8":
                case "INCREMENT16":
                case "INCREMENT32":
                    Output = HandleIncX(command);
                    break;
                case "DECREMENT8":
                case "DECREMENT16":
                case "DECREMENT32":
                    Output = HandleDecX(command);
                    break;
                case "OR8":
                case "OR16":
                case "AND8":
                case "AND16":
                case "XOR8":
                case "XOR16":
                    Output = HandleBoolX(command);
                    break;
                case "IF":
                    Output = HandleIf(command);
                    break;
                case "ENDIF":
                    break;
                case "ASM_START":
                    asmStartAddress = HandleAsmStart(command);
                    inAsmScope = true;
                    break;
                case "ASM_END":
                    SetError(ERROR.MISS_ASM_START, command);
                    break;
                default:
                    SetError(ERROR.UNKNOWN_COMMAND, command);
                    break;
            }
            return Output;
        }

        static LocalVar_t HandleSet(Command_t command)
        {
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return new LocalVar_t("", new List<string>());
            }

            return new LocalVar_t(command.Data[0], command.Data.Skip(1).ToList());
        }

        static string HandleSetEncoding(Command_t command)
        {
            if (command.WordCount != 2)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // _currentEncoding = Encoding.GetEncoding(currentCommand.Data[0]);
            switch (command.Data[0])
            {
                case "UTF-8":
                    _currentEncoding = Encoding.UTF8;
                    break;
                case "UTF-16":
                    _currentEncoding = Encoding.Unicode;
                    break;
                default:
                    SetError(ERROR.VALUE_INVALID, command);
                    break;
            }
            return "";
        }

        static string HandleSendRaw(Command_t command)
        {
            if (command.WordCount < 2)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            int i = 0;
            string Value = GetStringValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            Value = Value.Replace("\\n", newLine)
                         .Replace("\\t", "\t")
                         .Replace("\\\"", "\"");
            return Value;
        }

        static string HandleSendRawWeight(Command_t command)
        {
            string Value = HandleSendRaw(command);
            command.Weight = 1 + CountCharInRange(Value, '\n', 0, Value.Length);
            return $"{newLine}{Value}";
        }

        static string HandleWriteX(Command_t command)
        {
            // W8:   0aaaaaaa 000000vv
            // W16:  1aaaaaaa 0000vvvv
            // W32:  2aaaaaaa vvvvvvvv
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            switch (command.Type)
            {
                case "WRITE8":
                    Address = $"0{Address}";
                    Value = $"000000{Value.Substring(6, 2)}";
                    break;
                case "WRITE16":
                    Address = $"1{Address}";
                    Value = $"0000{Value.Substring(4, 4)}";
                    break;
                case "WRITE32":
                    Address = $"2{Address}";
                    break;
            }

            command.Weight = 1;
            return $"{newLine}{Address} {Value}";
        }

        static string HandleWriteFloat(Command_t command)
        {
            //WF:   2aaaaaaa vvvvvvvv
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetFloatValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            command.Weight = 1;
            return $"{newLine}2{Address} {Value}";
        }

        static string HandleWriteString(Command_t command)
        {
            // WS 20E71C00 "park"
            // 20E71C00 6B726170
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetStringValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            Value = Value.Replace("\\0", "\0")
                         .Replace("\\n", "\n")
                         .Replace("\\t", "\t")
                         .Replace("\\\"", "\"");
            byte[] TmpArray = _currentEncoding.GetBytes(Value); // "park" -> 0x70,0x61,0x72,0x6B
            string Output = GetAddressValuePairsFromAOB(TmpArray, Address);
            command.Weight = CountCharInRange(Output, '\n', 0, Output.Length);
            return Output;
        }

        static string HandleWriteBytes(Command_t command)
        {
            // WB 20E71C00 "00 11 22 33"
            // 20E71C00 33221100
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetStringValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            if (!IsAOBValid(Value))
            {
                SetError(ERROR.VALUE_INVALID, command);
                return "";
            }

            // "00 11 22 33" -> 0x00,0x11,0x22,0x33
            byte[] TmpArray = Value.Split().Select(item => byte.Parse(item, System.Globalization.NumberStyles.AllowHexSpecifier)).ToArray();
            string Output = GetAddressValuePairsFromAOB(TmpArray, Address);
            command.Weight = CountCharInRange(Output, '\n', 0, Output.Length);
            return Output;
        }

        static string HandleWritePointerX(Command_t command)
        {
            // WPX ADDRESS,OFFSET[,OFFSET2...] VALUE
            // WP8:  6aaaaaaa 000000vv
            //       0000nnnn iiiiiiii
            //       pppppppp pppppppp
            // WP16: 6aaaaaaa 0000vvvv
            //       0001nnnn iiiiiiii
            //       pppppppp pppppppp
            // WP32: 6aaaaaaa vvvvvvvv
            //       0002nnnn iiiiiiii
            //       pppppppp pppppppp
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESSES
            int i = 0;
            List<string> ListOffs = GetAddresses(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            string OffsCount = (ListOffs.Count - 1).ToString("X4"); // - base
            if (OffsCount == "0000")
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }
            string Output = "";
            switch (command.Type)
            {
                case "WRITEPOINTER8":
                    Output = $"6{ListOffs[0]} 000000{Value.Substring(6, 2)}{newLine}0000{OffsCount} {ListOffs[1]}";
                    break;
                case "WRITEPOINTER16":
                    Output = $"6{ListOffs[0]} 0000{Value.Substring(4, 4)}{newLine}0001{OffsCount} {ListOffs[1]}";
                    break;
                case "WRITEPOINTER32":
                    Output = $"6{ListOffs[0]} {Value}{newLine}0002{OffsCount} {ListOffs[1]}";
                    break;
            }

            Output += GetRemainingOffsets(ListOffs, out int weight);
            command.Weight = 2 + weight;
            return $"{newLine}{Output}";
        }

        static string HandleWritePointerFloat(Command_t command)
        {
            // WPF ADDRESS,OFFSET[,OFFSET2...] VALUE
            // WPF:  6aaaaaaa vvvvvvvv
            //       0002nnnn iiiiiiii
            //       pppppppp pppppppp

            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESSES
            int i = 0;
            List<string> ListOffs = GetAddresses(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetFloatValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            string OffsCount = (ListOffs.Count - 1).ToString("X4"); // - base
            if (OffsCount == "0000")
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }
            string Output = $"6{ListOffs[0]} {Value}{newLine}0002{OffsCount} {ListOffs[1]}";

            Output += GetRemainingOffsets(ListOffs, out int weight);
            command.Weight = 2 + weight;
            return $"{newLine}{Output}";
        }

        static string HandleCopyBytes(Command_t command)
        {
            // CB 20E71C00 20E71C04 4
            // 5sssssss nnnnnnnn
            // 0ddddddd 00000000
            if (command.WordCount < 4)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // SOURCE ADDRESS
            int i = 0;
            string SourceAddress = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // DESTINATION ADDRESS
            string DestinationAddress = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string LengthValue = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }
            if (Convert.ToInt32(LengthValue, 16) < 0)
            {
                SetError(ERROR.VALUE_INVALID, command);
                return "";
            }

            command.Weight = 2;
            return $"{newLine}5{SourceAddress} {LengthValue}{newLine}0{DestinationAddress} 00000000";
        }

        static string HandleFillX(Command_t command)
        {
            // FillX 000F0000 0xDEADBEEF 0x10
            // 8aaaaaaa nnnnssss
            // 000000vv 000000ii

            // 8aaaaaaa nnnnssss
            // 1000vvvv 0000iiii

            // 4aaaaaaa nnnnssss
            // vvvvvvvv iiiiiiii
            if (command.WordCount < 4)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // LENGTH
            string Length = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            var Tmp = Convert.ToInt32(Length, 16);
            if (Tmp < 0)
            {
                SetError(ERROR.VALUE_INVALID, command);
                return "";
            }

            switch (command.Type)
            {
                case "FILL8":
                    Address = $"8{Address}";
                    Value = $"000000{Value.Substring(6, 2)}";
                    if (Tmp > 0xFFFF)
                    {
                        SetError(ERROR.VALUE_INVALID, command);
                        return "";
                    }

                    Length = Tmp.ToString("X8").Substring(4);
                    break;
                case "FILL16":
                    Address = $"8{Address}";
                    Value = $"1000{Value.Substring(4, 4)}";
                    if (Tmp % 2 != 0)
                    {
                        SetError(ERROR.LENGTH_MUST_BE_DIVISIBLE_BY_2, command);
                        return "";
                    }
                    else if (Tmp > 0x1FFFE)
                    {
                        SetError(ERROR.VALUE_INVALID, command);
                        return "";
                    }

                    Length = (Tmp / 2).ToString("X4");
                    break;
                case "FILL32":
                    Address = $"4{Address}";
                    if (Tmp % 4 != 0)
                    {
                        SetError(ERROR.LENGTH_MUST_BE_DIVISIBLE_BY_4, command);
                        return "";
                    }
                    else if (Tmp > 0x3FFFC)
                    {
                        SetError(ERROR.VALUE_INVALID, command);
                        return "";
                    }

                    Length = (Tmp / 4).ToString("X4");
                    break;
            }
            
            command.Weight = 2;
            return $"{newLine}{Address} {Length}0001{newLine}{Value} 00000000";
        }

        static string HandleIncX(Command_t command)
        {
            // I8:   300000vv 0aaaaaaa
            // I16:  3020vvvv 0aaaaaaa
            // I32:  30400000 0aaaaaaa
            //       vvvvvvvv 00000000
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            string Output = "";
            switch (command.Type)
            {
                case "INCREMENT8":
                    Output = $"300000{Value.Substring(6, 2)} 0{Address}";
                    command.Weight = 1;
                    break;
                case "INCREMENT16":
                    Output = $"3020{Value.Substring(4, 4)} 0{Address}";
                    command.Weight = 1;
                    break;
                case "INCREMENT32":
                    Output = $"30400000 0{Address}{newLine}{Value} 00000000";
                    command.Weight = 2;
                    break;
            }

            return $"{newLine}{Output}";
        }

        static string HandleDecX(Command_t command)
        {
            // D8:   301000vv 0aaaaaaa
            // D16:  3030vvvv 0aaaaaaa
            // D32:  30500000 0aaaaaaa
            //       vvvvvvvv 00000000
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            string Output = "";
            switch (command.Type)
            {
                case "DECREMENT8":
                    Output = $"301000{Value.Substring(6, 2)} 0{Address}";
                    command.Weight = 1;
                    break;
                case "DECREMENT16":
                    Output = $"3030{Value.Substring(4, 4)} 0{Address}";
                    command.Weight = 1;
                    break;
                case "DECREMENT32":
                    Output = $"30500000 0{Address}{newLine}{Value} 00000000";
                    command.Weight = 2;
                    break;
            }

            return $"{newLine}{Output}";
        }

        static string HandleBoolX(Command_t command)
        {
            //           7aaaaaaa 00t0vvvv
            // OR8:      7aaaaaaa 000000vv
            // OR16:     7aaaaaaa 0010vvvv
            // AND8:     7aaaaaaa 002000vv
            // AND16:    7aaaaaaa 0030vvvv
            // XOR8:     7aaaaaaa 004000vv
            // XOR16:    7aaaaaaa 0050vvvv
            if (command.WordCount < 3)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }
            
            // VALUE
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            string Type = "";
            switch (command.Type)
            {
                case "OR8":
                    Type = "0";
                    Value = $"00{Value.Substring(6, 2)}";
                    break;
                case "OR16":
                    Type = "1";
                    Value = $"{Value.Substring(4, 4)}";
                    break;
                case "AND8":
                    Type = "2";
                    Value = $"00{Value.Substring(6, 2)}";
                    break;
                case "AND16":
                    Type = "3";
                    Value = $"{Value.Substring(4, 4)}";
                    break;
                case "XOR8":
                    Type = "4";
                    Value = $"00{Value.Substring(6, 2)}";
                    break;
                case "XOR16":
                    Type = "5";
                    Value = $"{Value.Substring(4, 4)}";
                    break;
            }
            
            command.Weight = 1;
            return $"{newLine}7{Address} 00{Type}0{Value}";
        }

        static string HandleIf(Command_t command)
        {
            // E1nn00vv taaaaaaa | Daaaaaaa nnt100vv
            // E0nnvvvv taaaaaaa | Daaaaaaa nnt0vvvv
            // t = 0 -> equal =
            // t = 1 -> not equal !
            // t = 2 -> less than <
            // t = 3 -> greater than >
            // t = 4 -> NAND ~&
            // t = 5 -> AND &
            // t = 6 -> NOR ~|
            // t = 7 -> OR |

            // Check if currentCommand.Output is null before doing the regex match:
            // This way, if the If command has a logical AND, for the extra produced If commands it will not try to match the regex pattern again
            // So, it will only *try* to match the regex pattern if it is the first time this command is being parsed
            if (command.WordCount < 2 || command.Output == null && !_ifRegex.IsMatch(command.FullLine))
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return "";
            }
            if (i >= command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return "";
            }

            // All characters up to, but not including, the last element. To accomodate for NAND and NOR (~&, ~|),
            string Condition = command.Data[i][..^1].ToString();
            // Last character
            string Type = command.Data[i][^1].ToString();

            // VALUE
            i++;
            string Value = GetIntValue(command, ref i);
            if (error)
            {
                return "";
            }

            // CONDITION
            switch (Condition)
            {
                case "=":
                    Condition = "0"; // Equality
                    break;
                case "!":
                    Condition = "1"; // Inequality
                    break;
                case "<":
                    Condition = "2"; // Less than
                    break;
                case ">":
                    Condition = "3"; // Greater than
                    break;
                case "~&":
                    Condition = "4"; // NAND
                    break;
                case "&":
                    Condition = "5"; // AND
                    break;
                case "~|":
                    Condition = "6"; // NOR
                    break;
                case "|":
                    Condition = "7"; // OR
                    break;
            }

            // TYPE
            switch (Type)
            {
                case ".":
                    Type = "1"; // 1 byte
                    Value = $"00{Value.Substring(6, 2)}";
                    break;
                case ":":
                    Type = "0"; // 2 bytes
                    Value = $"{Value.Substring(4, 4)}";
                    break;
            }

            command.Weight = 1;
            if (_consoleParser!.Value.UseDTypeCode)
            {
                return $"{newLine}D{Address} nn{Condition}{Type}{Value}";
            }

            return $"{newLine}E{Type}nn{Value} {Condition}{Address}";
        }

        static uint HandleAsmStart(Command_t command)
        {
            uint asmStartAddress = 0;
            if (command.WordCount < 2)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return asmStartAddress;
            }

            // ADDRESS
            int i = 0;
            string Address = GetAddress(command, ref i);
            if (error)
            {
                return asmStartAddress;
            }
            if (i < command.Data.Count)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return asmStartAddress;
            }

            asmStartAddress = Convert.ToUInt32($"2{Address}", 16);
            return asmStartAddress;
        }

        static string HandleAsmEnd(Command_t command, List<Command_t> asmLines, ref uint asmStartAddress, out int instructionsCount)
        {
            if (command.Data.Count != 0)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                instructionsCount = 0;
                return "";
            }

            string Output = "";
            if (_ee == null)
            {
                // Only create the instance when it's needed
                _ee = new();
            }

            byte[] bytes = _ee.Assemble(asmLines);
            instructionsCount = bytes.Length / 4;
            if (error)
            {
                return "";
            }

            for (int i = 0; i < instructionsCount; i++)
            {
                Output += $"{newLine}{asmStartAddress + i * 4:X8} {BitConverter.ToUInt32(bytes, i * 4):X8}";
            }

            return Output;
        }
    
        static List<Command_t> HandleInclude(Command_t command)
        {
            // 1) Absolute path: "C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC\Engine.txt"
            // 2) Relative path to the input file path (same folder as the input file): "Engine.txt"
            // 3) Relative path to the input file path (sub-folder(s) from the input file): "IncludeExample\Sly2\NTSC\Engine.txt"
            // 4) Relative path to the most recent included file path: "C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC\Engine.txt"
            //    includes "Entity.txt" (in the folder C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC)

            if (command.WordCount != 2)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return [];
            }

            string FilePath = command.Data[0];

            // VALUE
            if (!StartsAndEndsWithQuotes(FilePath))
            {
                SetError(ERROR.MISSING_QUOTES, command);
                return [];
            }
            FilePath = GetSubstringInQuotes(FilePath, false);

            if (!IsAbsolutePath(FilePath))
            {
                // 2) Relative path to the input file path (same folder as the input file):
                //      "Engine.txt"
                //      -> "C:\Users\admin\Desktop\CLPS2C\Engine.txt"
                // 3) Relative path to the input file path (sub-folder(s) from the input file):
                //      "IncludeExample\Sly2\NTSC\Engine.txt"
                //      -> "C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC\Engine.txt"
                // 4) Relative path to the most recent included file path:
                //      "C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC\Engine.txt"
                //      includes "Entity.txt" (in the folder C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC)
                //      -> "C:\Users\admin\Desktop\CLPS2C\IncludeExample\Sly2\NTSC\Entity.txt"
                var ParentFilePath = command.Traceback.First().FilePath;
                FilePath = $"{Path.GetDirectoryName(ParentFilePath)}{Path.DirectorySeparatorChar}{FilePath}";
            }

            if (command.Traceback.Any(item => item.FilePath == FilePath))
            {
                // overflow
                SetError(ERROR.INCLUDE_STACK_OVERFLOW, command);
                return [];
            }

            if (!IsFilePathValid(FilePath))
            {
                // Folder or invalid file
                SetError(ERROR.VALUE_INVALID, command);
                return [];
            }

            List<string> lines = File.ReadAllLines(FilePath).ToList();
            lines = TextCleanUp(lines);
            List<Command_t> IncludeCommands = GetListCommands(lines, FilePath);

            if (Path.GetExtension(FilePath) == ".sym")
            {
                // Parse symbol file. We create SET commands for labels and functions, and skip global definitions.
                // There are 3 types of definitions in a symbol file:
                // Label
                // [address] [name]
                // [address] Always hex without 0x prefix and can be up to 8 digits
                // [name] is a string and it's max 255 chars long

                // Function
                // [address] [name],[size]
                // [address] Always hex without 0x prefix and can be up to 8 digits
                // [name] is a string and it's max 255 chars long
                // [size] Always hex without 0x prefix and can be up to 8 digits

                // Data directive/Global variable
                // [address] .[type]:[size]
                // [address] Always hex without 0x prefix and can be up to 8 digits
                // [type] possible values: "byt", "wrd", "dbl", "asc"
                // [size] Always hex without 0x prefix and can be up to 8 digits

                // In a symbol file we don't exit the program if there's an error, we just skip the line

                for (int i = 0; i < IncludeCommands.Count; i++)
                {
                    // skip if wrong syntax or it's a data directive
                    if (IncludeCommands[i].Data.Count == 0 || IncludeCommands[i].Data[0].StartsWith("."))
                    {
                        IncludeCommands.RemoveAt(i);
                        i--;
                        continue;
                    }

                    string address = IncludeCommands[i].Type;

                    // skip invalid address
                    if (!IsAddressValid(address))
                    {
                        IncludeCommands.RemoveAt(i);
                        i--;
                        continue;
                    }

                    // LABEL or FUNCTION
                    // Change type to SET
                    // 1st element of Data is [name], 2nd element is [address]; remove the rest of Data
                    IncludeCommands[i].Type = "SET";
                    if (IncludeCommands[i].Data.Count == 1)
                    {
                        IncludeCommands[i].Data.Add(address);
                    }
                    else
                    {
                        IncludeCommands[i].Data[1] = address;
                        IncludeCommands[i].Data.RemoveRange(2, IncludeCommands[i].Data.Count - 2);
                    }
                }
            }

            return IncludeCommands;
        }

        static void HandleFunction(Command_t command)
        {
            if (command.WordCount == 1)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return;
            }

            Function_t NewFunction = new(command);
            if (NewFunction.Name == null)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return;
            }

            // Check if the function has already been defined.
            int Index = _functionList.FindIndex(item => item.Name == NewFunction.Name);
            if (Index != -1)
            {
                SetError(ERROR.FUNCTION_ALREADY_DEFINED, command);
                return;
            }

            // Add new function to list
            _functionList.Add(NewFunction);
            return;
        }

        static List<Command_t> HandleCall(Command_t command)
        {
            Match Match = Regex.Match(command.FullLine, @"Call (\w+)(\(.*\))", RegexOptions.IgnoreCase);
            if (!Match.Success)
            {
                SetError(ERROR.WRONG_SYNTAX, command);
                return [];
            }

            // Get the matching function
            Function_t? TargetFunction = _functionList.FirstOrDefault(item => item.Name == Match.Groups[1].Value);

            // If there is no function definition with the target NAME argument
            if (TargetFunction == null)
            {
                SetError(ERROR.VALUE_INVALID, command);
                return [];
            }

            List<string> CallArgs = GetCallArgsFromCommand(command);
            if (error)
            {
                return [];
            }

            if (CallArgs.Count == 1 && CallArgs[0] == "")
            {
                CallArgs.Clear();
            }

            // If the number of the arguments do not match
            if (TargetFunction.Args!.Count != CallArgs.Count)
            {
                SetError(ERROR.ARGUMENT_COUNT_MISMATCH, command);
                return [];
            }

            // Populate FunctionArgsSets with the args passed
            List<LocalVar_t> FunctionArgsSets = new(); // The args()
            for (int i = 0; i < TargetFunction.Args.Count; i++)
            {
                if (CallArgs[i].Contains('+'))
                {
                    List<string> q = GetDelimitatedPlusString(CallArgs[i]);
                    FunctionArgsSets.Add(new LocalVar_t(TargetFunction.Args[i], q));
                    continue;
                }
                FunctionArgsSets.Add(new LocalVar_t(TargetFunction.Args[i], new List<string>() { CallArgs[i] }));
            }

            // Get the commands inside the function
            List<Command_t> FunctionCommands = TargetFunction.Commands!.Select(item => item.DeepClone()).ToList();
            FunctionCommands.ForEach(item => item.AppendToTraceback(command.Traceback));

            if (FunctionArgsSets.Count == 0)
            {
                return FunctionCommands;
            }

            // Replace the arguments in each FunctionCommands
            // ONLY WITH FunctionArgsSets
            ApplySetsToCommands(FunctionCommands, FunctionArgsSets);

            // Handle a Call command differently
            for (int i = 0; i < FunctionCommands.Count; i++)
            {
                if (FunctionCommands[i].Type != "CALL")
                {
                    continue;
                }

                List<string> CallInCallArgs = GetCallArgsFromCommand(FunctionCommands[i]);
                if (error)
                {
                    return [];
                }

                for (int j = 0; j < CallInCallArgs.Count; j++)
                {
                    string Tmp = "";
                    List<string> DelimList = GetDelimitatedPlusString(CallInCallArgs[j]);

                    for (int k = 0; k < DelimList.Count; k++)
                    {
                        if (IsVarDeclared(DelimList[k], FunctionArgsSets))
                        {
                            List<string> ListValues = GetSetValuesFromTarget(DelimList[k], FunctionCommands[i].ID, FunctionArgsSets);
                            Tmp = string.Concat(ListValues);
                            FunctionCommands[i].Data[1] = FunctionCommands[i].Data[1].Replace(DelimList[k], Tmp);
                        }
                    }
                }
            }
            return FunctionCommands;
        }

        static void CorrectIf(List<Command_t> commands)
        {
            // Check if there's an If command without its EndIf
            CheckForMissEndIf(commands);
            if (error)
            {
                return;
            }

            // Convert all the If commands with logical and ("&&") to extra If+EndIf commands
            ConvertIfCommandsWithLogicalAnd(ref commands);

            // One E-code has a maximum of 0xFF lines that they can execute (the 'nn' parameter)
            // If the value is > 0xFF, create another copy of the If command (with its EndIf)
            // Very complicated, see if there's a better way
            List<Command_t> IfCommandsToAdd = new();
            int StartIndex = commands.FindIndex(item => item.Type == "IF");
            for (int i = StartIndex; i < commands.Count; i++)
            {
                int WeightCount = GetWeightCountFromIfIndex(commands, i);
                if (WeightCount > 0xFF)
                {
                    // If WeightCount is > 0xFF, we need to modify listCommands, so we run through all the commands and keep track of the CurrentWeight
                    // If WeightCount is > 0xFF:
                    //    Place as many as ENDIF commands as we collected (IfCommandsToAdd.Count)
                    //    Place all the IF commands we collected (IfCommandsToAdd)
                    // If we encounter an IF command, add it to the IfCommandsToAdd list
                    //    But if WeightCount is equal to 255, then we need to place the extra IF+ENDIF commands *before* this last If command
                    // If we encounter an ENDIF command, remove the last entry in the IfCommandsToAdd list

                    IfCommandsToAdd.Add(commands[i]);
                    WeightCount = 0;
                    for (int j = i + 1; j < commands.Count; j++)
                    {
                        WeightCount += commands[j].Weight;
                        if (WeightCount > 0xFF)
                        {
                            for (int k = 0; k < IfCommandsToAdd.Count; k++)
                            {
                                commands.Insert(j + k, new("ENDIF"));
                            }
                            j += IfCommandsToAdd.Count;

                            for (int k = 0; k < IfCommandsToAdd.Count; k++)
                            {
                                commands.Insert(j + k, IfCommandsToAdd[k].DeepClone());
                            }
                            break;
                        }
                        else if (commands[j].Type == "IF")
                        {
                            if (WeightCount != 255)
                            {
                                IfCommandsToAdd.Add(commands[j]);
                            }
                            else
                            {
                                j--;
                            }
                        }
                        else if (commands[j].Type == "ENDIF")
                        {
                            IfCommandsToAdd.RemoveAt(IfCommandsToAdd.Count - 1);
                        }
                    }

                    IfCommandsToAdd.Clear();
                }

                i = commands.FindIndex(i + 1, item => item.Type == "IF");
                if (i == -1)
                {
                    // No more if commands
                    break;
                }
                i--; // will get increased in the i++ in the for loop instruciton
            }

            // Replaces the 'nn' parameter in the E codes with the correct value.
            var matches = commands.Select((item, index) => new { Item = item, Index = index })
                                  .Where(item => item.Item.Type == "IF").ToList();

            for (int i = 0; i < matches.Count; i++)
            {
                int CurrentWeight = GetWeightCountFromIfIndex(commands, matches[i].Index);
                // if (CurrentWeight > 0xFF)
                // {
                //     // We should never end up here
                // }
                matches[i].Item.Output = Regex.Replace(matches[i].Item.Output!, "nn", CurrentWeight.ToString("X2"));
            }
        }

        static void CheckForMissEndIf(List<Command_t> commands)
        {
            var IfIndex = commands.FindIndex(item => item.Type == "IF");
            while (IfIndex != -1)
            {
                int SkipEndIfCount = 0;
                bool MissEndIf = true;

                // Find matched ENDIF
                for (int j = IfIndex + 1; j < commands.Count; j++)
                {
                    if (commands[j].Type == "IF")
                    {
                        SkipEndIfCount++;
                        continue;
                    }
                    else if (commands[j].Type == "ENDIF")
                    {
                        if (SkipEndIfCount == 0)
                        {
                            // Found the matched ENDIF
                            MissEndIf = false;
                            break;
                        }
                        SkipEndIfCount--;
                    }
                }

                if (MissEndIf)
                {
                    SetError(ERROR.MISS_ENDIF, commands[IfIndex]);
                    return;
                }

                // Jump to the next if
                IfIndex = commands.FindIndex(IfIndex + 1, item => item.Type == "IF");
            }
        }

        static void ConvertIfCommandsWithLogicalAnd(ref List<Command_t> commands)
        {
            var IfIndex = commands.FindIndex(item => item.Type == "IF" && item.FullLine.Contains("&&"));

            while (IfIndex != -1)
            {
                int ExtraIfCount = commands[IfIndex].FullLine.Split("&&", StringSplitOptions.None).Length - 1;
                for (int i = 0; i < ExtraIfCount; i++)
                {
                    // Add more IF commands
                    Command_t ExtraIf = commands[IfIndex].DeepClone();

                    // Remove data already parsed and data that will get parsed in the next loop
                    int LogicalAndIndex = GetNthOccuranceInList(ExtraIf.Data, "&&", i);
                    ExtraIf.Data.RemoveRange(0, LogicalAndIndex + 1);
                    LogicalAndIndex = ExtraIf.Data.IndexOf("&&");
                    if (LogicalAndIndex != -1)
                    {
                        ExtraIf.Data.RemoveRange(LogicalAndIndex, ExtraIf.Data.Count - LogicalAndIndex);
                    }

                    ExtraIf.Output = HandleIf(ExtraIf);
                    commands.Insert(IfIndex + 1 + i, ExtraIf);
                }

                // Find matched ENDIF to add more ENDIF commands
                int SkipEndIfCount = 0;
                for (int i = IfIndex + ExtraIfCount + 1; i < commands.Count; i++)
                {
                    if (commands[i].Type == "IF")
                    {
                        SkipEndIfCount++;
                        continue;
                    }
                    else if (commands[i].Type == "ENDIF")
                    {
                        if (SkipEndIfCount == 0)
                        {
                            // Found the matched ENDIF
                            for (int j = 0; j < ExtraIfCount; j++)
                            {
                                commands.Insert(i + j, commands[i]);
                            }
                            break;
                        }
                        SkipEndIfCount--;
                    }
                }

                // Jump to the next if with logical and
                IfIndex = commands.FindIndex(IfIndex + ExtraIfCount + 1, item => item.Type == "IF" && item.FullLine.Contains("&&"));
            }
        }

        static int GetWeightCountFromIfIndex(List<Command_t> commands, int ifIndex)
        {
            // Get the WeightCount in an if scope (between an if and its matched endif)
            int SkipEndIfCount = 0;
            int CurrentWeight = 0;
            for (int i = ifIndex + 1; i < commands.Count; i++)
            {
                CurrentWeight += commands[i].Weight;

                if (commands[i].Type == "IF")
                {
                    SkipEndIfCount++;
                    continue;
                }
                else if (commands[i].Type == "ENDIF")
                {
                    if (SkipEndIfCount == 0)
                    {
                        // Found the matched ENDIF
                        return CurrentWeight;
                    }
                    SkipEndIfCount--;
                }
            }
            return 0;
        }

        static void GetListSets(List<Command_t> commands)
        {
            bool InsideFunction = false;
            foreach (var command in commands)
            {
                if (command.Type == "FUNCTION")
                {
                    InsideFunction = true;
                }
                else if (command.Type == "ENDFUNCTION")
                {
                    InsideFunction = false;
                }
                else if (command.Type == "SET" && !InsideFunction)
                {
                    listSets.Add(HandleSet(command));
                    listSets.Last().ID = command.ID;
                    if (error)
                    {
                        break;
                    }
                }
            }
        }

        static void ReplaceInclude(List<Command_t> commands)
        {
            List<Command_t> FilteredList = new();
            bool InsideFunction = false;
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].Type == "FUNCTION")
                {
                    InsideFunction = true;
                }
                else if (commands[i].Type == "ENDFUNCTION")
                {
                    InsideFunction = false;
                }
                else if (commands[i].Type == "INCLUDE" && !InsideFunction)
                {
                    var IncludeCommands = HandleInclude(commands[i]);
                    IncludeCommands.ForEach(item => item.AppendToTraceback(commands[i].Traceback));
                    if (error)
                    {
                        return;
                    }
                    commands.RemoveAt(i); // Remove the "Include" command
                    commands.InsertRange(i, IncludeCommands); // Add all the commands found in the included file
                    i--; // Start from the 1st included command
                }
            }
        }

        static void GetFunctionList(List<Command_t> commands)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                int FunctionIndex = commands.FindIndex(i, item => item.Type == "FUNCTION");
                int EndFunctionIndex = commands.FindIndex(FunctionIndex + 1, item => item.Type == "ENDFUNCTION");

                if (FunctionIndex == -1 && EndFunctionIndex == -1)
                {
                    break;
                }
                else if (FunctionIndex == -1 || EndFunctionIndex == -1)
                {
                    if (FunctionIndex != -1)
                    {
                        // miss endfunction
                        SetError(ERROR.MISS_ENDFUNCTION, commands[FunctionIndex]);
                    }
                    else
                    {
                        // has endfunction but no start function
                        SetError(ERROR.MISS_FUNCTION, commands[EndFunctionIndex]);
                    }
                    break;
                }

                HandleFunction(commands[FunctionIndex]);
                if (error)
                {
                    break;
                }

                for (int j = FunctionIndex + 1; j < EndFunctionIndex; j++)
                {
                    if (commands[j].Type == "FUNCTION")
                    {
                        SetError(ERROR.FUNCTION_COMMAND_INSIDE_FUNCTION_DEFINITION, commands[j]);
                        break;
                    }
                    else if (commands[j].Type == "INCLUDE")
                    {
                        SetError(ERROR.INCLUDE_COMMAND_INSIDE_FUNCTION_DEFINITION, commands[j]);
                        break;
                    }
                    _functionList.Last().Commands!.Add(commands[j]);
                }
                if (error)
                {
                    break;
                }

                commands.RemoveRange(FunctionIndex, EndFunctionIndex - FunctionIndex + 1);
                i = FunctionIndex - 1;
            }
        }

        static void ReplaceCall(ref List<Command_t> commands)
        {
            int CallIndex = commands.FindIndex(item => item.Type == "CALL");
            while (CallIndex != -1)
            {
                Command_t CallCommand = commands[CallIndex];
                List<Command_t> CalledCommands = HandleCall(CallCommand);

                int SameCallNameIndex = CalledCommands.FindIndex(item => item.Type == "CALL" && item.Data[0] == CallCommand.Data[0]);
                if (SameCallNameIndex != -1)
                {
                    SetError(ERROR.CALL_STACK_OVERFLOW, CalledCommands[SameCallNameIndex]);
                }
                if (error)
                {
                    return;
                }

                commands.RemoveAt(CallIndex);
                commands.InsertRange(CallIndex, CalledCommands);
                commands = commands.Select((item, index) => { item.ID = index; return item; }).ToList();
                CallIndex = commands.FindIndex(CallIndex, item => item.Type == "CALL");
            }
        }

        // Default, use the global listSets
        public static Command_t ApplySetsToCommand(Command_t command)
        {
            command = ApplySetsToCommand(command, listSets);
            return command;
        }

        // Use a custom listSets, (used when handling function arguments)
        static Command_t ApplySetsToCommand(Command_t command, List<LocalVar_t> listSets)
        {
            for (int i = 0; i < command.Data.Count; i++)
            {
                string Target = command.Data[i];
                if (Target.StartsWith('+') || Target.StartsWith(','))
                {
                    // "+off" = "off"
                    // "+  off" = "off"
                    Target = Target.Substring(1).TrimStart();
                }

                if (IsVarDeclared(Target, listSets))
                {
                    List<string> ListValues = GetSetValuesFromTarget(Target, command.ID, listSets);

                    // Add + or , for the first element in the list of values
                    // For example:
                    //  Set test 8+4
                    //  Write32 FB1580+off
                    // "off" is going to be replaced with "8+4", but because it's the second term of a larger expression (FB1580+off), we add the + back
                    if (command.Data[i].StartsWith('+') || command.Data[i].StartsWith(','))
                    {
                        // "8" = "+8"
                        ListValues[0] = command.Data[i][0] + ListValues[0];
                    }

                    // Trim space after + or ,
                    for (int j = 1; j < ListValues.Count; j++)
                    {
                        if (ListValues[j].StartsWith('+') || ListValues[j].StartsWith(','))
                        {
                            ListValues[j] = ListValues[j][0] + ListValues[j].Substring(1).TrimStart();
                        }
                    }

                    command.Data.RemoveAt(i);
                    command.Data.InsertRange(i, ListValues);
                    i = i + ListValues.Count - 1;
                }
                else if (command.Data[i].StartsWith('+') || command.Data[i].StartsWith(','))
                {
                    // If it started with + or , then add it back (we removed whitespace earlier)
                    command.Data[i] = command.Data[i][0] + Target;
                }
            }
            return command;
        }

        // Use a custom listSets, (used when handling function arguments)
        static void ApplySetsToCommands(List<Command_t> commands, List<LocalVar_t> listSets)
        {
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i] = ApplySetsToCommand(commands[i], listSets);
            }
        }

        static List<string> GetCallArgsFromCommand(Command_t command)
        {
            // I don't know if there is a better way. This works and it's simple enough.
            List<string> CallArgs = new();
            StringBuilder CurrentElement = new();
            bool IsInsideString = false;
            bool IsEscape = false;
            string Tmp = command.Data[1].TrimStart('(').TrimEnd(')');

            foreach (char ch in Tmp)
            {
                if (ch == ',' && !IsInsideString)
                {
                    // Comma outside of a string, add the current element to the result
                    CallArgs.Add(CurrentElement.ToString());
                    CurrentElement.Clear();
                    continue;
                }
                else if (ch == '\\' && IsInsideString)
                {
                    // Handle escape character inside a string
                    IsEscape = true;
                }
                else if (ch == '\"' && !IsEscape)
                {
                    // Toggle the insideString flag when encountering a single quote
                    IsInsideString = !IsInsideString;
                }
                else
                {
                    if (!IsInsideString && (ch == '(' || ch == ')'))
                    {
                        SetError(ERROR.WRONG_SYNTAX, command);
                        break;
                    }
                    // Add the character to the current element
                    IsEscape = false;
                }
                CurrentElement.Append(ch);
            }

            CallArgs.Add(CurrentElement.ToString());
            CallArgs = CallArgs.Select(item => item.Trim()).ToList();
            return CallArgs;
        }

        public static string GetAddress(Command_t command, ref int i)
        {
            int Tmp = GetAddressAsInt(command, ref i);
            if (error)
            {
                return "";
            }

            string Address = Tmp.ToString("X8").Substring(1);
            return Address;
        }

        static int GetAddressAsInt(Command_t command, ref int i)
        {
            if (!IsAddressValid(command.Data[i]))
            {
                SetError(ERROR.ADDRESS_INVALID, command);
                return 0;
            }

            int Tmp = ConvertAddress(command.Data[i]);

            for (i = i + 1; i < command.Data.Count; i++)
            {
                if (!command.Data[i].StartsWith('+'))
                {
                    break;
                }

                // Check if it's a valid hex number without the '+'
                var partWithoutPlus = command.Data[i].Substring(1);
                if (!IsAddressValid(partWithoutPlus))
                {
                    SetError(ERROR.ADDRESS_INVALID, command);
                    return 0;
                }
                Tmp += ConvertAddress(partWithoutPlus);
            }

            return Tmp;
        }

        static List<string> GetAddresses(Command_t command, ref int i)
        {
            List<string> ListOffs = new();
            if (!IsAddressValid(command.Data[i]))
            {
                SetError(ERROR.ADDRESS_INVALID, command);
                return [];
            }
            int TmpAddress = ConvertAddress(command.Data[i]);

            for (i = i + 1; i < command.Data.Count; i++)
            {
                if (command.Data[i].StartsWith(','))
                {
                    ListOffs.Add(TmpAddress.ToString("X8"));
                    TmpAddress = 0;
                }
                else if (!command.Data[i].StartsWith('+'))
                {
                    ListOffs.Add(TmpAddress.ToString("X8"));
                    TmpAddress = 0;
                    break;
                }

                if (!IsAddressValid(command.Data[i].Substring(1)))
                {
                    SetError(ERROR.ADDRESS_INVALID, command);
                    return [];
                }
                TmpAddress += ConvertAddress(command.Data[i].Substring(1));
            }

            if (TmpAddress != 0)
            {
                ListOffs.Add(TmpAddress.ToString("X8"));
            }
            ListOffs[0] = ListOffs[0].Substring(1);
            return ListOffs;
        }

        static string GetIntValue(Command_t command, ref int i)
        {
            int Tmp = GetIntValueAsInt(command, ref i);
            if (error)
            {
                return "";
            }

            string Value = Tmp.ToString("X8");
            return Value;
        }

        static int GetIntValueAsInt(Command_t command, ref int i)
        {
            if (!IsIntValueValid(command.Data[i]))
            {
                SetError(ERROR.VALUE_INVALID, command);
                return 0;
            }
            int Tmp = ConvertIntValue(command.Data[i]);

            for (i = i + 1; i < command.Data.Count; i++)
            {
                if (!command.Data[i].StartsWith('+'))
                {
                    break;
                }
                if (!IsIntValueValid(command.Data[i].Substring(1)))
                {
                    SetError(ERROR.VALUE_INVALID, command);
                    return 0;
                }
                Tmp += ConvertIntValue(command.Data[i].Substring(1));
            }

            return Tmp;
        }

        static string GetFloatValue(Command_t command, ref int i)
        {
            if (!IsFloatValueValid(command.Data[i]))
            {
                SetError(ERROR.VALUE_INVALID, command);
                return "";
            }
            int Output = ConvertFloatValue(command.Data[i]);
            List<string> ListHexValues = new();

            for (i = i + 1; i < command.Data.Count; i++)
            {
                if (!command.Data[i].StartsWith('+'))
                {
                    break;
                }

                string CurrentValue = command.Data[i].Substring(1);
                if (!IsFloatValueValid(CurrentValue))
                {
                    SetError(ERROR.VALUE_INVALID, command);
                    return "";
                }
                if (CurrentValue.StartsWith("0x") || CurrentValue.StartsWith("-0x"))
                {
                    ListHexValues.Add(CurrentValue);
                    continue;
                }

                // Sum the values as float (0x3F800000 (1) + 0x3F800000 (1) = 0x40000000 (2))
                float Sum = BitConverter.ToSingle(BitConverter.GetBytes(ConvertFloatValue(CurrentValue))) + BitConverter.ToSingle(BitConverter.GetBytes(Output));
                Output = BitConverter.ToInt32(BitConverter.GetBytes(Sum));
            }

            // Sum the values as int (0x3F800000 + 0x1 = 0x3F800001)
            Output += ListHexValues.Sum(ConvertIntValue);

            return Output.ToString("X8");
        }

        static string GetStringValue(Command_t command, ref int i)
        {
            string Value = "";
            uint Sum = 0;
            if (command.Data[i].StartsWith('"'))
            {
                // string
                Value = command.Data[i];
            }
            else
            {
                // value
                if (!IsIntValueValid(command.Data[i]))
                {
                    SetError(ERROR.VALUE_INVALID, command);
                    return "";
                }
                Sum += ConvertUIntValue(command.Data[i]);
            }

            for (i = i + 1; i < command.Data.Count; i++)
            {
                string Element = command.Data[i];
                if (!Element.StartsWith('+') || Element == "+")
                {
                    // without +, or just '+'
                    SetError(ERROR.WRONG_SYNTAX, command);
                    return "";
                }

                Element = Element.Substring(1);
                if (Element[0] == '"')
                {
                    // string
                    if (Sum > 0)
                    {
                        if (Value.EndsWith('"'))
                        {
                            Value = Value.TrimEnd('"') + Sum.ToString() + '"';
                        }
                        else
                        {
                            Value = Value + Sum.ToString();
                        }
                        Sum = 0;
                    }

                    if (!StartsAndEndsWithQuotes(Element))
                    {
                        SetError(ERROR.MISSING_QUOTES, command);
                        return "";
                    }
                    string Tmp = GetSubstringInQuotes(Element, false);
                    if (Value.EndsWith('"'))
                    {
                        Value = Value.TrimEnd('"') + Tmp + '"';
                    }
                    else
                    {
                        Value = Value + Tmp;
                    }
                }
                else
                {
                    // value
                    if (!IsIntValueValid(Element))
                    {
                        SetError(ERROR.VALUE_INVALID, command);
                        return "";
                    }
                    Sum += ConvertUIntValue(Element);
                }
            }

            if (Sum > 0)
            {
                if (Value.EndsWith('"'))
                {
                    Value = Value.TrimEnd('"') + Sum.ToString() + '"';
                }
                else
                {
                    Value = Value + Sum.ToString();
                }
            }
            if (StartsAndEndsWithQuotes(Value))
            {
                Value = GetSubstringInQuotes(Value, false);
            }

            return Value;
        }

        static string GetRemainingOffsets(List<string> listOffs, out int weight)
        {
            string Output = "";
            weight = 0;
            int RemainingOffs = listOffs.Count - 2; // - base and first offset
            for (int i = 0; i < RemainingOffs; i++)
            {
                if (i % 2 == 0)
                {
                    weight += 1;
                    Output += newLine;
                }
                else
                {
                    Output += " ";
                }
                Output += listOffs[i + 2];
            }

            if (RemainingOffs % 2 == 1)
            {
                Output += " 00000000";
            }
            return Output;
        }

        static void PopulateAbbreviationDict()
        {
            _commandsDict.Add("SE", "SETENCODING");
            _commandsDict.Add("SR", "SENDRAW");
            _commandsDict.Add("SRW", "SENDRAWWEIGHT");
            _commandsDict.Add("W8", "WRITE8");
            _commandsDict.Add("W16", "WRITE16");
            _commandsDict.Add("W32", "WRITE32");
            _commandsDict.Add("WF", "WRITEFLOAT");
            _commandsDict.Add("WS", "WRITESTRING");
            _commandsDict.Add("WB", "WRITEBYTES");
            _commandsDict.Add("WP8", "WRITEPOINTER8");
            _commandsDict.Add("WP16", "WRITEPOINTER16");
            _commandsDict.Add("WP32", "WRITEPOINTER32");
            _commandsDict.Add("WPF", "WRITEPOINTERFLOAT");
            _commandsDict.Add("CB", "COPYBYTES");
            _commandsDict.Add("F8", "FILL8");
            _commandsDict.Add("F16", "FILL16");
            _commandsDict.Add("F32", "FILL32");
            _commandsDict.Add("I8", "INCREMENT8");
            _commandsDict.Add("I16", "INCREMENT16");
            _commandsDict.Add("I32", "INCREMENT32");
            _commandsDict.Add("D8", "DECREMENT8");
            _commandsDict.Add("D16", "DECREMENT16");
            _commandsDict.Add("D32", "DECREMENT32");
            _commandsDict.Add("EI", "ENDIF");
        }

        public static void SetError(ERROR errorValue, Command_t command)
        {
            error = true;
            _errorInfo.ErrorValue = errorValue;
            _errorInfo.Command = command;
        }
    }
}
