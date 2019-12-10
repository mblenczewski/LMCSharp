using System;
using System.Collections.Concurrent;
using System.IO;

namespace LmcLib
{
    /// <summary>
    /// Represents a method that decodes a given encoded instruction into an opcode, and also outputs the address modes of
    /// the operands that are associated with the given instruction.
    /// </summary>
    /// <param name="encodedInstruction">The encoded instruction.</param>
    /// <param name="operandModes">The address modes of the operands associated with the instruction.</param>
    /// <returns>The instruction opcode.</returns>
    public delegate byte InstructionDecoder(in ushort encodedInstruction, out AddressModes[] operandModes);

    /// <summary>
    /// Represents a method that implements an instruction.
    /// </summary>
    /// <param name="globalMemory">The global memory available to the instruction.</param>
    /// <param name="registers">The registers that store the program state.</param>
    /// <param name="operands">The operands that were passed to the instruction.</param>
    /// <param name="input">Allows the instruction to get external user input.</param>
    /// <param name="output">Allows the instruction to send output to the user.</param>
    /// <returns>What the computer should do after the instruction finishes execution.</returns>
    public delegate InstructionResult InstructionDelegate(in ArraySegment<byte> globalMemory,
        in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
        in TextReader input, in TextWriter output);

    /// <summary>
    /// Represents a computer capable of sequentially executing instructions.
    /// </summary>
    public class Computer
    {
        /// <summary>
        /// The underlying architecture.
        /// </summary>
        private readonly ArchitectureWidth architecture;

        /// <summary>
        /// Holds the instruction set, indexed by opcode.
        /// </summary>
        private readonly ConcurrentDictionary<byte, Instruction> instructionSet;

        /// <summary>
        /// The global memory for the computer.
        /// </summary>
        private readonly byte[] memory;

        /// <summary>
        /// Holds the registers in the processor, indexed by their names.
        /// </summary>
        private readonly ConcurrentDictionary<string, MemoryRegister> registers;

        /// <summary>
        /// The input reader for getting external input.
        /// </summary>
        private TextReader inputReader;

        /// <summary>
        /// The output writer for writing external output.
        /// </summary>
        private TextWriter outputWriter;

        /// <summary>
        /// The byte mask to use to get the opcode from an encoded opcode.
        /// </summary>
        public static readonly ushort OpcodeMask = 0b00000000_11111111;

        /// <summary>
        /// Holds the byte masks used to get the addressing modes of the operands to an instruction.
        /// </summary>
        public static readonly ushort[] OperandAddressModeMasks =
        {
            0b11000000_00000000,
            0b00110000_00000000,
            0b00001100_00000000,
            0b00000011_00000000
        };

        /// <summary>
        /// Initialises a new instance of the <see cref="Computer"/> class.
        /// </summary>
        /// <param name="memorySize">The amount of bytes to allocate as memory.</param>
        /// <param name="architectureWidth">The width of the computer architecture, determining the size of registers.</param>
        public Computer(uint memorySize = ushort.MaxValue, ArchitectureWidth architectureWidth = ArchitectureWidth.Default)
        {
            architecture = architectureWidth;

            memory = new byte[memorySize];
            registers = new ConcurrentDictionary<string, MemoryRegister>();
            instructionSet = new ConcurrentDictionary<byte, Instruction>();

            inputReader = TextReader.Null;
            outputWriter = TextWriter.Null;
        }

        /// <summary>
        /// Decodes the given instruction into the opcode key and the address modes of the instruction operands.
        /// </summary>
        /// <param name="encodedInstruction">The encoded instruction to execute.</param>
        /// <param name="operandAddressModes">The addressing modes for the instruction operands.</param>
        /// <returns>The instruction opcode key.</returns>
        public static byte DecodeInstruction(in ushort encodedInstruction, out AddressModes[] operandAddressModes)
        {
            ushort opcode = (ushort)(encodedInstruction & OpcodeMask);

            operandAddressModes = new AddressModes[OperandAddressModeMasks.Length];
            for (int i = 0; i < operandAddressModes.Length; i++)
            {
                ushort maskedOperandAddressMode = (ushort)(encodedInstruction & OperandAddressModeMasks[i]);
                ushort operandAddressMode = (ushort)(maskedOperandAddressMode >> (8 + 2 * i));

                operandAddressModes[i] = operandAddressMode switch
                {
                    0 => AddressModes.Immediate,
                    1 => AddressModes.Direct,
                    2 => AddressModes.Indirect,
                    3 => AddressModes.Relative,
                    _ => AddressModes.Immediate,
                };
            }

            return (byte)opcode;
        }

        /// <summary>
        /// Unwraps the given operand into a value, according to its address mode.
        /// </summary>
        /// <param name="globalMemory">The global memory available to the instruction.</param>
        /// <param name="operand">The operand whose value to unwrap.</param>
        /// <param name="valueWidth">The number of bytes to slice from the value.</param>
        /// <returns>The array segment containing the desired value.</returns>
        public static ArraySegment<byte> UnwrapOperand(in ArraySegment<byte> globalMemory, InstructionOperand operand, int valueWidth)
        {
            int startDirectRange = BitConverter.ToInt32(operand.Value);
            int endDirectRange = startDirectRange + valueWidth;

            switch (operand.AddressingMode)
            {
                case AddressModes.Direct:
                    return globalMemory[startDirectRange..endDirectRange][..valueWidth];

                case AddressModes.Indirect:
                    ArraySegment<byte> indirectAddressBytes = globalMemory[startDirectRange..endDirectRange];

                    int startIndirectRange = BitConverter.ToInt32(indirectAddressBytes);
                    int endIndirectRange = startIndirectRange + valueWidth;

                    return globalMemory[startIndirectRange..endIndirectRange][..valueWidth];

                case AddressModes.Immediate:
                    return operand.Value[..valueWidth];

                case AddressModes.Relative:
                    throw new NotSupportedException("Relative address mode is not currently supported by the computer.");

                default:
                    throw new ArgumentOutOfRangeException(nameof(operand.AddressingMode), operand.AddressingMode,
                        "Given operand addressing mode was not within the range of valid values.");
            }
        }

