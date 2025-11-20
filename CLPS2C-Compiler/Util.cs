using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CLPS2C_Compiler
{
    public class Util
    {
        // string PatternString = @"(""(?:[^""\\]|\\.)*""*)";
        // string PatternString2 = @"(""(\\.|[^""])*""*)";
        // string PatternString3 = @"(""(?:\\.|[^""])*""?)";

        [DebuggerDisplay("({ID}) | {FullLine} = {Output}")]
        public class Command_t
        {
            [DebuggerDisplay("{System.IO.Path.GetFileName(FilePath)}:{LineIdx} - {FullLine}")]
            public class Traceback_t
            {
                public string? FilePath;
                public string? FullLine;
                public int LineIdx;
            }

            public string FullLine;
            public int ID;
            public int Weight; // How many lines the command produced. Will get set when parsing the command. Some commands might have a weight of 0
            public string Type;
            public List<string> Data;
            public string? Output;
            public List<Traceback_t> Traceback;
            public int WordCount { get => Data.Count + 1; }

            public Command_t(string line)
            {
                Weight = 0;
                FullLine = line;
                Traceback = new();

                // (\(.*\))|("(?:\\.|[^"])*"?)|(\+\s*(("(?:\\.|[^"])*"?)|([^+"]+?))?(?=\+|$| |,))|(,("?) *([^,"]+?)\8(?=,|$| |\+))|([^ \t\n\r\f\v(+,]+)
                // Order of precedence:
                // (\(.*\)) - In parenthesis, this is one element: (test "test" test, test)
                // ("(?:\\.|[^"])*"?) - In double quotes, taking into account escaped ". this is one element: "test \"test\" test"
                // (\+\s*(("(?:\\.|[^"])*"?)|([^+"]+?))?(?=\+|$| |,)) - + symbol, any white space, string (see above) or word. With the ? before the last group so that it matches '+'
                // In the following: +1+2+3+4+ 5+"6+   7"+    8
                // The elements are +1,+2,+3,+4,+ 5,+"6+   7",+    8
                // (,("?) *([^,"]+?)\8(?=,|$| |\+)) - Separated by comma, with any white space after the comma
                // ([^ \t\n\r\f\v(+,]+) - Any other word. This is \S, and '(', '+', ','
                MatchCollection Matches = Regex.Matches(line, @"(\(.*\))|(""(?:\\.|[^""])*""?)|(\+\s*((""(?:\\.|[^""])*""?)|([^+""]+?))?(?=\+|$| |,))|(,(""?) *([^,""]+?)\8(?=,|$| |\+))|([^ \t\n\r\f\v(+,]+)");
                Type = Matches[0].Value.ToUpper();
                Data = Matches.Skip(1).Take(Matches.Count - 1).Select(item => item.Value).ToList(); // Data has every word except first
            }

            public Command_t(string line, Command_t sourceCommand, bool replaceTypeAndData = false)
            {
                var tmp = new Command_t(line);
                ID = sourceCommand.ID;
                FullLine = sourceCommand.FullLine;
                Traceback = sourceCommand.Traceback;
                Type = tmp.Type;
                // Data = tmp.Data;

                if (replaceTypeAndData)
                {
                    //Type = sourceCommand.Data[0].ToUpper();
                    Data = sourceCommand.Data.Skip(1).ToList();
                }
                else
                {
                    //Type = sourceCommand.Type;
                    //Data = sourceCommand.Data;

                    Data = tmp.Data;
                }
            }

            public void AppendToTraceback(string file, string fullLine, int lineIdx)
            {
                Traceback.Add(new Traceback_t
                {
                    FilePath = file,
                    FullLine = fullLine,
                    LineIdx = lineIdx
                });
            }

            public void AppendToTraceback(List<Traceback_t> traceback)
            {
                Traceback.AddRange(traceback);
            }

            public Command_t DeepClone()
            {
                Command_t Clone = new(FullLine)
                {
                    Weight = Weight,
                    Type = Type,
                    Data = Data.Select(item => item).ToList(), // Deep clone the list
                    Output = Output,
                    Traceback = Traceback.Select(item => item).ToList()
                };

                return Clone;
            }
        }

        [DebuggerDisplay("({ID}) | {Name} = {string.Join(\", \", Values)}")]
        public class LocalVar_t
        {
            public string Name;
            public List<string> Values;
            public int ID;

            public LocalVar_t(string name, List<string> values)
            {
                Name = name;
                Values = values;
            }
        }

        [DebuggerDisplay("{ThisCommand.FullLine}")]
        public class Function_t
        {
            public string? Name; // WriteXYZ
            public List<string>? Args; // base, valueX, valueY, valueZ
            public Command_t? ThisCommand;
            public List<Command_t>? Commands; // WritePointerFloat base,0x30 valueX | WritePointerFloat base,0x34 valueY | WritePointerFloat base,0x38 valueZ

            public Function_t(Command_t currentCommand)
            {
                Match Match = Regex.Match(currentCommand.FullLine, @"Function (\w+)\(([^)]*)\)", RegexOptions.IgnoreCase);
                
                // Wrong syntax
                if (!Match.Success)
                {
                    return;
                }

                ThisCommand = currentCommand;
                Name = Match.Groups[1].Value;
                Args = new();
                Commands = new();
                if (Match.Groups[2].Value != "")
                {
                    Args.AddRange(Match.Groups[2].Value.Split(',').Select(elementName => elementName.Trim()));
                }
            }
        }

        public class ErrorInfo_t
        {
            public Command_t? Command;
            public ERROR ErrorValue;
        }

        public static List<string> TextCleanUp(List<string> lines)
        {
            lines = lines.Select(x => x.Replace("\t", "")).ToList(); // remove \t
            lines = RemoveComments(lines);
            lines = lines.Select(x => x.Trim()).ToList(); // remove white spaces at the beginning and end (including \r\n)
            return lines;
        }

        public static List<string> RemoveComments(List<string> lines)
        {
            string Code = string.Join(Program.newLine, lines);
            Code = RemoveMultiLineComments(Code);
            Code = RemoveSingleLineComments(Code);
            lines = Code.Split(new[] { Program.newLine }, StringSplitOptions.None).ToList();
            return lines;
        }

        public static string RemoveMultiLineComments(string code)
        {
            // Preserve the original line indices (by keeping the line breaks).
            // ("(?:\\.|[^"])*"?)|(\/\*.*?(?:\*\/|$))
            // ("(?:\\.|[^"])*"?) - Match strings (so we can ignore them)
            // (\/\*.*?(?:\*\/|$)) - Match /* anything */ - */ can be absent
            // With the ? quantifier at the end of the first group so that it matches: "test""what" and "test
            Regex MultiLineCommentRegex = new(@"(""(?:\\.|[^""])*""?)|(\/\*.*?(?:\*\/|$))", RegexOptions.Singleline);
            code = MultiLineCommentRegex.Replace(code, match =>
            {
                if (match.Groups[1].Success)
                {
                    // It's a string, return the matched string as it is
                    return match.Value;
                }
                else
                {
                    // It's a comment, count new lines
                    int newLinesCount = match.Value.Split(Program.newLine).Length - 1;
                    return string.Concat(Enumerable.Repeat(Program.newLine, newLinesCount));
                }
            });

            return code;
        }

        public static string RemoveSingleLineComments(string code)
        {
            // https://www.reddit.com/r/regex/comments/g86xrg/comment/folxneo
            // With the ? quantifier at the end so that it matches the string "//test" with no new line after
            // With the ? quantifier at the end of the first group so that it matches: "test""what" and "test
            // ("(?:\\.|[^"])*"?)|(\/\/.*(?:\r\n|\n)?)
            Regex SingleLineCommentRegex = new(@"(""(?:\\.|[^""])*""?)|(\/\/.*(?:\r\n|\n)?)");
            code = SingleLineCommentRegex.Replace(code, match =>
            {
                if (match.Groups[1].Success)
                {
                    // It's a string, return the matched string as it is
                    return match.Value;
                }
                else
                {
                    // It's a comment
                    if (match.Value.EndsWith(Program.newLine))
                    {
                        return Program.newLine;
                    }
                    return ""; // If the comment doesn't end with a new line (for example, when the comment is at the last line)
                }
            });

            return code;
        }

        public static string PrintError(ErrorInfo_t errorInfo)
        {
            var version = Assembly.GetExecutingAssembly()
                              .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                              ?.InformationalVersion;
            if (version!.Contains('+'))
            {
                version = version!.Split('+')[0];
            }

            string Tmp = $"CLPS2C-Compiler v{version} {DateTime.Now}{Program.newLine}{Program.newLine}";
            Tmp += $"ERROR: ";
            Tmp += $"{Enum.GetName(errorInfo.ErrorValue)}";
            Tmp += $" at line {errorInfo.Command!.Traceback.First().LineIdx + 1}";
            Tmp += $" in file {errorInfo.Command.Traceback.First().FilePath}{Program.newLine}";
            Tmp += $"{ErrorMessages[errorInfo.ErrorValue]}{Program.newLine}{Program.newLine}";
            
            Tmp += $"Line that produced the error:{Program.newLine}{errorInfo.Command!.FullLine}";

            Tmp += $"{Program.newLine}{Program.newLine}Traceback:";
            errorInfo.Command.Traceback.Reverse();
            foreach (Command_t.Traceback_t item in errorInfo.Command.Traceback)
            {
                Tmp += $"{Program.newLine}at {item.FilePath}:{item.LineIdx + 1} - {item.FullLine}";
            }

            return Tmp;
        }

        public static void ConsolePrintError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"ERROR: ");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(text);
        }

        public static int CountCharInRange(string str, char ch, int startIdx, int endIdx)
        {
            // Returns the count of how many chars 'ch' there're between 2 index positions in a string
            if (startIdx == -1 || endIdx == -1)
            {
                return -1;
            }
            string Substring = str.Substring(startIdx, endIdx - startIdx);
            return Substring.Count(c => c == ch);
        }

        public static string GetSubstringInQuotes(string line, bool includeQuotes)
        {
            // "\"park\"" -> "park"
            // "\"00 11 22 33\"" -> "00 11 22 33"
            var First = line.IndexOf('\"');
            var Second = line.LastIndexOf('\"');
            if (includeQuotes)
            {
                return line.Substring(First, Second - First + 1); // With '\"'
            }
            return line.Substring(First + 1, Second - First - 1); // Without '\"'
        }

        public static List<string> GetDelimitatedPlusString(string input)
        {
            List<string> Elements = new();
            MatchCollection Matches = Regex.Matches(input, @"(\+\s*((""(?:\\.|[^""])*""?)|([^+""]+?))(?=\+|$| |,))|([^ \t\n\r\f\v(+,]+)");
            for (int i = 0; i < Matches.Count; i++)
            {
                Elements.Add(Matches[i].Value);
            }

            return Elements;
        }

        public static int GetNthOccuranceInList(List<string> list, string value, int n)
        {
            int tmp = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    if (tmp == n)
                    {
                        // found
                        return i;
                    }
                    tmp++;
                }
            }
            return -1;
        }

        public static int GetNthOccuranceInString(string input, char value, int n)
        {
            int tmp = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == value)
                {
                    if (tmp == n)
                    {
                        // found
                        return i;
                    }
                    tmp++;
                }
            }
            return -1;
        }

        public static bool StartsAndEndsWithQuotes(string value)
        {
            if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length != 1)
            {
                return true;
            }
            return false;
        }

        public static string GetAddressValuePairsFromAOB(byte[] array, string startAddress)
        {
            int W32Count = array.Length / 4; // How many W32
            int WXCount = array.Length % 4; // How many writes remain (W8 + W16)
            string Output = "";
            uint Address = Convert.ToUInt32(startAddress, 16);

            for (int i = 0; i < W32Count; i++)
            {
                string address = $"2{((int)(Address + i * 4)).ToString("X8").Substring(1)}";
                string value = array[(i + 1) * 4 - 1].ToString("X2") +
                               array[(i + 1) * 4 - 2].ToString("X2") +
                               array[(i + 1) * 4 - 3].ToString("X2") +
                               array[(i + 1) * 4 - 4].ToString("X2");
                Output += $"{Program.newLine}{address} {value}";
            }

            switch (WXCount)
            {
                case 0:
                    break;
                case 1:
                    // W8
                    Output += $"{Program.newLine}0{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"000000{array[array.Length - 1].ToString("X2")}";
                    break;
                case 2:
                    // W16
                    Output += $"{Program.newLine}1{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"0000{array[array.Length - 1].ToString("X2") + array[array.Length - 2].ToString("X2")}";
                    break;
                case 3:
                    // W16 + W8
                    Output += $"{Program.newLine}1{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"0000{array[array.Length - 2].ToString("X2")}{array[array.Length - 3].ToString("X2")}";
                    Output += $"{Program.newLine}0{((int)(Address + W32Count * 4 + 2)).ToString("X8").Substring(1)} ";
                    Output += $"000000{array[array.Length - 1].ToString("X2")}";
                    break;
            }
            return Output;
        }

        public static bool IsAOBValid(string value)
        {
            return Regex.IsMatch(value, @"^([0-9A-Fa-f]{2} )*[0-9A-Fa-f]{2}$");
        }

        public static bool IsAddressValid(string address)
        {
            return Regex.IsMatch(address, @"^-?(0x)?[0-9A-Fa-f]{1,8}$");
        }

        public static bool IsIntValueValid(string value)
        {
            return Regex.IsMatch(value, @"^-?(0x){1}[0-9A-Fa-f]{1,8}$|^-?(?!0x)\d+$");
        }

        public static bool IsFloatValueValid(string value)
        {
            if (!Regex.IsMatch(value, @"^-?(0x){1}[0-9A-Fa-f]{1,8}$|^-?(?!0x)\d+(\.\d+)?$"))
            {
                value = value.ToUpper();
                return value == "INFINITY" || value == "-INFINITY" || value == "NAN";
            }
            return true;
        }

        public static int ConvertAddress(string input)
        {
            if (!input.StartsWith("-"))
            {
                //0x123A, 123A, 123
                return Convert.ToInt32(input, 16);
            }

            if (input.StartsWith("-0x"))
            {
                input = input.Substring(2);
            }

            input = input.Substring(1);
            return Convert.ToInt32(input, 16) * -1;
        }

        public static int ConvertIntValue(string input)
        {
            if (input.StartsWith("0x"))
            {
                //0x123A
                return Convert.ToInt32(input, 16);
            }

            if (input.StartsWith("-0x"))
            {
                //-0x123A
                return Convert.ToInt32(input.Substring(3), 16) * -1;
            }

            // 123, -123
            return Convert.ToInt32(input, 10);
        }

        public static uint ConvertUIntValue(string input)
        {
            if (input.StartsWith("0x"))
            {
                //0x123A
                return Convert.ToUInt32(input, 16);
            }

            if (input.StartsWith("-0x"))
            {
                //-0x123A
                return (uint)(Convert.ToUInt32(input.Substring(3), 16) * -1);
            }

            // 123, -123
            return Convert.ToUInt32(input, 10);
        }

        public static int ConvertFloatValue(string input)
        {
            if (input.StartsWith("0x"))
            {
                //0x123A
                return Convert.ToInt32(input, 16);
            }

            if (input.StartsWith("-0x"))
            {
                //-0x123A
                return (Convert.ToInt32(input.Substring(3), 16) * -1);
            }

            // 123, -123
            input = Convert.ToDouble(input, CultureInfo.InvariantCulture.NumberFormat).ToString();
            return BitConverter.ToInt32(BitConverter.GetBytes(Convert.ToSingle(input)), 0);
        }

        public static bool IsVarDeclared(string target, List<LocalVar_t> listSets)
        {
            // Checks in listSets if there's a match with a local var
            return listSets.Any(y => y.Name == target);
        }

        public static List<string> GetSetValuesFromTarget(string target, int id, List<LocalVar_t> listSets)
        {
            // Based on a string "target", find the closest "Set" (considering the id) in which the (NAME) parameter has the target value
            List<LocalVar_t> PossibleSets = listSets.Where(item => item.Name == target).ToList();
            var SetID = FindClosestLowerElement(PossibleSets, id);
            if (SetID == -1)
            {
                return [];
            }

            List<string> Output = PossibleSets.FirstOrDefault(item => item.ID == SetID)!.Values.ToList();
            for (int i = 0; i < Output.Count; i++)
            {
                string Element = Output[i];
                if (Element.StartsWith('+'))
                {
                    Element = Element.Substring(1).TrimStart();
                }

                if (IsVarDeclared(Element, listSets))
                {
                    List<string> RecursiveValues = GetSetValuesFromTarget(Element, SetID, listSets);
                    if (RecursiveValues.Count != 0)
                    {
                        if (Output[i].StartsWith('+'))
                        {
                            RecursiveValues[0] = "+" + RecursiveValues[0];
                        }
                        Output.RemoveAt(i);
                        Output.InsertRange(i, RecursiveValues);
                        i = i + RecursiveValues.Count - 1;
                    }
                }
            }
            return Output;
        }

        static int FindClosestLowerElement(List<LocalVar_t> listSets, int targetValue)
        {
            int closestLowerElement = -1;
            var q = listSets.Select(item => item.ID).ToList();

            foreach (int number in q)
            {
                int difference = targetValue - number;
                if (difference < 0)
                {
                    // not found based on the id
                    break;
                }
                closestLowerElement = number;
            }
            return closestLowerElement;
        }

        public static List<Command_t> GetListCommands(List<string> lines, string filePath)
        {
            List<Command_t> ListCommands = new();
            foreach (var line in lines.OfType<string>().Select((CurrentLine, CurrentLineIdx) => new { CurrentLine, CurrentLineIdx }))
            {
                if (line.CurrentLine == "")
                {
                    continue;
                }

                ListCommands.Add(new(line.CurrentLine));
                ListCommands.Last().AppendToTraceback(filePath, line.CurrentLine, line.CurrentLineIdx);
            }
            return ListCommands;
        }

        public static string ConvertRawToPnach(string source)
        {
            return Regex.Replace(source, @"([0-9A-F]{8}) ([0-9A-F]{8})", "patch=1,EE,$1,extended,$2");
        }

        public static bool IsFilePathValid(string path)
        {
            // q -> invalid
            // "C:\Users\admin\Desktop\CLPS2C\CLPS2C-Test" -> invalid
            // "C:\Users\admin\Desktop\CLPS2C\CLPS2C-Test\CLPS2C-Test.clps2c" -> valid
            if (Directory.Exists(path) || !File.Exists(path))
            {
                return false;
            }
            return true;
        }

        public static bool IsAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path) && !Path.GetPathRoot(path)!.Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }

        public enum ERROR : int
        {
            OK,
            UNKNOWN_COMMAND,
            WRONG_SYNTAX,
            ADDRESS_INVALID,
            VALUE_INVALID,
            MISS_ENDIF,
            MISS_ASM_START,
            MISS_ASM_END,
            MISS_FUNCTION,
            MISS_ENDFUNCTION,
            FUNCTION_COMMAND_INSIDE_FUNCTION_DEFINITION,
            FUNCTION_ALREADY_DEFINED,
            INCLUDE_COMMAND_INSIDE_FUNCTION_DEFINITION,
            INCLUDE_STACK_OVERFLOW,
            CALL_STACK_OVERFLOW,
            ARGUMENT_COUNT_MISMATCH,
            MISSING_QUOTES,
            LENGTH_MUST_BE_DIVISIBLE_BY_2,
            LENGTH_MUST_BE_DIVISIBLE_BY_4,

            ASM_UNKNOWN_MNEMONIC,
            ASM_WRONG_SYNTAX,
            ASM_UNDEFINED_LABEL,
            ASM_LABEL_ALREADY_DEFINED,
            ASM_UNKNOWN_REGISTER,
            ASM_WRONG_DEST,
            ASM_WRONG_INTERLOCK,
            ASM_IMMEDIATE_VALUE_INVALID,
            ASM_SHIFT_AMOUNT_VALUE_INVALID,
        }

        public static Dictionary<ERROR, string> ErrorMessages = new()
        {
            { ERROR.UNKNOWN_COMMAND, "An unknown command has been detected. Check the list of commands supported in the documentation -> \"List of commands\" section." },
            { ERROR.WRONG_SYNTAX, "There is something wrong with the command's syntax. Check the list of commands and their syntaxes in the documentation -> \"List of commands\" section." },
            { ERROR.ADDRESS_INVALID, "The (ADDRESS) argument was invalid. Check the conditions for a valid (ADDRESS) in the documentation -> \"Description and Settings\" section." },
            { ERROR.VALUE_INVALID, "The (VALUE) argument was invalid. Check the conditions for a valid (VALUE) in the documentation -> \"Description and Settings\" section." },
            { ERROR.MISS_ENDIF, "An \"If\" scope was opened but not closed. An \"If\" command must always have an \"EndIf\" command." },
            { ERROR.MISS_ASM_START, "An \"ASM_END\" command was detected but no matching \"ASM_START\" command is present. An \"ASM_END\" command must always have an \"ASM_START\" command." },
            { ERROR.MISS_ASM_END, "An \"ASM_START\" scope was opened but not closed. An \"ASM_START\" command must always have an \"ASM_END\" command." },
            { ERROR.MISS_FUNCTION, "An \"EndFunction\" command was detected but no matching \"Function\" command is present. An \"EndFunction\" command must always have a \"Function\" command." },
            { ERROR.MISS_ENDFUNCTION, "A \"Function\" scope was opened but not closed. A \"Function\" command must always have an \"EndFunction\" command." },
            { ERROR.FUNCTION_COMMAND_INSIDE_FUNCTION_DEFINITION, "A \"Function\" command was detected inside a \"Function\" definition. A \"Function\" scope must not have a \"Function\" command inside." },
            { ERROR.FUNCTION_ALREADY_DEFINED, "A \"Function\" command with the same (NAME) has already been defined. A function must not be defined more than once." },
            { ERROR.INCLUDE_COMMAND_INSIDE_FUNCTION_DEFINITION, "An \"Include\" command was detected inside a \"Function\" definition. A \"Function\" scope must not have an \"Include\" command inside." },
            { ERROR.INCLUDE_STACK_OVERFLOW, "An \"Include\" command was trying to include an already included file. This would create infinite recursion and result in a StackOverflowException.\nA file must not include itself, or include another file which includes one of the previous files." },
            { ERROR.CALL_STACK_OVERFLOW, "A \"Call\" command was trying to call the function in which it is defined. This would create infinite recursion and result in a StackOverflowException.\nRecursive functions are not allowed." },
            { ERROR.ARGUMENT_COUNT_MISMATCH, "The count of the arguments in the \"Function\" definition and the count of the arguments passed to the function with the \"Call\" command are not equal.\nThe count of the arguments in the \"Function\" command and the ones in the \"Call\" command must be equal." },
            { ERROR.MISSING_QUOTES, "The argument must start and end with quotes." },
            { ERROR.LENGTH_MUST_BE_DIVISIBLE_BY_2, "The length argument must be divisible by 2." },
            { ERROR.LENGTH_MUST_BE_DIVISIBLE_BY_4, "The length argument must be divisible by 4." },

            // EEAssembler
            { ERROR.ASM_UNKNOWN_MNEMONIC, "The assembler could not recognize an opcode. Note that not all opcodes supported by the PS2's MIPS R5900 CPU are supported in CLPS2C." },
            { ERROR.ASM_WRONG_SYNTAX, "The assembler could not assemble the instruction. Make sure its syntax is correct." },
            { ERROR.ASM_UNDEFINED_LABEL, "The assembler could not find a label. This usually occurs because there's a branch instruction to a label which has not been defined." },
            { ERROR.ASM_LABEL_ALREADY_DEFINED, "The assembler has already encountered a label with the same name. The same assembly scope can't have more than one label with the same name.\nNote that labels are case-insensetive." },
            { ERROR.ASM_UNKNOWN_REGISTER, "The assembler could not assemble the instruction because an unknown register has been detected. Make sure its syntax is correct." },
            { ERROR.ASM_WRONG_DEST, "The \"dest\" field is wrong. The \"dest\" field should have length between 1 and 4, in which the allowed letters are \"x\", \"y\", \"z\" and \"w\", and each letter must only appear once." },
            { ERROR.ASM_WRONG_INTERLOCK, "The \"interlock\" field is wrong. The \"interlock\" field should have length of 1 and the only allowed letter is \"i\"." },
            { ERROR.ASM_IMMEDIATE_VALUE_INVALID, "The \"immediate\" field is wrong." },
            { ERROR.ASM_SHIFT_AMOUNT_VALUE_INVALID, "The \"shift amount\" field is wrong. The \"shift amount\" field should be between 0x00 and 0x1F." },
        };
    }
}
