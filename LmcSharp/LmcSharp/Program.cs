using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LmcLib;

namespace LmcSharp
{
    internal class Program
    {
        private static readonly TextWriter debugOutput;

        static Program()
        {
#if DEBUG
            debugOutput = Console.Out;
#else
            debugOutput = TextWriter.Null;
#endif
        }

        /// <summary>
        /// Assembles the given assembler instructions into a binary serialised form that can be executed on a computer instance.
        /// </summary>
        /// <param name="programLines">The string contents of the program.</param>
        /// <param name="mnemonicToOpcodeMap">Maps assembler mnemonics into opcodes.</param>
        /// <param name="instructionSet">Maps opcodes into instructions.</param>
        /// <param name="operandAddressModeMap">Maps opcodes to the address modes for their parameters.</param>
        /// <returns>The binary serialised program, such that it can be executed on a computer.</returns>
        private static byte[] Assemble(IEnumerable<string> programLines, IReadOnlyDictionary<string, byte> mnemonicToOpcodeMap,
            IReadOnlyDictionary<byte, Instruction> instructionSet, IReadOnlyDictionary<byte, AddressModes[]> operandAddressModeMap)
        {
            List<string> programLinesEnumerable = programLines as List<string> ?? programLines.ToList();

            //
            static Dictionary<string, int> GetLabels(in IReadOnlyList<string> programLines,
                in ISet<string> mnemonics)
            {
                Dictionary<string, int> addresses = new Dictionary<string, int>();

                int addressCounter = 0;
                for (int i = 0; i < programLines.Count; i++)
                {
                    string line = programLines[i];

                    if (string.IsNullOrWhiteSpace(line)) { continue; }

                    string[] spaceSplit = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string instruction;
                    string[] operands;

                    if (!mnemonics.Contains(spaceSplit[0]))
                    {
                        // label is first
                        string label = spaceSplit[0];

                        instruction = spaceSplit[1];
                        operands = spaceSplit[2..];

                        // we cache the start address so as to get the correct memory address for the label
                        int cachedAddressCounter = addressCounter;

                        addressCounter += (instruction.Equals("DAT") ? 0 : sizeof(ushort)) +
                                          operands.Length * InstructionOperand.OperandSize;

                        addresses[label] = cachedAddressCounter;
                    }
                    else
                    {
                        // instruction is first
                        instruction = spaceSplit[0];
                        operands = spaceSplit[1..];

                        addressCounter += (instruction.Equals("DAT") ? 0 : sizeof(ushort)) +
                                          operands.Length * InstructionOperand.OperandSize;
                    }
                }

                return addresses;
            }

            Dictionary<string, int> labelAddresses =
                GetLabels(programLinesEnumerable, mnemonicToOpcodeMap.Keys.ToHashSet());

            List<(Instruction instruction, InstructionOperand[] operands)> lmcInstructions =
                new List<(Instruction instruction, InstructionOperand[] operands)>();

            // converts program lines into a list of instructions
            for (int i = 0; i < programLinesEnumerable.Count; i++)
            {
                string line = programLinesEnumerable[i];

                if (string.IsNullOrWhiteSpace(line)) { continue; }

                string[] spaceSplit = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                string mnemonic;
                string[] operands;

                if (labelAddresses.ContainsKey(spaceSplit[0]))
                {
                    mnemonic = spaceSplit[1].ToUpperInvariant();
                    operands = spaceSplit[2..];
                }
                else
                {
                    mnemonic = spaceSplit[0].ToUpperInvariant();
                    operands = spaceSplit[1..];
                }

                if (!mnemonicToOpcodeMap.ContainsKey(mnemonic))
                {
                    throw new ArgumentException(
                        $"The encountered mnemonic, \'{mnemonic}\', does not exist in the mnemonic map",
                        nameof(mnemonicToOpcodeMap));
                }

                byte opcode = mnemonicToOpcodeMap[mnemonic];

                if (!instructionSet.ContainsKey(opcode))
                {
                    throw new ArgumentException(
                        $"The encountered opcode, \'{opcode}\', does not exist in the instruction set",
                        nameof(instructionSet));
                }

                // maps string instruction
                Instruction mappedInstruction = instructionSet[opcode];

                if (operands.Length != mappedInstruction.OperandCount)
                {
                    throw new Exception(
                        $"The instruction, \'{mappedInstruction.Mnemonic}\', did not get the correct number of operands " +
                        $"({mappedInstruction.OperandCount}); it got {operands.Length} instead");
                }

                // maps string operands
                InstructionOperand[] mappedOperands = new InstructionOperand[mappedInstruction.OperandCount];
                for (int j = 0; j < mappedInstruction.OperandCount; j++)
                {
                    if (long.TryParse(operands[j], out long value))
                    {
                        byte[] serialisedOperand = BitConverter.GetBytes(value);

                        AddressModes operandAddressMode = operandAddressModeMap[mappedInstruction.Opcode][j];
                        mappedOperands[j] =
                            new InstructionOperand(operandAddressMode, new ArraySegment<byte>(serialisedOperand));
                    }
                    else
                    {
                        if (labelAddresses.TryGetValue(operands[j], out _))
                        {
                            long labelLineAddress = labelAddresses[operands[j]];
                            byte[] serialisedOperand = BitConverter.GetBytes(labelLineAddress);

                            AddressModes operandAddressMode = operandAddressModeMap[mappedInstruction.Opcode][j];
                            mappedOperands[j] =
                                new InstructionOperand(operandAddressMode, new ArraySegment<byte>(serialisedOperand));
                        }
                        else
                        {
                            throw new Exception($"Could not find label \'{operands[j]}\' in program. Labels are case-sensitive");
                        }
                    }
                }

                // adds the mapped instruction and operands to the instruction list
                lmcInstructions.Add((mappedInstruction, mappedOperands));
            }

            if (lmcInstructions.Count == 0)
            {
                lmcInstructions.Add((Instruction.Halt, new InstructionOperand[0]));
            }

            // serialises instructions to byte memory
            List<byte> assembledProgram = new List<byte>();
            foreach ((Instruction instruction, InstructionOperand[] operands) in lmcInstructions)
            {
                if (instruction.Mnemonic.Equals("DAT"))
                {
                    assembledProgram.AddRange(operands[0].Value);
                }
                else
                {
                    ushort serialisedOpcode = instruction.Opcode;

                    byte[] serialisedInstructionBytes =
                        new byte[sizeof(ushort) + operands.Length * InstructionOperand.OperandSize];

                    Memory<byte> serialisedOpcodeMemory =
                        new Memory<byte>(serialisedInstructionBytes, 0, sizeof(ushort));

                    Memory<byte> serialisedOperandMemory =
                        new Memory<byte>(serialisedInstructionBytes, sizeof(ushort),
                            operands.Length * InstructionOperand.OperandSize);

                    for (ushort i = 0; i < operands.Length; i++)
                    {
                        ushort operandAddressMode = (ushort)operands[i].AddressingMode;

                        ushort serialisedOperandAddressMode = (ushort)(operandAddressMode << (8 + 2 * i));
                        serialisedOpcode |= serialisedOperandAddressMode;

                        Memory<byte> currentOperandMemory =
                            serialisedOperandMemory.Slice(i * InstructionOperand.OperandSize,
                                InstructionOperand.OperandSize);

                        operands[i].Value.AsMemory().CopyTo(currentOperandMemory);
                    }

                    BitConverter.GetBytes(serialisedOpcode).CopyTo(serialisedOpcodeMemory);

                    assembledProgram.AddRange(serialisedInstructionBytes);
                }
            }

            return assembledProgram.ToArray();
        }