        /// <summary>
        /// Executes the program currently stored in memory, starting at the given memory address.
        /// </summary>
        /// <param name="startingProgramCounter">The memory address at which program execution will start.</param>
        /// <param name="instructionDecoder">The instruction decoder to use.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when an instruction returns an invalid <see cref="InstructionResultType"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when an unknown opcode is encountered within the program.
        /// </exception>
        public void Execute(int startingProgramCounter = 0, InstructionDecoder? instructionDecoder = null)
        {
            startingProgramCounter = startingProgramCounter >= 0 ? startingProgramCounter : 0;
            instructionDecoder ??= DecodeInstruction;

            int programCounter = startingProgramCounter;
            bool shouldExecute = true;

            while (shouldExecute)
            {
                ArraySegment<byte> nextInstructionBytes = new ArraySegment<byte>(memory, programCounter, sizeof(ushort));
                ushort nextInstruction = BitConverter.ToUInt16(nextInstructionBytes);

                byte opcode = instructionDecoder(in nextInstruction, out AddressModes[] operandAddressModes);

                if (instructionSet.TryGetValue(opcode, out Instruction instruction))
                {
                    ArraySegment<byte> operandBytes = new ArraySegment<byte>(memory,
                        programCounter + sizeof(ushort),
                        instruction.OperandCount * InstructionOperand.OperandSize);

                    InstructionOperand[] operands = new InstructionOperand[instruction.OperandCount];
                    for (int i = 0; i < instruction.OperandCount; i++)
                    {
                        operands[i] = new InstructionOperand(operandAddressModes[i],
                            operandBytes.Slice(InstructionOperand.OperandSize * i, InstructionOperand.OperandSize));
                    }

                    InstructionResult instructionResult = instruction.Func(memory, registers, operands, inputReader, outputWriter);

                    switch (instructionResult.Result)
                    {
                        case InstructionResultType.Halt:
                            shouldExecute = false;
                            break;

                        case InstructionResultType.Continue:
                            if (instruction.OperandCount <= 0) { continue; }

                            programCounter += sizeof(ushort) + instruction.OperandCount * InstructionOperand.OperandSize;
                            break;

                        case InstructionResultType.Jump:
                            if (instruction.OperandCount <= 0) { continue; }

                            programCounter = instructionResult.JumpAddress;
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(instructionResult.Result), instructionResult.Result,
                                "The given instruction result was outside of the range of valid values.");
                    }
                }
                else
                {
                    throw new ArgumentException($"Unknown opcode \'{opcode}\' at memory address {programCounter}");
                }
            }
        }

        /// <summary>
        /// Loads the given program into memory, at the given offset.
        /// </summary>
        /// <param name="programContents">The program serialised to binary.</param>
        /// <param name="offset">The offset into memory where the program should be stored at.</param>
        public void LoadProgram(in ReadOnlyMemory<byte> programContents, int offset = 0)
        {
            offset = offset >= 0 ? offset : 0;

            Memory<byte> globalMemory = new Memory<byte>(memory, offset, memory.Length - offset);

            programContents.CopyTo(globalMemory);
        }

        /// <summary>
        /// Sets the external input reader for this computer.
        /// </summary>
        /// <param name="readerImplementation">The reader implementation to use.</param>
        public void SetInput(in TextReader readerImplementation) => inputReader = readerImplementation;

        /// <summary>
        /// Sets the external output writer for this computer.
        /// </summary>
        /// <param name="writerImplementation">The writer implementation to use.</param>
        public void SetOutput(in TextWriter writerImplementation) => outputWriter = writerImplementation;

        /// <summary>
        /// Attempts to deregister the instruction with the given opcode.
        /// </summary>
        /// <param name="opcode">The opcode that corresponds to the instruction to deregister.</param>
        /// <param name="oldInstruction">The old instruction implementation.</param>
        /// <returns>Whether the instruction was successfully deregistered.</returns>
        public bool TryDeregisterInstruction(byte opcode, out Instruction oldInstruction) =>
            instructionSet.TryRemove(opcode, out oldInstruction);

        /// <summary>
        /// Attempts to register the given instruction, using its opcode as the key.
        /// </summary>
        /// <param name="instruction">The instruction implementation.</param>
        /// <returns>Whether the instruction was successfully registered.</returns>
        public bool TryRegisterInstruction(in Instruction instruction) =>
                                            instructionSet.TryAdd(instruction.Opcode, instruction);

        /// <summary>
        /// Attempts to update the instruction implementation for the instruction with the given opcode key.
        /// </summary>
        /// <param name="opcode">The opcode that corresponds to the instruction to update.</param>
        /// <param name="newInstruction">The new instruction implementation.</param>
        /// <param name="oldInstruction">The old instruction implementation.</param>
        /// <returns>Whether the instruction was successfully updated.</returns>
        public bool TryUpdateInstruction(byte opcode, in Instruction newInstruction, out Instruction oldInstruction)
        {
            return TryDeregisterInstruction(opcode, out oldInstruction) &&
                   TryRegisterInstruction(newInstruction);
        }
    }
}