using static CLPS2C_Compiler.Util;

namespace CLPS2C_Compiler
{
    public class EEAssembler
    {
        private Dictionary<string, Instruction_t> _instructions = new();

        // Instructions that can be abbreviated (2 arguments instead of 3).
        // add $t0,$t1 -> add $t0,$t0,$t1
        // addi $t0,1 -> addi $t0,$t0,1
        private List<string> _abbreviatedInstructions = new()
        {
            "ADDI", "ADDIU", "SLTI", "SLTIU",
            "ANDI", "ORI", "XORI", "DADDI",
            "DADDIU", "SLL", "SRL", "SRA",
            "SLLV", "SRAV", "DSRAV", "ADD",
            "ADDU", "SUB", "SUBU", "AND",
            "OR", "XOR", "NOR", "SLT",
            "SLTU", "DSLL", "DSRL", "DSRA",
            "DSLL32", "DSRL32", "DSRA32", "MADD",
            "MADDU", "MULT1", "MULTU1", "MADD1",
            "MADDU1", "PADDW", "PSUBW", "PCGTW",
            "PMAXW", "PADDH", "PSUBH", "PCGTH",
            "PMAXH", "PADDB", "PSUBB", "PCGTB",
            "PADDSW", "PSUBSW", "PEXTLW", "PPACW",
            "PADDSH", "PSUBSH", "PEXTLH", "PPACH",
            "PADDSB", "PSUBSB", "PEXTLB", "PPACB",
            "PCEQW", "PMINW", "PADSBH", "PCEQH",
            "PMINH", "PCEQB", "PADDUW", "PSUBUW",
            "PEXTUW", "PPACW", "PADDUH", "PSUBUH",
            "PEXTUH", "PPACH", "PADDUB", "PSUBUB",
            "PEXTUB", "QFSRV", "PMADDW", "PSLLVW",
            "PSRLVW", "PMSUBW", "PINTH", "PMULTW",
            "PCPYLD", "PMADDH", "PHMADH", "PAND",
            "PXOR", "PMSUBH", "PHMSBH", "PMULTH",
            "PMADDUW", "PSRAVW", "PINTEH", "PMULTUW",
            "PCPYUD", "POR", "PNOR", "ADD.S",
            "SUB.S", "MUL.S", "DIV.S", "MADD.S",
            "MSUB.S", "DADDU"
        };

        // Labels defined in the same assembly scope. Name and address.
        private Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, int> _registersGPR = new()
        {
            { "zero", 0 }, { "at", 1 }, { "v0", 2 }, { "v1", 3 },
            { "a0", 4 }, { "a1", 5 }, { "a2", 6 }, { "a3", 7 },
            { "t0", 8 }, { "t1", 9 }, { "t2", 10 }, { "t3", 11 },
            { "t4", 12 }, { "t5", 13 }, { "t6", 14 }, { "t7", 15 },
            { "s0", 16 }, { "s1", 17 }, { "s2", 18 }, { "s3", 19 },
            { "s4", 20 }, { "s5", 21 }, { "s6", 22 }, { "s7", 23 },
            { "t8", 24 }, { "t9", 25 }, { "k0", 26 }, { "k1", 27 },
            { "gp", 28 }, { "sp", 29 }, { "fp", 30 }, { "ra", 31 },
        };

        private Dictionary<string, int> _registersFPR = new()
        {
            { "f0", 0 }, { "f00", 0 },
            { "f1", 1 }, { "f01", 1 },
            { "f2", 2 }, { "f02", 2 },
            { "f3", 3 }, { "f03", 3 },
            { "f4", 4 }, { "f04", 4 },
            { "f5", 5 }, { "f05", 5 },
            { "f6", 6 }, { "f06", 6 },
            { "f7", 7 }, { "f07", 7 },
            { "f8", 8 }, { "f08", 8 },
            { "f9", 9 }, { "f09", 9 },
            { "f10", 10 },{ "f11", 11 },{ "f12", 12 },{ "f13", 13 },
            { "f14", 14 },{ "f15", 15 },{ "f16", 16 },{ "f17", 17 },
            { "f18", 18 },{ "f19", 19 },{ "f20", 20 },{ "f21", 21 },
            { "f22", 22 },{ "f23", 23 },{ "f24", 24 },{ "f25", 25 },
            { "f26", 26 },{ "f27", 27 },{ "f28", 28 },{ "f29", 29 },
            { "f30", 30 },{ "f31", 31 },
        };

        private Dictionary<string, int> _registersVU0f = new()
        {
            { "vf0", 0 }, { "vf00", 0 },
            { "vf1", 1 }, { "vf01", 1 },
            { "vf2", 2 }, { "vf02", 2 },
            { "vf3", 3 }, { "vf03", 3 },
            { "vf4", 4 }, { "vf04", 4 },
            { "vf5", 5 }, { "vf05", 5 },
            { "vf6", 6 }, { "vf06", 6 },
            { "vf7", 7 }, { "vf07", 7 },
            { "vf8", 8 }, { "vf08", 8 },
            { "vf9", 9 }, { "vf09", 9 },
            { "vf10", 10 },{ "vf11", 11 },{ "vf12", 12 },{ "vf13", 13 },
            { "vf14", 14 },{ "vf15", 15 },{ "vf16", 16 },{ "vf17", 17 },
            { "vf18", 18 },{ "vf19", 19 },{ "vf20", 20 },{ "vf21", 21 },
            { "vf22", 22 },{ "vf23", 23 },{ "vf24", 24 },{ "vf25", 25 },
            { "vf26", 26 },{ "vf27", 27 },{ "vf28", 28 },{ "vf29", 29 },
            { "vf30", 30 },{ "vf31", 31 },
        };

        private Dictionary<string, int> _registersVU0i = new()
        {
            { "vi0", 0 }, { "vi00", 0 },
            { "vi1", 1 }, { "vi01", 1 },
            { "vi2", 2 }, { "vi02", 2 },
            { "vi3", 3 }, { "vi03", 3 },
            { "vi4", 4 }, { "vi04", 4 },
            { "vi5", 5 }, { "vi05", 5 },
            { "vi6", 6 }, { "vi06", 6 },
            { "vi7", 7 }, { "vi07", 7 },
            { "vi8", 8 }, { "vi08", 8 },
            { "vi9", 9 }, { "vi09", 9 },
            { "vi10", 10 },{ "vi11", 11 },{ "vi12", 12 },{ "vi13", 13 },
            { "vi14", 14 },{ "vi15", 15 }
        };

        public EEAssembler()
        {
            RegisterInstructions();
        }

        public byte[] Assemble(List<Command_t> commands)
        {
            List<byte> output = new();
            _labels.Clear();

            commands = PreprocessCommands(commands);
            if (Program.error)
            {
                return [];
            }

            int currentAddress = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                byte[] bytes = EncodeCommand(commands[i], currentAddress);
                if (Program.error)
                {
                    return [];
                }

                output.AddRange(bytes);
                currentAddress += 4;
            }

            return output.ToArray();
        }

