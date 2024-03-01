using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Keystone;

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

                // (\(.*\))|("(?:\\.|[^"])*"?)|(\+\s*(("(?:\\.|[^"])*"?)|([^+"]+?))?(?=\+|$| |,))|(,("?)([^,"]+?)\8(?=,|$| |\+))|([^ \t\n\r\f\v(+,]+)
                // Order of precedence:
                // (\(.*\)) - In parenthesis, this is one element: (test "test" test, test)
                // ("(?:\\.|[^"])*"?) - In double quotes, taking into account escaped ". this is one element: "test \"test\" test"
                // (\+\s*(("(?:\\.|[^"])*"?)|([^+"]+?))?(?=\+|$| |,)) - + symbol, any white space, string (see above) or word. With the ? before the last group so that it matches '+'
                // In the following: +1+2+3+4+ 5+"6+   7"+    8
                // The elements are +1,+2,+3,+4,+ 5,+"6+   7",+    8
                // (,("?)([^,"]+?)\8(?=,|$| |\+)) - Separated by comma
                // ([^ \t\n\r\f\v(+,]+) - Any other word. This is \S, and '(', '+', ','
                MatchCollection Matches = Regex.Matches(line, @"(\(.*\))|(""(?:\\.|[^""])*""?)|(\+\s*((""(?:\\.|[^""])*""?)|([^+""]+?))?(?=\+|$| |,))|(,(""?)([^,""]+?)\8(?=,|$| |\+))|([^ \t\n\r\f\v(+,]+)");
                Type = Matches[0].Value.ToUpper();
                Data = Matches.Skip(1).Take(Matches.Count - 1).Select(item => item.Value).ToList(); // Data has every word except first
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
                Match Match = Regex.Match(currentCommand.FullLine, @"Function (\w+)\(([^)]*)\)");
                
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
            public KeystoneError KeystoneErrorValue;
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
            string Code = string.Join(Program._newLine, lines);
            Code = RemoveMultiLineComments(Code);
            Code = RemoveSingleLineComments(Code);
            lines = Code.Split(new[] { Program._newLine }, StringSplitOptions.None).ToList();
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
                    int newLinesCount = match.Value.Split(Program._newLine).Length - 1;
                    return string.Concat(Enumerable.Repeat(Program._newLine, newLinesCount));
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
                    if (match.Value.EndsWith(Program._newLine))
                    {
                        return Program._newLine;
                    }
                    return ""; // If the comment doesn't end with a new line (for example, when the comment is at the last line)
                }
            });

            return code;
        }

        public static string PrintError(ErrorInfo_t errorInfo)
        {
            string Tmp = "ERROR: ";
            if (errorInfo.ErrorValue == ERROR.OK)
            {
                // Keystone error
                if (errorInfo.KeystoneErrorValue != KeystoneError.KS_ERR_OK)
                {
                    Tmp += $"{Engine.ErrorToString(errorInfo.KeystoneErrorValue)} at line {errorInfo.Command!.Traceback.First().LineIdx + 1} in file {errorInfo.Command.Traceback.First().FilePath}{Program._newLine}";

                    if (errorInfo.KeystoneErrorValue == KeystoneError.KS_ERR_ASM_SYMBOL_MISSING)
                    {
                        // b notDefinedLabel
                        // Tmp += $"The following symbol was referenced but it does not exist in the current context: {errorInfo.Command.FullLine}{Program._newLine}";
                        // Tmp += $"Line that produced the error:{Program._newLine}ASM_END";
                        Tmp += $"The Keystone engine could not find a symbol. This usually occurs because there's a branch/jump instruction to a label which has not been defined.";
                    }
                    else if (errorInfo.KeystoneErrorValue == KeystoneError.KS_ERR_ASM_MNEMONICFAIL) 
                    {
                        // lqw t1,0x002DE2F0 (no opcode); %li $t0,1 (% character)
                        Tmp += $"The Keystone engine could not recognize an opcode. Note that not all opcodes supported by the PS2's MIPS R5900 CPU are supported in Keystone.";
                    }
                    else if (errorInfo.KeystoneErrorValue == KeystoneError.KS_ERR_ASM_INVALIDOPERAND)
                    {
                        // lw t1,0x002DE2F0 (no $)
                        Tmp += $"The Keystone engine could not correctly parse the assembly instruction.{Program._newLine}This usually occurs because the dollar sign (\"$\") symbol is missing before referencing a register (\"t1\" instead of \"$t1\"), or because of an invalid number (for example, in the line \"lw $t1,0xq\", \"0xq\" is not a valid hexadecimal number).";
                    }
                    Tmp += $"{Program._newLine}{Program._newLine}";
                }
            }
            else
            {
                // CLPS2C error
                Tmp += $"{Enum.GetName(typeof(ERROR), errorInfo.ErrorValue)} at line {errorInfo.Command!.Traceback.First().LineIdx + 1} in file {errorInfo.Command.Traceback.First().FilePath}{Program._newLine}";
                Tmp += $"{ErrorMessages[errorInfo.ErrorValue]}{Program._newLine}{Program._newLine}";
            }

            Tmp += $"Line that produced the error:{Program._newLine}{errorInfo.Command!.FullLine}";
            Tmp += $"{Program._newLine}{Program._newLine}Traceback:";
            errorInfo.Command.Traceback.Reverse();
            foreach (Command_t.Traceback_t item in errorInfo.Command.Traceback)
            {
                Tmp += $"{Program._newLine}at {item.FilePath}:{item.LineIdx + 1} - {item.FullLine}";
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
                Output += $"{Program._newLine}{address} {value}";
            }

            switch (WXCount)
            {
                case 0:
                    break;
                case 1:
                    // W8
                    Output += $"{Program._newLine}0{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"000000{array[array.Length - 1].ToString("X2")}";
                    break;
                case 2:
                    // W16
                    Output += $"{Program._newLine}1{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"0000{array[array.Length - 1].ToString("X2") + array[array.Length - 2].ToString("X2")}";
                    break;
                case 3:
                    // W16 + W8
                    Output += $"{Program._newLine}1{((int)(Address + W32Count * 4)).ToString("X8").Substring(1)} ";
                    Output += $"0000{array[array.Length - 2].ToString("X2")}{array[array.Length - 3].ToString("X2")}";
                    Output += $"{Program._newLine}0{((int)(Address + W32Count * 4 + 2)).ToString("X8").Substring(1)} ";
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
            Match match = Regex.Match(address, @"^-?(0x)?[0-9A-Fa-f]{1,8}$");
            if (!match.Success)
            {
                return false;
            }
            return true;
        }

        public static bool IsIntValueValid(string value)
        {
            Match Match = Regex.Match(value, @"^-?(0x){1}[0-9A-Fa-f]{1,8}$|^-?(?!0x)\d+$");
            if (!Match.Success)
                return false;
            return true;
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

        public static List<string> GetSetValueFromTarget(string target, int id, List<LocalVar_t> listSets)
        {
            // Based on a string "target", find the closest "Set" (considering the id) in which the (NAME) parameter has the target value
            List<LocalVar_t> PossibleSets = listSets.Where(item => item.Name == target).ToList();
            var SetID = FindClosestLowerElement(PossibleSets, id);
            if (SetID == -1)
            {
                return new List<string>();
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
                    List<string> RecursiveValues = GetSetValueFromTarget(Element, SetID, listSets);
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

        public static string ReplaceTRegisters(string value)
        {
            // TO-DO: It would be better to handle this inside keystone itself
            // With keystone set to MIPS64 mode, the first 7 temporary registers do not produce the same result as Mips32:
            // https://github.com/keystone-engine/keystone/issues/421

            return value.Replace("t0", "8").Replace("t1", "9")
                         .Replace("t2", "10").Replace("t3", "11")
                         .Replace("t4", "12").Replace("t5", "13")
                         .Replace("t6", "14").Replace("t7", "15");
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
            ARGUMENT_COUNT_MISMATCH
        }

        public static Dictionary<ERROR, string> ErrorMessages = new()
        {
            { ERROR.UNKNOWN_COMMAND, "An unknown command has been detected. Check the list of commands supported in the documentation -> \"List of commands\" section" },
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
            { ERROR.ARGUMENT_COUNT_MISMATCH, "The count of the arguments in the \"Function\" definition and the count of the arguments passed to the function with the \"Call\" command are not equal.\nThe count of the arguments in the \"Function\" command and the ones in the \"Call\" command must be equal." }
        };
    }
}