        private static InstructionResult LmcAdd(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc addition instruction");

            ArraySegment<byte> unwrappedOperand =
                Computer.UnwrapOperand(globalMemory, operands[0], InstructionOperand.OperandSize);
            long operandValue = BitConverter.ToInt64(unwrappedOperand);

            long accumulatorValue = registers["ACC"].GetContents();
            accumulatorValue += operandValue;
            registers["ACC"].SetContents(accumulatorValue);

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcBra(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc unconditional jump instruction");

            return new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value));
        }

        private static InstructionResult LmcBrp(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc conditional jump (x >= 0) instruction");

            bool condition = registers["ACC"].GetContents() >= 0;

            return condition
                ? new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value))
                : InstructionResult.Continue;
        }

        private static InstructionResult LmcBrz(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc conditional jump (x == 0) instruction");

            bool condition = registers["ACC"].GetContents() == 0;

            return condition
                ? new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value))
                : InstructionResult.Continue;
        }

        private static InstructionResult LmcDat(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            throw new Exception("This opcode is invalid as it shouldn't be executed");
        }

        private static InstructionResult LmcInp(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc input instruction");

            long inputValue = long.TryParse(input.ReadLine(), out long value) ? value : 0;
            registers["ACC"].SetContents(inputValue);

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcLda(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc load instruction");

            ArraySegment<byte> unwrappedOperand =
                Computer.UnwrapOperand(globalMemory, operands[0], InstructionOperand.OperandSize);
            long operandValue = BitConverter.ToInt64(unwrappedOperand);

            registers["ACC"].SetContents(operandValue);

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcNul(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            throw new Exception("This opcode is invalid in the LMC and causes an error.");
        }

        private static InstructionResult LmcOtc(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc character output instruction");

            output.WriteLine($"{(char)registers["ACC"].GetContents()}");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcOut(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc value output instruction");

            output.WriteLine($"{registers["ACC"].GetContents()}");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcSta(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc store instruction");

            ArraySegment<byte> unwrappedOperand =
                Computer.UnwrapOperand(globalMemory, operands[0], InstructionOperand.OperandSize);
            long operandValue = BitConverter.ToInt64(unwrappedOperand);

            Memory<byte> targetMemory = globalMemory.AsMemory((int)operandValue, InstructionOperand.OperandSize);
            registers["ACC"].Contents.CopyTo(targetMemory);

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcSub(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc subtraction instruction");

            ArraySegment<byte> unwrappedOperand =
                Computer.UnwrapOperand(globalMemory, operands[0], InstructionOperand.OperandSize);
            long operandValue = BitConverter.ToInt64(unwrappedOperand);

            long accumulatorValue = registers["ACC"].GetContents();
            accumulatorValue -= operandValue;
            registers["ACC"].SetContents(accumulatorValue);

            return InstructionResult.Continue;
        }

        private static void Main()
        {
            Console.WriteLine("Hello World!");

            Computer lmc = new Computer();

            lmc.TryRegisterInstruction(Instruction.Halt);
            lmc.TryRegisterInstruction(new Instruction(1, 1, "ADD", LmcAdd));
            lmc.TryRegisterInstruction(new Instruction(2, 1, "SUB", LmcSub));
            lmc.TryRegisterInstruction(new Instruction(3, 1, "STA", LmcSta));
            lmc.TryRegisterInstruction(new Instruction(4, 1, "NUL", LmcNul));
            lmc.TryRegisterInstruction(new Instruction(5, 1, "LDA", LmcLda));
            lmc.TryRegisterInstruction(new Instruction(6, 1, "BRA", LmcBra));
            lmc.TryRegisterInstruction(new Instruction(7, 1, "BRZ", LmcBrz));
            lmc.TryRegisterInstruction(new Instruction(8, 1, "BRP", LmcBrp));
            lmc.TryRegisterInstruction(new Instruction(9, 0, "INP", LmcInp));
            lmc.TryRegisterInstruction(new Instruction(10, 0, "OUT", LmcOut));
            lmc.TryRegisterInstruction(new Instruction(11, 0, "OTC", LmcOtc));
            lmc.TryRegisterInstruction(new Instruction(255, 1, "DAT", LmcDat));

            lmc.SetInput(Console.In);
            lmc.SetOutput(Console.Out);

            Dictionary<string, byte> mnemonicToOpcodeMap = new Dictionary<string, byte>
            {
                { "HLT", 0 },
                { "ADD", 1 },
                { "SUB", 2 },
                { "STA", 3 },
                { "STO", 3 },
                { "NUL", 4 },
                { "LDA", 5 },
                { "BRA", 6 },
                { "BRZ", 7 },
                { "BRP", 8 },
                { "INP", 9 },
                { "OUT", 10 },
                { "OTC", 11 },
                { "DAT", 255 }
            };

            AddressModes[] nullAddressMode = new AddressModes[0],
                directAddressMode = { AddressModes.Direct },
                immediateAddressMode = { AddressModes.Immediate };

            Dictionary<byte, AddressModes[]> operandAddressModeMap = new Dictionary<byte, AddressModes[]>
            {
                { 0, nullAddressMode },
                { 1, directAddressMode },
                { 2, directAddressMode },
                { 3, immediateAddressMode },
                { 4, nullAddressMode },
                { 5, directAddressMode },
                { 6, immediateAddressMode },
                { 7, immediateAddressMode },
                { 8, immediateAddressMode },
                { 9, nullAddressMode },
                { 10, nullAddressMode },
                { 11, nullAddressMode },
                { 255, immediateAddressMode },
            };

            string[] exampleLines = File.ReadAllLines(Path.GetFullPath(@".\exampleCode.txt"));

            byte[] lmcProgram = Assemble(exampleLines, mnemonicToOpcodeMap, lmc.InstructionSet, operandAddressModeMap);

            lmc.LoadProgram(lmcProgram);
            lmc.Execute();

            Console.WriteLine("Goodbye World!");
        }
    }
}