        private byte[] EncodeCommand(Command_t command, int currentAddress)
        {
            Instruction_t? instruction = GetInstructionFromCommand(command);
            if (instruction == null)
            {
                // Unknown opcode
                Program.SetError(ERROR.ASM_UNKNOWN_MNEMONIC, command);
                return [];
            }

            if (command.Data.Count != instruction.Operands.Length)
            {
                // too few or too many arguments
                Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                return [];
            }

            int rs = 0;
            int rt = 0;
            int rd = 0;
            int shiftAmount = 0;
            int immediate = 0;
            for (int i = 0; i < instruction.Operands.Length; i++)
            {
                if (i > 0 && !command.Data[i].StartsWith(','))
                {
                    // Arguments after the first one must start with ','
                    Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                    return [];
                }

                string operand = command.Data[i].TrimStart(',').Trim();
                switch (instruction.Operands[i])
                {
                    // GPR
                    case OPERAND.rs:
                        if (!TryResolveRegister(command, operand, _registersGPR, out rs))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.rt:
                        if (!TryResolveRegister(command, operand, _registersGPR, out rt))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.rd:
                        if (!TryResolveRegister(command, operand, _registersGPR, out rd))
                        {
                            return [];
                        }
                        break;
                    // FPR
                    case OPERAND.fs:
                        if (!TryResolveRegister(command, operand, _registersFPR, out rs))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.ft:
                        if (!TryResolveRegister(command, operand, _registersFPR, out rt))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.fd:
                        if (!TryResolveRegister(command, operand, _registersFPR, out rd))
                        {
                            return [];
                        }
                        break;
                    // VU0f
                    case OPERAND.vfs:
                        if (!TryResolveRegister(command, operand, _registersVU0f, out rs))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.vft:
                        if (!TryResolveRegister(command, operand, _registersVU0f, out rt))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.vfd:
                        if (!TryResolveRegister(command, operand, _registersVU0f, out rd))
                        {
                            return [];
                        }
                        break;
                    // VU0i
                    case OPERAND.vis:
                        if (!TryResolveRegister(command, operand, _registersVU0i, out rs))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.vit:
                        if (!TryResolveRegister(command, operand, _registersVU0i, out rt))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.vid:
                        if (!TryResolveRegister(command, operand, _registersVU0i, out rd))
                        {
                            return [];
                        }
                        break;
                    case OPERAND.Immediate5:
                        if (!IsIntValueValid(operand))
                        {
                            Program.SetError(ERROR.ASM_IMMEDIATE_VALUE_INVALID, command);
                            return [];
                        }

                        rd = ConvertIntValue(operand);
                        if (rd < 0 || rd > 0x1F)
                        {
                            Program.SetError(ERROR.ASM_IMMEDIATE_VALUE_INVALID, command);
                            return [];
                        }

                        break;
                    case OPERAND.ShiftAmount:
                        if (!IsIntValueValid(operand))
                        {
                            Program.SetError(ERROR.ASM_SHIFT_AMOUNT_VALUE_INVALID, command);
                            return [];
                        }

                        shiftAmount = ConvertIntValue(operand);
                        if (shiftAmount < 0 || shiftAmount > 0x1F)
                        {
                            Program.SetError(ERROR.ASM_SHIFT_AMOUNT_VALUE_INVALID, command);
                            return [];
                        }

                        break;
                    case OPERAND.Immediate:
                        if (!IsIntValueValid(operand))
                        {
                            Program.SetError(ERROR.ASM_IMMEDIATE_VALUE_INVALID, command);
                            return [];
                        }

                        immediate = ConvertIntValue(operand);
                        break;
                    case OPERAND.Label:
                        if (instruction.Type == INSTRUCTIONTYPE.Jump)
                        {
                            // J and JAL, either an address or a variable set with the Set command, or a mix of the 2 with '+'
                            int labelAddress = Convert.ToInt32(operand, 16);
                            immediate = labelAddress / 4;
                        }
                        else
                        {
                            // branches, only labels defined in the same assembly scope
                            if (!_labels.TryGetValue(operand, out int labelAddress))
                            {
                                Program.SetError(ERROR.ASM_UNDEFINED_LABEL, command);
                                return [];
                            }

                            immediate = (labelAddress - (currentAddress + 4)) / 4;
                        }

                        break;
                }
            }

            int output = 0;
            switch (instruction.Type)
            {
                case INSTRUCTIONTYPE.Normal:
                    output = (instruction.Opcode << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (immediate & 0xFFFF);
                    break;
                case INSTRUCTIONTYPE.Jump:
                    output = (instruction.Opcode << 26)
                           | (immediate & 0x03FFFFFF);
                    break;
                case INSTRUCTIONTYPE.Special:
                    output = (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (shiftAmount << 6)
                           | (instruction.Opcode);
                    break;
                case INSTRUCTIONTYPE.REGIMM:
                    output = (1 << 26)
                           | (rs << 21)
                           | (instruction.Opcode << 16)
                           | (immediate & 0xFFFF);
                    break;
                case INSTRUCTIONTYPE.MMI:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (shiftAmount << 6)
                           | (instruction.Opcode);
                    break;
                case INSTRUCTIONTYPE.MMI0:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (instruction.Opcode << 6)
                           | (0b001000);
                    break;
                case INSTRUCTIONTYPE.MMI1:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (instruction.Opcode << 6)
                           | (0b101000);
                    break;
                case INSTRUCTIONTYPE.MMI2:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (instruction.Opcode << 6)
                           | (0b001001);
                    break;
                case INSTRUCTIONTYPE.MMI3:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (instruction.Opcode << 6)
                           | (0b101001);
                    break;
                case INSTRUCTIONTYPE.ParallelMoveFromHILORegister:
                    output = (0b011100 << 26)
                           | (rd << 11)
                           | (instruction.Opcode << 6)
                           | (0b110000);
                    break;
                case INSTRUCTIONTYPE.ParallelMoveToHILORegister:
                    output = (0b011100 << 26)
                           | (rs << 21)
                           | (instruction.Opcode << 6)
                           | (0b110001);
                    break;
                case INSTRUCTIONTYPE.FPUS:
                    output = (0b010001 << 26)
                           | (0b10000 << 21)
                           | (rt << 16)
                           | (rs << 11)
                           | (rd << 6)
                           | (instruction.Opcode);
                    break;
                case INSTRUCTIONTYPE.FPUW:
                    output = (0b010001 << 26)
                           | (0b10100 << 21)
                           | (rs << 11)
                           | (rd << 6)
                           | (instruction.Opcode);
                    break;
                case INSTRUCTIONTYPE.BC0:
                    // TODO
                    output = 0;
                    break;
                case INSTRUCTIONTYPE.BC1:
                    output = (0b010001 << 26)
                           | (0b01000 << 21)
                           | (instruction.Opcode << 16)
                           | (immediate & 0xFFFF);
                    break;
                case INSTRUCTIONTYPE.COP0:
                    // TODO
                    output = 0;
                    break;
                case INSTRUCTIONTYPE.TLBException:
                    // TODO
                    output = 0;
                    break;
                case INSTRUCTIONTYPE.COP1:
                    output = (0b010001 << 26)
                           | (instruction.Opcode << 21)
                           | (rt << 16)
                           | (rs << 11);
                    break;
                case INSTRUCTIONTYPE.COP2:
                    int withInterlock = GetCOP2Interlock(command);
                    if (Program.error)
                    {
                        return [];
                    }

                    output = (0b010010 << 26)
                           | (instruction.Opcode << 21)
                           | (rt << 16)
                           | (rd << 11)
                           | (withInterlock);
                    break;
                case INSTRUCTIONTYPE.COP2Special1:
                    int destCOP2Special1 = GetCOP2SpecialDest(command);
                    if (Program.error)
                    {
                        return [];
                    }

                    output = (0b010010 << 26)
                           | (1 << 25)
                           | (destCOP2Special1 << 21)
                           | (rt << 16)
                           | (rs << 11)
                           | (rd << 6)
                           | (instruction.Opcode);
                    break;
                case INSTRUCTIONTYPE.COP2Special2:
                    int destCOP2Special2 = GetCOP2SpecialDest(command);
                    if (Program.error)
                    {
                        return [];
                    }

                    output = (0b010010 << 26)
                           | (1 << 25)
                           | (destCOP2Special2 << 21)
                           | (rt << 16)
                           | (rs << 11)
                           | (((instruction.Opcode >> 2) & 0b11111) << 6) // upper 5 bits
                           | (0b1111 << 2)
                           | (instruction.Opcode & 0b11); // lower 2 bits
                    break;
            }

            return BitConverter.GetBytes(output);
        }

        private Instruction_t? GetInstructionFromCommand(Command_t command)
        {
            Instruction_t? instruction = null;
            string mnemonicToFind = command.Type;
            if (_instructions.TryGetValue(mnemonicToFind, out var instr))
            {
                instruction = instr;
            }
            else
            {
                // Try stripping suffix only for:
                // - COP2: ".i" for interlock
                // - COP2Special1: ".xyzw" for dest
                // - COP2Special2: ".xyzw" for dest
                int dotIndex = mnemonicToFind.IndexOf('.');
                if (dotIndex != -1)
                {
                    string mnemonicToFind2 = mnemonicToFind.Substring(0, dotIndex);
                    if (_instructions.TryGetValue(mnemonicToFind2, out instr)
                        && (instr.Type == INSTRUCTIONTYPE.COP2
                            || instr.Type == INSTRUCTIONTYPE.COP2Special2
                            || instr.Type == INSTRUCTIONTYPE.COP2Special1))
                    {
                        instruction = instr;
                    }
                }
            }
            return instruction;
        }

        private List<Command_t> PreprocessCommands(List<Command_t> commands)
        {
            // This methods handles
            // - labels
            // - abbreviated instructions (2 arguments instead of 3) which have the same mnemonic as the ones added when this class is constructed, and that might produce one or more instructions (we can't add them directly because we implemented the instructions list as a dictionary)
            // J and JAL instructions (they support set commands + the '+' operator)
            // "dsllv" and "dsrlv" (2 arguments to 3 arguments where the middle one becomes $zero)
            // - "jalr $ra" to "jalr $ra,$ra"
            // - pseudo-instructions (BLT, BGE, BLE, BGT, LI)
            // load and store instructions

            List<Command_t> expanded = new();
            for (int i = 0; i < commands.Count; i++)
            {
                Command_t? command = commands[i];
                if (command.Type.EndsWith(':'))
                {
                    // Labels
                    int currentAddress = expanded.Count * 4;
                    string labelName = command.Type.TrimEnd(':');
                    if (labelName == "" || command.Type.Contains("::"))
                    {
                        // no label before the ":"
                        // myLabel::
                        // myLabel: :
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    if (!_labels.TryAdd(labelName, currentAddress))
                    {
                        Program.SetError(ERROR.ASM_LABEL_ALREADY_DEFINED, command);
                        return [];
                    }

                    int index = command.FullLine.IndexOf(':');
                    string instructionAfterLabel = command.FullLine.Substring(index + 1).Trim();
                    if (instructionAfterLabel != "")
                    {
                        // label and the next instruction are on the same line
                        Command_t newCommand = new(instructionAfterLabel, command, true);
                        commands.RemoveAt(i);
                        commands.Insert(i, newCommand);
                        i--;
                    }
                }
                else if (command.Data.Count != 3 && _abbreviatedInstructions.Contains(command.Type))
                {
                    // add $t0,$t1 -> addi $t0,$t0,$t1
                    // addi $t0,1 -> addi $t0,$t0,1
                    if (command.Data.Count != 2)
                    {
                        // syntax error
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    expanded.Add(new($"{command.Type} {command.Data[0]},{command.Data[0]}{command.Data[1]}", command));
                }
                else if (command.Data.Count != 3
                    && (command.Type == "DSLLV"
                     || command.Type == "DSRLV"))
                {
                    // dsllv $t0,$t1 -> dsllv $t0,$zero,$t1
                    if (command.Data.Count != 2)
                    {
                        // syntax error
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    expanded.Add(new($"{command.Type} {command.Data[0]},$zero{command.Data[1]}", command));
                }
                else if (command.Data.Count >= 1
                    && (command.Type == "J"
                     || command.Type == "JAL"))
                {
                    // j label -> j address
                    // j 0x1234 + var1 + 0x5678 -> j address

                    int k = 0;
                    string labelAddress = Program.GetAddress(command, ref k);
                    if (Program.error)
                    {
                        return [];
                    }
                    if (k < command.Data.Count)
                    {
                        // If there are remaining arguments (without the + symbol)
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    command.Data[0] = labelAddress;
                    command.Data.RemoveRange(1, k - 1);
                    expanded.Add(command);
                }

                else if (command.Type == "JALR" && command.Data.Count == 1)
                {
                    // jalr $ra -> jalr $ra,$ra
                    expanded.Add(new($"JALR $ra,{command.Data[0]}", command));
                }
                else if (command.Type == "BLT"
                    || command.Type == "BGE"
                    || command.Type == "BLE"
                    || command.Type == "BGT")
                {
                    // blt $t0,$t1,label -> slt $at,$t0,$t1 + bne $at,$zero,label // branch if $t0 < $t1
                    // bge $t0,$t1,label -> slt $at,$t0,$t1 + beq $at,$zero,label // branch if $t0 >= $t1

                    // ble $t0,$t1,label -> slt $at,$t1,$t0 + beq $at,$zero,label // branch if $t0 <= $t1
                    // bgt $t0,$t1,label -> slt $at,$t1,$t0 + bne $at,$zero,label // branch if $t0 > $t1

                    // The difference is beq/bne, and if register1 comes before register2
                    if (command.Data.Count != 3)
                    {
                        // syntax error
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    string register1 = command.Data[0]; // $t0
                    string register2 = command.Data[1].TrimStart(','); // $t1
                    string label = command.Data[2]; // ,label

                    if (command.Type == "BLE" || command.Type == "BGT")
                    {
                        // Switch the registers
                        register1 = register2;
                        register2 = command.Data[0];
                    }

                    string opcode = "BNE"; // blt and bgt
                    if (command.Type == "BGE" || command.Type == "BLE")
                    {
                        opcode = "BEQ";
                    }

                    expanded.Add(new($"SLT $at,{register1},{register2}", command));
                    expanded.Add(new($"{opcode} $at,$zero{label}", command));
                }
                else if (command.Type == "LB" || command.Type == "LBU"
                    || command.Type == "LD" || command.Type == "LDL" || command.Type == "LDR"
                    || command.Type == "LH" || command.Type == "LHU"
                    || command.Type == "LW" || command.Type == "LWL" || command.Type == "LWR" || command.Type == "LWU"
                    || command.Type == "LQ"
                    || command.Type == "LWC1"
                    || command.Type == "SB"
                    || command.Type == "SD" || command.Type == "SDL" || command.Type == "SDR"
                    || command.Type == "SH"
                    || command.Type == "SW" || command.Type == "SWL" || command.Type == "SWR"
                    || command.Type == "SQ"
                    || command.Type == "SWC1"
                    || command.Type == "LQC2"
                    || command.Type == "SQC2")
                {
                    // The load and store instructions (the ones with the "rt,offset(base)" format) can be abbreviated to "rt,address" where address is a 32-bit address.
                    // They are expanded by using the provided register as the base and a lui instruction.
                    // In the case of lwc1, swc1, lqc2 and sqc2, the $at register is used as the base register for the lui instruction.
                    // This is because the lui instruction only supports GPR registers (e.g. we can't do "lui $f0,0x00FB").

                    // lw $t0,0x1 -> lw $t0,0x1($zero)
                    // lw $t0,0x7FFF -> lw $t0,0x7FFF($zero)

                    // lw $t0,0x8000 -> lui $t0,0x1 + lw $t0,-0x8000($t0)
                    // lw $t0,0xFFFF -> lui $t0,0x1 + lw $t0,-0xFFFF($t0)

                    // lw $t0,0x10000 -> lui $t0,0x1 + lw $t0,0x0($t0)
                    // lw $t0,0x17FFF -> lui $t0,0x1 + lw $t0,0x7FFF($t0)

                    // lw $t0,0x18000 -> lui $t0,0x2 + lw $t0,-0x8000($t0)
                    // lw $t0,0x1FFFF -> lui $t0,0x2 + lw $t0,-0x1($t0)

                    // lw $t0,0x12345678 -> lui $t0,0x1234 + lw $t0,0x5678($t0)
                    // lw $t0,0xFB1580 -> lui $t0,0x00FB + lw $t0,0x1580($t0)
                    // lw $t0,0xFB8580 -> lui $t0,0x00FC + lw $t0,-0x8580($t0)

                    // lwc1 $f0,0xFB1580 -> lui $at,0x00FB + lwc1 $f0,0x1580($at)

                    if (command.Data.Count == 3)
                    {
                        // lw $t0, 0x1ea8 ($t1)
                        // This is an hack. There is a space before the parenthesis. We can still try to save this command
                        command.Data[1] = command.Data[1] + command.Data[2];
                        command.Data.RemoveAt(2);
                    }
                    else if (command.Data.Count != 2)
                    {
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    if (command.Data[1].Contains('(') && command.Data[1].Contains(')'))
                    {
                        // If an rs was specified
                        // lw $t0,0x1580($t1)
                        // lw $t0,($t1) which is about to be lw $t0,0x0($t1)

                        int parenStart = command.Data[1].IndexOf('(');
                        string offsetStr = command.Data[1].Substring(0, parenStart).TrimEnd(); // ,0x58
                        if (offsetStr == ",")
                        {
                            // If an offset was not specified, set it to 0x0
                            // lw $t0,($t1) -> lw $t0,0x0($t1)
                            offsetStr = $",0x0";
                        }

                        string operand2 = command.Data[1].Substring(parenStart + 1, command.Data[1].Length - parenStart - 2); // $t1

                        command.Data[1] = $"{offsetStr}"; // ",0x58"
                        command.Data.Add($",{operand2}"); // ",$t1"
                        command = Program.ApplySetsToCommand(command);

                        // Force the offset to be a hexadecimal number
                        int commaStart = 0 /*command.Data[1].IndexOf(',')*/;
                        offsetStr = command.Data[1].Substring(commaStart + 1, command.Data[1].Length - commaStart - 1).TrimEnd();
                        if (!offsetStr.StartsWith("0x") && !offsetStr.StartsWith("-0x"))
                        {
                            if (offsetStr.StartsWith('-'))
                            {
                                offsetStr = $",-0x{offsetStr.Substring(1)}";
                            }
                            else
                            {
                                offsetStr = $",0x{offsetStr}";
                            }

                            command.Data[1] = offsetStr;
                        }

                        expanded.Add(command);
                        continue;
                    }

                    // If a rs was not specified
                    // lw $t0,0xFB1580
                    string operand = command.Data[1].TrimStart(',');
                    if (!IsAddressValid(operand))
                    {
                        Program.SetError(ERROR.ASM_IMMEDIATE_VALUE_INVALID, command);
                        return [];
                    }

                    int immediate = ConvertAddress(operand);
                    string register = command.Data[0];

                    if (immediate < 0x8000)
                    {
                        // If it's less than 0x8000, we can use the $zero register. This is rare. This is only used if you load from address 0 to address 0x7FFF
                        Command_t loadStoreCommand = new($"{command.Type} {register},0x{immediate:X}($zero)", command); // lw $t0,0x1580($t0)
                        operand = loadStoreCommand.Data[1].TrimStart(',')/*.Trim()*/;
                        int parenStart = operand.IndexOf('(');
                        string offsetStr = operand.Substring(0, parenStart); // 0x58
                        string operand2 = operand.Substring(parenStart + 1, operand.Length - parenStart - 2); // $t1
                        loadStoreCommand.Data[1] = $",{offsetStr}"; // ",0x58"
                        loadStoreCommand.Data.Add($",{operand2}"); // ",$t1"

                        expanded.Add(loadStoreCommand);
                    }
                    else
                    {
                        // Usually the address specified is greater than 0x8000.
                        // We can't use $zero. Instead, we use the same register to load the base (lui)
                        int upper = (immediate >> 16) & 0xFFFF; // 0xFB
                        int lower = immediate & 0xFFFF; // 0x1580
                        if (lower >= 0x8000)
                        {
                            // But if the offset is greater than 0x8000, then we need to increase the base by 0x10000, and "go backwards" with the offset
                            // This is because the offset is signed, not unsigned
                            upper += 1;
                            lower -= 0x10000;
                        }

                        string sourceRegister = register;
                        if (command.Type == "LWC1" || command.Type == "SWC1"
                         || command.Type == "LQC2" || command.Type == "SQC2")
                        {
                            // For floating point and vector instructions we can't use the target register as the source register
                            // Let's use $at instead
                            sourceRegister = "$at";
                        }

                        // lui $t0,0xFB
                        // lui $at,0xFB
                        expanded.Add(new($"LUI {sourceRegister},0x{upper:X}", command));

                        // lw $t0,0x1580($t0)
                        // lwc1 $f0,0x1580($at)
                        Command_t loadStoreCommand = new($"{command.Type} {register},0x{lower:X}({sourceRegister})", command);
                        operand = loadStoreCommand.Data[1].TrimStart(',');
                        int parenStart = operand.IndexOf('(');
                        string offsetStr = operand.Substring(0, parenStart); // 0x58
                        string operand2 = operand.Substring(parenStart + 1, operand.Length - parenStart - 2); // $t1
                        loadStoreCommand.Data[1] = $",{offsetStr}"; // ",0x58"
                        loadStoreCommand.Data.Add($",{operand2}"); // ",$t1"

                        expanded.Add(loadStoreCommand);
                    }
                }
                else if (command.Type == "LI")
                {
                    // "li -> ori"
                    // "li -> lui + ori"

                    // "li -> addiu" was not chosen because addiu sign-extends the immediate value: addiu $t0,$zero,0x8123 = 0xFFFF8123
                    // but ori zero-extends, so: ori $t0,$zero,0x8123 = 0x00008123

                    if (command.Data.Count != 2)
                    {
                        Program.SetError(ERROR.ASM_WRONG_SYNTAX, command);
                        return [];
                    }

                    string operand = command.Data[1].TrimStart(',').Trim();
                    if (!IsIntValueValid(operand))
                    {
                        Program.SetError(ERROR.ASM_IMMEDIATE_VALUE_INVALID, command);
                        return [];
                    }

                    int immediate = ConvertIntValue(operand);
                    string register = command.Data[0];
                    if (immediate < 0x10000)
                    {
                        // Only 1 instruction, either addiu or ori
                        string instruction = "ORI";
                        if (immediate < 0)
                        {
                            instruction = "ADDIU";
                        }

                        // li $t0,-1 -> addiu $t0,$zero,0xFFFFFFFF
                        // li $t0,0x8123 -> ori $t0,$zero,0x8123
                        expanded.Add(new($"{instruction} {register},$zero,0x{immediate:X}", command));
                    }
                    else
                    {
                        int upper = (immediate >> 16) & 0xFFFF;
                        int lower = immediate & 0xFFFF;

                        expanded.Add(new($"LUI {register},0x{upper:X}", command));
                        if (lower != 0)
                        {
                            // li $t0,0x12345678
                            expanded.Add(new($"ORI {register},{register},0x{lower:X}", command));
                        }
                    }
                }
                else
                {
                    // Keep as it is
                    expanded.Add(command);
                }
            }

            return expanded;
        }

        private bool TryResolveRegister(Command_t command, string operand, Dictionary<string, int> registers, out int value)
        {
            if (operand.StartsWith('$'))
            {
                operand = operand.Substring(1);
            }

            if (!registers.TryGetValue(operand, out value))
            {
                Program.SetError(ERROR.ASM_UNKNOWN_REGISTER, command);
                return false;
            }

            return true;
        }

        private int GetCOP2Interlock(Command_t command)
        {
            int dotIndex = command.Type.IndexOf('.');
            if (dotIndex == -1)
            {
                // If no interlock was specified, default to no interlock
                return 0;
            }

            string suffix = command.Type.Substring(dotIndex + 1);
            if (suffix.Length == 0 || suffix.Length > 1 || suffix != "I")
            {
                // error, no string, too many characters after the dot or wrong letter
                // cfc2.
                // cfc2.xx
                Program.SetError(ERROR.ASM_WRONG_INTERLOCK, command);
                return 0;
            }

            // With interlock
            return 1;
        }

        private int GetCOP2SpecialDest(Command_t command)
        {
            int dest = 0;
            int dotIndex = command.Type.IndexOf('.');
            if (dotIndex == -1)
            {
                // If no dest was specified, default to nothing
                return dest;
            }

            string suffix = command.Type.Substring(dotIndex + 1);
            if (suffix.Length == 0 || suffix.Length > 4)
            {
                // error, no string or too many characters after the dot
                // vabs.
                // vabs.xxxxx
                Program.SetError(ERROR.ASM_WRONG_DEST, command);
                return 0;
            }

            foreach (char ch in suffix)
            {
                var tmp = 0;
                switch (ch)
                {
                    case 'X':
                        tmp = 0b1000;
                        break;
                    case 'Y':
                        tmp = 0b0100;
                        break;
                    case 'Z':
                        tmp = 0b0010;
                        break;
                    case 'W':
                        tmp = 0b0001;
                        break;
                    default:
                        // incorrect letter
                        Program.SetError(ERROR.ASM_WRONG_DEST, command);
                        return 0;
                }

                // dest != 0 to ignore the first loop, as any letter is acceptable in the first loop
                if (dest != 0)
                {
                    if ((dest & tmp) != 0
                    || tmp > dest)
                    {
                        // If the bit is set, it means the letter has already appeared
                        Program.SetError(ERROR.ASM_WRONG_DEST, command);
                        return 0;
                    }
                }

                dest |= tmp;
            }

            return dest;
        }

        public class Instruction_t
        {
            public string Mnemonic { get; set; }
            public INSTRUCTIONTYPE Type { get; set; }
            public byte Opcode { get; set; }
            public OPERAND[] Operands { get; set; }
            public Instruction_t(string mnemonic, INSTRUCTIONTYPE type, byte opcode, params OPERAND[] operands)
            {
                Mnemonic = mnemonic;
                Type = type;
                Opcode = opcode;
                Operands = operands;
            }

            public override string ToString()
            {
                var tmp = $"\"{Mnemonic}";
                for (int i = 0; i < Operands.Length; i++)
                {
                    tmp += $" {Operands[i]}";
                }

                tmp += $"\" ({Type}) 0b{Opcode:B}";

                return tmp;
            }
        }

        public enum OPERAND
        {
            rs,
            rd,
            rt,
            ShiftAmount,
            Immediate,
            Label,

            fs,
            fd,
            ft,

            vfs,
            vfd,
            vft,

            vis,
            vid,
            vit,
            Immediate5, // used for VIADDI
        }

        public enum INSTRUCTIONTYPE
        {
            Normal,
            Jump, // special type for J and JAL instructions
            Special,
            REGIMM,
            COP0,
            COP1,
            COP2,
            TLBException, // COP0 TLB list
            MMI,
            MMI0,
            MMI1,
            MMI2,
            MMI3,
            ParallelMoveFromHILORegister, // special type for PMFHL instruction
            ParallelMoveToHILORegister, // special type for PMTHL instruction
            BC0,
            BC1,
            FPUS,
            FPUW,
            COP2Special1,
            COP2Special2,
        }

        private void AddInstruction(string mnemonic, INSTRUCTIONTYPE instructionType, byte opcode, OPERAND[] operands)
        {
            _instructions.Add(mnemonic, new(mnemonic, instructionType, opcode, operands));
        }

        private void RegisterInstructions()
        {
            AddInstruction("ADD", INSTRUCTIONTYPE.Special, 0b100000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("ADDI", INSTRUCTIONTYPE.Normal, 0b001000, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("ADDIU", INSTRUCTIONTYPE.Normal, 0b001001, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("ADDU", INSTRUCTIONTYPE.Special, 0b100001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("AND", INSTRUCTIONTYPE.Special, 0b100100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("ANDI", INSTRUCTIONTYPE.Normal, 0b001100, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("BEQ", INSTRUCTIONTYPE.Normal, 0b000100, [OPERAND.rs, OPERAND.rt, OPERAND.Label]);
            AddInstruction("B", INSTRUCTIONTYPE.Normal, 0b000100, [OPERAND.Label]);
            AddInstruction("BEQZ", INSTRUCTIONTYPE.Normal, 0b000100, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BEQL", INSTRUCTIONTYPE.Normal, 0b010100, [OPERAND.rs, OPERAND.rt, OPERAND.Label]);
            AddInstruction("BEQZL", INSTRUCTIONTYPE.Normal, 0b010100, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGEZ", INSTRUCTIONTYPE.REGIMM, 0b00001, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGEZAL", INSTRUCTIONTYPE.REGIMM, 0b10001, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGEZALL", INSTRUCTIONTYPE.REGIMM, 0b10011, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGEZL", INSTRUCTIONTYPE.REGIMM, 0b00011, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGTZ", INSTRUCTIONTYPE.Normal, 0b000111, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BGTZL", INSTRUCTIONTYPE.Normal, 0b010111, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLEZ", INSTRUCTIONTYPE.Normal, 0b000110, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLEZL", INSTRUCTIONTYPE.Normal, 0b010110, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLTZ", INSTRUCTIONTYPE.REGIMM, 0b00000, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLTZAL", INSTRUCTIONTYPE.REGIMM, 0b10000, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLTZALL", INSTRUCTIONTYPE.REGIMM, 0b10010, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BLTZL", INSTRUCTIONTYPE.REGIMM, 0b00010, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BNE", INSTRUCTIONTYPE.Normal, 0b000101, [OPERAND.rs, OPERAND.rt, OPERAND.Label]);
            AddInstruction("BNEZ", INSTRUCTIONTYPE.Normal, 0b000101, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BNEL", INSTRUCTIONTYPE.Normal, 0b010101, [OPERAND.rs, OPERAND.rt, OPERAND.Label]);
            AddInstruction("BNEZL", INSTRUCTIONTYPE.Normal, 0b010101, [OPERAND.rs, OPERAND.Label]);
            AddInstruction("BREAK", INSTRUCTIONTYPE.Special, 0b001101, []);
            AddInstruction("DADD", INSTRUCTIONTYPE.Special, 0b101100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("DADDI", INSTRUCTIONTYPE.Normal, 0b011000, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("DADDIU", INSTRUCTIONTYPE.Normal, 0b011001, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("DADDU", INSTRUCTIONTYPE.Special, 0b101101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("DIV", INSTRUCTIONTYPE.Special, 0b011010, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("DIVU", INSTRUCTIONTYPE.Special, 0b011011, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("DSLL", INSTRUCTIONTYPE.Special, 0b111000, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSLL32", INSTRUCTIONTYPE.Special, 0b111100, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSLLV", INSTRUCTIONTYPE.Special, 0b010100, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("DSRA", INSTRUCTIONTYPE.Special, 0b111011, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSRA32", INSTRUCTIONTYPE.Special, 0b111111, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSRAV", INSTRUCTIONTYPE.Special, 0b010111, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("DSRL", INSTRUCTIONTYPE.Special, 0b111010, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSRL32", INSTRUCTIONTYPE.Special, 0b111110, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("DSRLV", INSTRUCTIONTYPE.Special, 0b010110, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("DSUB", INSTRUCTIONTYPE.Special, 0b101110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("DSUBU", INSTRUCTIONTYPE.Special, 0b101111, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("J", INSTRUCTIONTYPE.Jump, 0b000010, [OPERAND.Label]);
            AddInstruction("JAL", INSTRUCTIONTYPE.Jump, 0b000011, [OPERAND.Label]);
            AddInstruction("JALR", INSTRUCTIONTYPE.Special, 0b001001, [OPERAND.rd, OPERAND.rs]);
            AddInstruction("JR", INSTRUCTIONTYPE.Special, 0b001000, [OPERAND.rs]);
            AddInstruction("LB", INSTRUCTIONTYPE.Normal, 0b100000, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LBU", INSTRUCTIONTYPE.Normal, 0b100100, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LD", INSTRUCTIONTYPE.Normal, 0b110111, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LDL", INSTRUCTIONTYPE.Normal, 0b011010, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LDR", INSTRUCTIONTYPE.Normal, 0b011011, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LH", INSTRUCTIONTYPE.Normal, 0b100001, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LHU", INSTRUCTIONTYPE.Normal, 0b100101, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LUI", INSTRUCTIONTYPE.Normal, 0b001111, [OPERAND.rt, OPERAND.Immediate]);
            AddInstruction("LW", INSTRUCTIONTYPE.Normal, 0b100011, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LWL", INSTRUCTIONTYPE.Normal, 0b100010, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LWR", INSTRUCTIONTYPE.Normal, 0b100110, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("LWU", INSTRUCTIONTYPE.Normal, 0b100111, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("MFHI", INSTRUCTIONTYPE.Special, 0b010000, [OPERAND.rd]);
            AddInstruction("MFLO", INSTRUCTIONTYPE.Special, 0b010010, [OPERAND.rd]);
            AddInstruction("MOVN", INSTRUCTIONTYPE.Special, 0b001011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MOVZ", INSTRUCTIONTYPE.Special, 0b001010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MTHI", INSTRUCTIONTYPE.Special, 0b010001, [OPERAND.rs]);
            AddInstruction("MTLO", INSTRUCTIONTYPE.Special, 0b010011, [OPERAND.rs]);
            AddInstruction("MULT", INSTRUCTIONTYPE.Special, 0b011000, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("MULTU", INSTRUCTIONTYPE.Special, 0b011001, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("NOR", INSTRUCTIONTYPE.Special, 0b100111, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("OR", INSTRUCTIONTYPE.Special, 0b100101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            // "MOVE" and "DMOVE" are effectively the same.
            AddInstruction("MOVE", INSTRUCTIONTYPE.Special, 0b100101, [OPERAND.rd, OPERAND.rs]);
            AddInstruction("DMOVE", INSTRUCTIONTYPE.Special, 0b100101, [OPERAND.rd, OPERAND.rs]);
            AddInstruction("ORI", INSTRUCTIONTYPE.Normal, 0b001101, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            // PREF invalid opcode by pcsx2
            AddInstruction("SB", INSTRUCTIONTYPE.Normal, 0b101000, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SD", INSTRUCTIONTYPE.Normal, 0b111111, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SDL", INSTRUCTIONTYPE.Normal, 0b101100, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SDR", INSTRUCTIONTYPE.Normal, 0b101101, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SH", INSTRUCTIONTYPE.Normal, 0b101001, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SLL", INSTRUCTIONTYPE.Special, 0b000000, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("NOP", INSTRUCTIONTYPE.Special, 0b000000, []);
            AddInstruction("SLLV", INSTRUCTIONTYPE.Special, 0b000100, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("SLT", INSTRUCTIONTYPE.Special, 0b101010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("SLTI", INSTRUCTIONTYPE.Normal, 0b001010, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("SLTIU", INSTRUCTIONTYPE.Normal, 0b001011, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("SLTU", INSTRUCTIONTYPE.Special, 0b101011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("SRA", INSTRUCTIONTYPE.Special, 0b000011, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("SRAV", INSTRUCTIONTYPE.Special, 0b000111, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("SRL", INSTRUCTIONTYPE.Special, 0b000010, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("SRLV", INSTRUCTIONTYPE.Special, 0b000110, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("SUB", INSTRUCTIONTYPE.Special, 0b100010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("NEG", INSTRUCTIONTYPE.Special, 0b100010, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("SUBU", INSTRUCTIONTYPE.Special, 0b100011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("NEGU", INSTRUCTIONTYPE.Special, 0b100011, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("SW", INSTRUCTIONTYPE.Normal, 0b101011, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SWL", INSTRUCTIONTYPE.Normal, 0b101010, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SWR", INSTRUCTIONTYPE.Normal, 0b101110, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("SYNC", INSTRUCTIONTYPE.Special, 0b001111, []);
            // SYNC.L, SYNC.P invalid opcode by pcsx2
            AddInstruction("SYSCALL", INSTRUCTIONTYPE.Special, 0b001100, []);
            // In pcsx2, "teq $t0,$t1" assembles to 0x01094034, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TEQ", INSTRUCTIONTYPE.Special, 0b110100, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("TEQI", INSTRUCTIONTYPE.REGIMM, 0b01100, [OPERAND.rs, OPERAND.Immediate]);
            // In pcsx2, "tge $t0,$t1" assembles to 0x01094030, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TGE", INSTRUCTIONTYPE.Special, 0b110000, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("TGEI", INSTRUCTIONTYPE.REGIMM, 0b01000, [OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("TGEIU", INSTRUCTIONTYPE.REGIMM, 0b01001, [OPERAND.rs, OPERAND.Immediate]);
            // In pcsx2, "tgeu $t0,$t1" assembles to 0x01094031, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TGEU", INSTRUCTIONTYPE.Special, 0b110001, [OPERAND.rs, OPERAND.rt]);
            // In pcsx2, "tlt $t0,$t1" assembles to 0x01094032, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TLT", INSTRUCTIONTYPE.Special, 0b110010, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("TLTI", INSTRUCTIONTYPE.REGIMM, 0b01010, [OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("TLTIU", INSTRUCTIONTYPE.REGIMM, 0b01011, [OPERAND.rs, OPERAND.Immediate]);
            // In pcsx2, "tltu $t0,$t1" assembles to 0x01094033, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TLTU", INSTRUCTIONTYPE.Special, 0b110011, [OPERAND.rs, OPERAND.rt]);
            // In pcsx2, "tne $t0,$t1" assembles to 0x01094036, with the "0x40" as the "code" field. It doesn't seem that there is any difference what the "code" field is set to
            AddInstruction("TNE", INSTRUCTIONTYPE.Special, 0b110110, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("TNEI", INSTRUCTIONTYPE.REGIMM, 0b01110, [OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("XOR", INSTRUCTIONTYPE.Special, 0b100110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("XORI", INSTRUCTIONTYPE.Normal, 0b001110, [OPERAND.rt, OPERAND.rs, OPERAND.Immediate]);
            // EE specific
            AddInstruction("DIV1", INSTRUCTIONTYPE.MMI, 0b011010, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("DIVU1", INSTRUCTIONTYPE.MMI, 0b011011, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("LQ", INSTRUCTIONTYPE.Normal, 0b011110, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("MADD", INSTRUCTIONTYPE.MMI, 0b000000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MADD1", INSTRUCTIONTYPE.MMI, 0b100000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MADDU", INSTRUCTIONTYPE.MMI, 0b000001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MADDU1", INSTRUCTIONTYPE.MMI, 0b100001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("MFHI1", INSTRUCTIONTYPE.MMI, 0b010000, [OPERAND.rd]);
            AddInstruction("MFLO1", INSTRUCTIONTYPE.MMI, 0b010010, [OPERAND.rd]);
            AddInstruction("MFSA", INSTRUCTIONTYPE.Special, 0b101000, [OPERAND.rd]);
            AddInstruction("MTHI1", INSTRUCTIONTYPE.MMI, 0b010001, [OPERAND.rs]);
            AddInstruction("MTLO1", INSTRUCTIONTYPE.MMI, 0b010011, [OPERAND.rs]);
            AddInstruction("MTSA", INSTRUCTIONTYPE.Special, 0b101001, [OPERAND.rs]);
            AddInstruction("MTSAB", INSTRUCTIONTYPE.REGIMM, 0b11000, [OPERAND.rs, OPERAND.Immediate]);
            AddInstruction("MTSAH", INSTRUCTIONTYPE.REGIMM, 0b11001, [OPERAND.rs, OPERAND.Immediate]);
            // MULT, but already added
            AddInstruction("MULT1", INSTRUCTIONTYPE.MMI, 0b011000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            // MULTU, but already added
            AddInstruction("MULTU1", INSTRUCTIONTYPE.MMI, 0b011001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PABSH", INSTRUCTIONTYPE.MMI1, 0b00101, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PABSW", INSTRUCTIONTYPE.MMI1, 0b00001, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PADDB", INSTRUCTIONTYPE.MMI0, 0b01000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDH", INSTRUCTIONTYPE.MMI0, 0b00100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDSB", INSTRUCTIONTYPE.MMI0, 0b11000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDSH", INSTRUCTIONTYPE.MMI0, 0b10100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDSW", INSTRUCTIONTYPE.MMI0, 0b10000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDUB", INSTRUCTIONTYPE.MMI1, 0b11000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDUH", INSTRUCTIONTYPE.MMI1, 0b10100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDUW", INSTRUCTIONTYPE.MMI1, 0b10000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADDW", INSTRUCTIONTYPE.MMI0, 0b00000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PADSBH", INSTRUCTIONTYPE.MMI1, 0b00100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PAND", INSTRUCTIONTYPE.MMI2, 0b10010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCEQB", INSTRUCTIONTYPE.MMI1, 0b01010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCEQH", INSTRUCTIONTYPE.MMI1, 0b00110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCEQW", INSTRUCTIONTYPE.MMI1, 0b00010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCGTB", INSTRUCTIONTYPE.MMI0, 0b01010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCGTH", INSTRUCTIONTYPE.MMI0, 0b00110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCGTW", INSTRUCTIONTYPE.MMI0, 0b00010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            // For some reason pcsx2 copies "rd" to where "rs" is too, even though it should be 0.
            // Not implemented here
            AddInstruction("PCPYH", INSTRUCTIONTYPE.MMI3, 0b11011, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PCPYLD", INSTRUCTIONTYPE.MMI2, 0b01110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PCPYUD", INSTRUCTIONTYPE.MMI3, 0b01110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PDIVBW", INSTRUCTIONTYPE.MMI2, 0b11101, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("PDIVUW", INSTRUCTIONTYPE.MMI3, 0b01101, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("PDIVW", INSTRUCTIONTYPE.MMI2, 0b01101, [OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXCH", INSTRUCTIONTYPE.MMI3, 0b11010, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PEXCW", INSTRUCTIONTYPE.MMI3, 0b11110, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PEXEH", INSTRUCTIONTYPE.MMI2, 0b11010, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PEXEW", INSTRUCTIONTYPE.MMI2, 0b11110, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PEXT5", INSTRUCTIONTYPE.MMI0, 0b11110, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PEXTLB", INSTRUCTIONTYPE.MMI0, 0b11010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXTLH", INSTRUCTIONTYPE.MMI0, 0b10110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXTLW", INSTRUCTIONTYPE.MMI0, 0b10010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXTUB", INSTRUCTIONTYPE.MMI1, 0b11010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXTUH", INSTRUCTIONTYPE.MMI1, 0b10110, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PEXTUW", INSTRUCTIONTYPE.MMI1, 0b10010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PHMADH", INSTRUCTIONTYPE.MMI2, 0b10001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PHMSBH", INSTRUCTIONTYPE.MMI2, 0b10101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PINTEH", INSTRUCTIONTYPE.MMI3, 0b01010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PINTH", INSTRUCTIONTYPE.MMI2, 0b01010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PLZCW", INSTRUCTIONTYPE.MMI, 0b000100, [OPERAND.rd, OPERAND.rs]);
            AddInstruction("PMADDH", INSTRUCTIONTYPE.MMI2, 0b10000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMADDUW", INSTRUCTIONTYPE.MMI3, 0b00000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMADDW", INSTRUCTIONTYPE.MMI2, 0b00000, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMAXH", INSTRUCTIONTYPE.MMI0, 0b00111, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMAXW", INSTRUCTIONTYPE.MMI0, 0b00011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMFHI", INSTRUCTIONTYPE.MMI2, 0b01000, [OPERAND.rd]);
            // For PMFHL, the opcode is the format
            AddInstruction("PMFHL.LH", INSTRUCTIONTYPE.ParallelMoveFromHILORegister, 0b00011, [OPERAND.rd]);
            AddInstruction("PMFHL.LW", INSTRUCTIONTYPE.ParallelMoveFromHILORegister, 0b00000, [OPERAND.rd]);
            AddInstruction("PMFHL.SH", INSTRUCTIONTYPE.ParallelMoveFromHILORegister, 0b00100, [OPERAND.rd]);
            AddInstruction("PMFHL.SLW", INSTRUCTIONTYPE.ParallelMoveFromHILORegister, 0b00010, [OPERAND.rd]);
            AddInstruction("PMFHL.UW", INSTRUCTIONTYPE.ParallelMoveFromHILORegister, 0b00001, [OPERAND.rd]);
            AddInstruction("PMFLO", INSTRUCTIONTYPE.MMI2, 0b01001, [OPERAND.rd]);
            AddInstruction("PMINH", INSTRUCTIONTYPE.MMI1, 0b00111, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMINW", INSTRUCTIONTYPE.MMI1, 0b00011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMSUBH", INSTRUCTIONTYPE.MMI2, 0b10100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMSUBW", INSTRUCTIONTYPE.MMI2, 0b00100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMTHI", INSTRUCTIONTYPE.MMI3, 0b01000, [OPERAND.rs]);
            // For PMTHL, the opcode is the format. Only .lw is valid(?)
            AddInstruction("PMTHL.LW", INSTRUCTIONTYPE.ParallelMoveToHILORegister, 0b00000, [OPERAND.rs]);
            AddInstruction("PMTLO", INSTRUCTIONTYPE.MMI3, 0b01001, [OPERAND.rs]);
            AddInstruction("PMULTH", INSTRUCTIONTYPE.MMI2, 0b11100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMULTUW", INSTRUCTIONTYPE.MMI3, 0b01100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PMULTW", INSTRUCTIONTYPE.MMI2, 0b01100, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PNOR", INSTRUCTIONTYPE.MMI3, 0b10011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("POR", INSTRUCTIONTYPE.MMI3, 0b10010, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PPAC5", INSTRUCTIONTYPE.MMI0, 0b11111, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PPACB", INSTRUCTIONTYPE.MMI0, 0b11011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PPACH", INSTRUCTIONTYPE.MMI0, 0b10111, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PPACW", INSTRUCTIONTYPE.MMI0, 0b10011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PREVH", INSTRUCTIONTYPE.MMI2, 0b11011, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PROT3W", INSTRUCTIONTYPE.MMI2, 0b11111, [OPERAND.rd, OPERAND.rt]);
            AddInstruction("PSLLH", INSTRUCTIONTYPE.MMI, 0b110100, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            // PSLLVW: rt and rs are switched in pcsx2 (bug?)
            // psllvw $t0,$t1,$t2 in pcsx2: 712A4089.
            // But it does $t0 <- $t2 << $t1,
            // instead of  $t0 <- $t1 << $t2
            // Implemented here like pcsx2 does
            AddInstruction("PSLLVW", INSTRUCTIONTYPE.MMI2, 0b00010, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("PSLLW", INSTRUCTIONTYPE.MMI, 0b111100, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("PSRAH", INSTRUCTIONTYPE.MMI, 0b110111, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("PSRAVW", INSTRUCTIONTYPE.MMI3, 0b00011, [OPERAND.rd, OPERAND.rt, OPERAND.rs]);
            AddInstruction("PSRAW", INSTRUCTIONTYPE.MMI, 0b111111, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            // PSRLH invalid opcode by pcsx2
            AddInstruction("PSRLH", INSTRUCTIONTYPE.MMI, 0b110110, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            // PSRLVW: rt and rs are switched in pcsx2 (bug?)
            // psrlvw $t0,$t1,$t2 in pcsx2: 712A40C9.
            // But it does $t0 <- $t2 >> $t1,
            // instead of  $t0 <- $t1 >> $t2
            // Implemented here like pcsx2 does
            AddInstruction("PSRLVW", INSTRUCTIONTYPE.MMI2, 0b00011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSRLW", INSTRUCTIONTYPE.MMI, 0b111110, [OPERAND.rd, OPERAND.rt, OPERAND.ShiftAmount]);
            AddInstruction("PSUBB", INSTRUCTIONTYPE.MMI0, 0b01001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBH", INSTRUCTIONTYPE.MMI0, 0b00101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBSB", INSTRUCTIONTYPE.MMI0, 0b11001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBSH", INSTRUCTIONTYPE.MMI0, 0b10101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBSW", INSTRUCTIONTYPE.MMI0, 0b10001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBUB", INSTRUCTIONTYPE.MMI1, 0b11001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBUH", INSTRUCTIONTYPE.MMI1, 0b10101, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBUW", INSTRUCTIONTYPE.MMI1, 0b10001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PSUBW", INSTRUCTIONTYPE.MMI0, 0b00001, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("PXOR", INSTRUCTIONTYPE.MMI2, 0b10011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("QFSRV", INSTRUCTIONTYPE.MMI1, 0b11011, [OPERAND.rd, OPERAND.rs, OPERAND.rt]);
            AddInstruction("SQ", INSTRUCTIONTYPE.Normal, 0b011111, [OPERAND.rt, OPERAND.Immediate, OPERAND.rs]);
            // COP0...

            // COP1
            AddInstruction("ABS.S", INSTRUCTIONTYPE.FPUS, 0b000101, [OPERAND.fd, OPERAND.fs]);
            AddInstruction("ADD.S", INSTRUCTIONTYPE.FPUS, 0b000000, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("ADDA.S", INSTRUCTIONTYPE.FPUS, 0b011000, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("BC1F", INSTRUCTIONTYPE.BC1, 0b00000, [OPERAND.Label]);
            AddInstruction("BC1FL", INSTRUCTIONTYPE.BC1, 0b00010, [OPERAND.Label]);
            AddInstruction("BC1T", INSTRUCTIONTYPE.BC1, 0b00001, [OPERAND.Label]);
            AddInstruction("BC1TL", INSTRUCTIONTYPE.BC1, 0b00011, [OPERAND.Label]);
            AddInstruction("C.EQ.S", INSTRUCTIONTYPE.FPUS, 0b110010, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("C.F.S", INSTRUCTIONTYPE.FPUS, 0b110000, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("C.LE.S", INSTRUCTIONTYPE.FPUS, 0b110110, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("C.LT.S", INSTRUCTIONTYPE.FPUS, 0b110100, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("CFC1", INSTRUCTIONTYPE.COP1, 0b00010, [OPERAND.rt, OPERAND.fs]);
            AddInstruction("CTC1", INSTRUCTIONTYPE.COP1, 0b00110, [OPERAND.rt, OPERAND.fs]);
            AddInstruction("CVT.S.W", INSTRUCTIONTYPE.FPUW, 0b100000, [OPERAND.fd, OPERAND.fs]);
            AddInstruction("CVT.W.S", INSTRUCTIONTYPE.FPUS, 0b100100, [OPERAND.fd, OPERAND.fs]);
            AddInstruction("DIV.S", INSTRUCTIONTYPE.FPUS, 0b000011, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("LWC1", INSTRUCTIONTYPE.Normal, 0b110001, [OPERAND.ft, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("MADD.S", INSTRUCTIONTYPE.FPUS, 0b011100, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("MADDA.S", INSTRUCTIONTYPE.FPUS, 0b011110, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("MAX.S", INSTRUCTIONTYPE.FPUS, 0b101000, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("MFC1", INSTRUCTIONTYPE.COP1, 0b00000, [OPERAND.rt, OPERAND.fs]);
            AddInstruction("MIN.S", INSTRUCTIONTYPE.FPUS, 0b101001, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("MOV.S", INSTRUCTIONTYPE.FPUS, 0b000110, [OPERAND.fd, OPERAND.fs]);
            AddInstruction("MSUB.S", INSTRUCTIONTYPE.FPUS, 0b011101, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("MSUBA.S", INSTRUCTIONTYPE.FPUS, 0b011111, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("MTC1", INSTRUCTIONTYPE.COP1, 0b00100, [OPERAND.rt, OPERAND.fs]);
            AddInstruction("MUL.S", INSTRUCTIONTYPE.FPUS, 0b000010, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("MULA.S", INSTRUCTIONTYPE.FPUS, 0b011010, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("NEG.S", INSTRUCTIONTYPE.FPUS, 0b000111, [OPERAND.fd, OPERAND.fs]);
            AddInstruction("RSQRT.S", INSTRUCTIONTYPE.FPUS, 0b010110, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            // SQRT.S: ft and fs are switched in pcsx2 (bug?)
            // sqrt.s $f10,$f11 in pcsx2: 46005A84. It changes it to "sqrt.s $f10,$f00" and it places "ft" where fs usually is (bits 15-11)
            // This makes it so that if you "assemble instruction" again, it becomes 46000284. Making ft (bits 20-16) and where fs usually is (bits 15-11) 0s
            // Implemented here like pcsx2 does
            AddInstruction("SQRT.S", INSTRUCTIONTYPE.FPUS, 0b000100, [OPERAND.fd, OPERAND.ft]);
            AddInstruction("SUB.S", INSTRUCTIONTYPE.FPUS, 0b000001, [OPERAND.fd, OPERAND.fs, OPERAND.ft]);
            AddInstruction("SUBA.S", INSTRUCTIONTYPE.FPUS, 0b011001, [OPERAND.fs, OPERAND.ft]);
            AddInstruction("SWC1", INSTRUCTIONTYPE.Normal, 0b111001, [OPERAND.ft, OPERAND.Immediate, OPERAND.rs]);

            // VU
            // BC2F, BC2FL, BC2T, BC2TL invalid opcode by pcsx2
            AddInstruction("CFC2", INSTRUCTIONTYPE.COP2, 0b00010, [OPERAND.rt, OPERAND.vid]);
            AddInstruction("CTC2", INSTRUCTIONTYPE.COP2, 0b00110, [OPERAND.rt, OPERAND.vid]);
            AddInstruction("LQC2", INSTRUCTIONTYPE.Normal, 0b110110, [OPERAND.vft, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("QMFC2", INSTRUCTIONTYPE.COP2, 0b00001, [OPERAND.rt, OPERAND.vfd]);
            AddInstruction("QMTC2", INSTRUCTIONTYPE.COP2, 0b00101, [OPERAND.rt, OPERAND.vfd]);
            AddInstruction("SQC2", INSTRUCTIONTYPE.Normal, 0b111110, [OPERAND.vft, OPERAND.Immediate, OPERAND.rs]);
            AddInstruction("VABS", INSTRUCTIONTYPE.COP2Special2, 0b0011101, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VADD", INSTRUCTIONTYPE.COP2Special1, 0b101000, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VADDI", INSTRUCTIONTYPE.COP2Special1, 0b100010, [OPERAND.vfd, OPERAND.vfs]);
            // Without the Q parameter
            AddInstruction("VADDQ", INSTRUCTIONTYPE.COP2Special1, 0b100000, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VADDX", INSTRUCTIONTYPE.COP2Special1, 0b000000, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDY", INSTRUCTIONTYPE.COP2Special1, 0b000001, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDZ", INSTRUCTIONTYPE.COP2Special1, 0b000010, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDW", INSTRUCTIONTYPE.COP2Special1, 0b000011, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the ACC parameter
            AddInstruction("VADDA", INSTRUCTIONTYPE.COP2Special2, 0b0101000, [OPERAND.vfs, OPERAND.vft]);
            // Without the I and ACC parameters
            AddInstruction("VADDAI", INSTRUCTIONTYPE.COP2Special2, 0b0100010, [OPERAND.vfs]);
            // Without the Q and ACC parameters
            AddInstruction("VADDAQ", INSTRUCTIONTYPE.COP2Special2, 0b0100000, [OPERAND.vfs]);
            // Without the ACC parameter and the x/y/z/w letter for the last parameter
            AddInstruction("VADDAX", INSTRUCTIONTYPE.COP2Special2, 0b0000000, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDAY", INSTRUCTIONTYPE.COP2Special2, 0b0000001, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDAZ", INSTRUCTIONTYPE.COP2Special2, 0b0000010, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VADDAW", INSTRUCTIONTYPE.COP2Special2, 0b0000011, [OPERAND.vfs, OPERAND.vft]);
            // VCALLMS, VCALLMSR
            // Without the letters in the parameters
            AddInstruction("VCLIPW.XYZ", INSTRUCTIONTYPE.COP2Special2, 0b0011111, [OPERAND.vfs, OPERAND.vft]);
            // VDIV
            AddInstruction("VFTOI0", INSTRUCTIONTYPE.COP2Special2, 0b0010100, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VFTOI4", INSTRUCTIONTYPE.COP2Special2, 0b0010101, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VFTOI12", INSTRUCTIONTYPE.COP2Special2, 0b0010110, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VFTOI15", INSTRUCTIONTYPE.COP2Special2, 0b0010111, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VIADD", INSTRUCTIONTYPE.COP2Special1, 0b110000, [OPERAND.vid, OPERAND.vis, OPERAND.vit]);
            AddInstruction("VIADDI", INSTRUCTIONTYPE.COP2Special1, 0b110010, [OPERAND.vit, OPERAND.vis, OPERAND.Immediate5]);
            AddInstruction("VIAND", INSTRUCTIONTYPE.COP2Special1, 0b110100, [OPERAND.vid, OPERAND.vis, OPERAND.vit]);
            // VILWR
            AddInstruction("VIOR", INSTRUCTIONTYPE.COP2Special1, 0b110101, [OPERAND.vid, OPERAND.vis, OPERAND.vit]);
            AddInstruction("VISUB", INSTRUCTIONTYPE.COP2Special1, 0b110001, [OPERAND.vid, OPERAND.vis, OPERAND.vit]);
            // VISWR
            AddInstruction("VITOF0", INSTRUCTIONTYPE.COP2Special2, 0b0010000, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VITOF4", INSTRUCTIONTYPE.COP2Special2, 0b0010001, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VITOF12", INSTRUCTIONTYPE.COP2Special2, 0b0010010, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VITOF15", INSTRUCTIONTYPE.COP2Special2, 0b0010011, [OPERAND.vft, OPERAND.vfs]);
            // VLQD, in pcsx2 "dest" is not repeated after the is register
            // VLQI, in pcsx2 "dest" is not repeated after the is register
            AddInstruction("VMADD", INSTRUCTIONTYPE.COP2Special1, 0b101001, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VMADDI", INSTRUCTIONTYPE.COP2Special1, 0b100011, [OPERAND.vfd, OPERAND.vfs]);
            // Without the Q parameter
            AddInstruction("VMADDQ", INSTRUCTIONTYPE.COP2Special1, 0b100001, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VMADDX", INSTRUCTIONTYPE.COP2Special1, 0b001000, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDY", INSTRUCTIONTYPE.COP2Special1, 0b001001, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDZ", INSTRUCTIONTYPE.COP2Special1, 0b001010, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDW", INSTRUCTIONTYPE.COP2Special1, 0b001011, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the ACC parameter
            AddInstruction("VMADDA", INSTRUCTIONTYPE.COP2Special2, 0b0101001, [OPERAND.vfs, OPERAND.vft]);
            // Without the I and ACC parameters
            AddInstruction("VMADDAI", INSTRUCTIONTYPE.COP2Special2, 0b0100011, [OPERAND.vfs]);
            // Without the Q and ACC parameters
            AddInstruction("VMADDAQ", INSTRUCTIONTYPE.COP2Special2, 0b0100001, [OPERAND.vfs]);
            // Without the ACC parameter and the x/y/z/w letter for the last parameter
            AddInstruction("VMADDAX", INSTRUCTIONTYPE.COP2Special2, 0b0001000, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDAY", INSTRUCTIONTYPE.COP2Special2, 0b0001001, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDAZ", INSTRUCTIONTYPE.COP2Special2, 0b0001010, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMADDAW", INSTRUCTIONTYPE.COP2Special2, 0b0001011, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMAX", INSTRUCTIONTYPE.COP2Special1, 0b101011, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VMAXI", INSTRUCTIONTYPE.COP2Special1, 0b011101, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VMAXX", INSTRUCTIONTYPE.COP2Special1, 0b010000, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMAXY", INSTRUCTIONTYPE.COP2Special1, 0b010001, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMAXZ", INSTRUCTIONTYPE.COP2Special1, 0b010010, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMAXW", INSTRUCTIONTYPE.COP2Special1, 0b010011, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // VMFIR: no dest after mnemonic in pcsx2, and "p" letter after vft
            AddInstruction("VMFIR", INSTRUCTIONTYPE.COP2Special2, 0b0111101, [OPERAND.vft, OPERAND.vis]);
            AddInstruction("VMINI", INSTRUCTIONTYPE.COP2Special1, 0b101111, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VMINII", INSTRUCTIONTYPE.COP2Special1, 0b011111, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VMINIX", INSTRUCTIONTYPE.COP2Special1, 0b010100, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMINIY", INSTRUCTIONTYPE.COP2Special1, 0b010101, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMINIZ", INSTRUCTIONTYPE.COP2Special1, 0b010110, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMINIW", INSTRUCTIONTYPE.COP2Special1, 0b010111, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMOVE", INSTRUCTIONTYPE.COP2Special2, 0b0110000, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VMR32", INSTRUCTIONTYPE.COP2Special2, 0b0110001, [OPERAND.vft, OPERAND.vfs]);
            AddInstruction("VMSUB", INSTRUCTIONTYPE.COP2Special1, 0b101101, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VMSUBI", INSTRUCTIONTYPE.COP2Special1, 0b100111, [OPERAND.vfd, OPERAND.vfs]);
            // Without the Q parameter
            AddInstruction("VMSUBQ", INSTRUCTIONTYPE.COP2Special1, 0b100101, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VMSUBX", INSTRUCTIONTYPE.COP2Special1, 0b001100, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBY", INSTRUCTIONTYPE.COP2Special1, 0b001101, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBZ", INSTRUCTIONTYPE.COP2Special1, 0b001110, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBW", INSTRUCTIONTYPE.COP2Special1, 0b001111, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the ACC parameter
            AddInstruction("VMSUBA", INSTRUCTIONTYPE.COP2Special2, 0b0101101, [OPERAND.vfs, OPERAND.vft]);
            // Without the I and ACC parameters
            AddInstruction("VMSUBAI", INSTRUCTIONTYPE.COP2Special2, 0b0100111, [OPERAND.vfs]);
            // Without the Q and ACC parameters
            AddInstruction("VMSUBAQ", INSTRUCTIONTYPE.COP2Special2, 0b0100101, [OPERAND.vfs]);
            // Without the ACC parameter and the x/y/z/w letter for the last parameter
            AddInstruction("VMSUBAX", INSTRUCTIONTYPE.COP2Special2, 0b0001100, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBAY", INSTRUCTIONTYPE.COP2Special2, 0b0001101, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBAZ", INSTRUCTIONTYPE.COP2Special2, 0b0001110, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMSUBAW", INSTRUCTIONTYPE.COP2Special2, 0b0001111, [OPERAND.vfs, OPERAND.vft]);
            // VMTIR
            AddInstruction("VMUL", INSTRUCTIONTYPE.COP2Special1, 0b101010, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VMULI", INSTRUCTIONTYPE.COP2Special1, 0b011110, [OPERAND.vfd, OPERAND.vfs]);
            // Without the Q parameter
            AddInstruction("VMULQ", INSTRUCTIONTYPE.COP2Special1, 0b011100, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VMULX", INSTRUCTIONTYPE.COP2Special1, 0b011000, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULY", INSTRUCTIONTYPE.COP2Special1, 0b011001, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULZ", INSTRUCTIONTYPE.COP2Special1, 0b011010, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULW", INSTRUCTIONTYPE.COP2Special1, 0b011011, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the ACC parameter
            AddInstruction("VMULA", INSTRUCTIONTYPE.COP2Special2, 0b0101010, [OPERAND.vfs, OPERAND.vft]);
            // Without the I and ACC parameters
            AddInstruction("VMULAI", INSTRUCTIONTYPE.COP2Special2, 0b0011110, [OPERAND.vfs]);
            // Without the Q and ACC parameters
            AddInstruction("VMULAQ", INSTRUCTIONTYPE.COP2Special2, 0b0011100, [OPERAND.vfs]);
            // Without the ACC parameter and the x/y/z/w letter for the last parameter
            AddInstruction("VMULAX", INSTRUCTIONTYPE.COP2Special2, 0b0011000, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULAY", INSTRUCTIONTYPE.COP2Special2, 0b0011001, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULAZ", INSTRUCTIONTYPE.COP2Special2, 0b0011010, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VMULAW", INSTRUCTIONTYPE.COP2Special2, 0b0011011, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VNOP", INSTRUCTIONTYPE.COP2Special2, 0b0101111, []);
            // Without the ACC parameter and the xyz letters for the parameters
            AddInstruction("VOPMULA.XYZ", INSTRUCTIONTYPE.COP2Special2, 0b0101110, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VOPMSUB.XYZ", INSTRUCTIONTYPE.COP2Special1, 0b101110, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the R parameter and the xyz letters for the vft parameter
            AddInstruction("VRGET", INSTRUCTIONTYPE.COP2Special2, 0b1000001, [OPERAND.vft]);
            // VRINIT
            // Without the R parameter and the xyz letter for the vft parameter
            AddInstruction("VRNEXT", INSTRUCTIONTYPE.COP2Special2, 0b1000000, [OPERAND.vft]);
            // VRSQRT
            // VRXOR
            // VSQD
            // VSQI
            // VSQRT
            AddInstruction("VSUB", INSTRUCTIONTYPE.COP2Special1, 0b101100, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the I parameter
            AddInstruction("VSUBI", INSTRUCTIONTYPE.COP2Special1, 0b100110, [OPERAND.vfd, OPERAND.vfs]);
            // Without the Q parameter
            AddInstruction("VSUBQ", INSTRUCTIONTYPE.COP2Special1, 0b100100, [OPERAND.vfd, OPERAND.vfs]);
            // Without the x/y/z/w letter for the last parameter
            AddInstruction("VSUBX", INSTRUCTIONTYPE.COP2Special1, 0b000100, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBY", INSTRUCTIONTYPE.COP2Special1, 0b000101, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBZ", INSTRUCTIONTYPE.COP2Special1, 0b000110, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBW", INSTRUCTIONTYPE.COP2Special1, 0b000111, [OPERAND.vfd, OPERAND.vfs, OPERAND.vft]);
            // Without the ACC parameter
            AddInstruction("VSUBA", INSTRUCTIONTYPE.COP2Special2, 0b0101100, [OPERAND.vfs, OPERAND.vft]);
            // Without the I and ACC parameters
            AddInstruction("VSUBAI", INSTRUCTIONTYPE.COP2Special2, 0b0100110, [OPERAND.vfs]);
            // Without the Q and ACC parameters
            AddInstruction("VSUBAQ", INSTRUCTIONTYPE.COP2Special2, 0b0100100, [OPERAND.vfs]);
            // Without the ACC parameter and the x/y/z/w letter for the last parameter
            AddInstruction("VSUBAX", INSTRUCTIONTYPE.COP2Special2, 0b0000100, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBAY", INSTRUCTIONTYPE.COP2Special2, 0b0000101, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBAZ", INSTRUCTIONTYPE.COP2Special2, 0b0000110, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VSUBAW", INSTRUCTIONTYPE.COP2Special2, 0b0000111, [OPERAND.vfs, OPERAND.vft]);
            AddInstruction("VWAITQ", INSTRUCTIONTYPE.COP2Special2, 0b0111011, []);
        }
    }
}