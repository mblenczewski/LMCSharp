using System;
using System.Collections.Concurrent;
using System.IO;

namespace LmcLib
{
    /// <summary>
    /// Represents an instruction capable of being executed by the LMC.
    /// </summary>
    public readonly struct Instruction
    {
        /// <summary>
        /// Represents a halt instruction.
        /// </summary>
        public static readonly Instruction Halt = new Instruction(0, 0, "HLT",
            (in ArraySegment<byte> memory, in ConcurrentDictionary<string, MemoryRegister> registers,
                in InstructionOperand[] operands, in TextReader input, in TextWriter output) => InstructionResult.Halt);

        /// <summary>
        /// The delegate method that implements the instruction.
        /// </summary>
        public readonly InstructionDelegate Func;

        /// <summary>
        /// The mnemonic that represents this instruction.
        /// </summary>
        public readonly string Mnemonic;

        /// <summary>
        /// The opcode for the instruction.
        /// </summary>
        public readonly byte Opcode;

        /// <summary>
        /// The number of operands this instruction takes.
        /// </summary>
        public readonly byte OperandCount;

        /// <summary>
        /// Initialises a new instance of the <see cref="Instruction"/> struct.
        /// </summary>
        /// <param name="opcode"></param>
        /// <param name="operandCount"></param>
        /// <param name="mnemonic"></param>
        /// <param name="func"></param>
        public Instruction(byte opcode, byte operandCount, string mnemonic, InstructionDelegate func)
        {
            Opcode = opcode;
            OperandCount = operandCount;
            Mnemonic = mnemonic;
            Func = func;
        }
    }

    /// <summary>
    /// Represents an instruction operand.
    /// </summary>
    public readonly struct InstructionOperand
    {
        /// <summary>
        /// The size of the operand in bytes.
        /// </summary>
        public const byte OperandSize = 8;

        /// <summary>
        /// The addressing mode for this operand.
        /// </summary>
        public readonly AddressModes AddressingMode;

        /// <summary>
        /// The data stored in this operand.
        /// </summary>
        public readonly ArraySegment<byte> Value;

        /// <summary>
        /// Initialises a new instance of the <see cref="LmcLib.InstructionOperand"/> struct.
        /// </summary>
        /// <param name="addressingMode">The address mode for this operand.</param>
        /// <param name="value">The data stored in this operand.</param>
        public InstructionOperand(AddressModes addressingMode, ArraySegment<byte> value)
        {
            AddressingMode = addressingMode;

            Value = value;
        }
    }

    /// <summary>
    /// Represents the outcome of an instruction.
    /// </summary>
    public readonly struct InstructionResult
    {
        /// <summary>
        /// Represents a continue instruction.
        /// </summary>
        public static readonly InstructionResult Continue = new InstructionResult(InstructionResultType.Continue, 0);

        /// <summary>
        /// Represents a halt instruction.
        /// </summary>
        public static readonly InstructionResult Halt = new InstructionResult(InstructionResultType.Halt, 0);

        /// <summary>
        /// The address to jump to.
        /// </summary>
        public readonly int JumpAddress;

        /// <summary>
        /// The result of the instruction.
        /// </summary>
        public readonly InstructionResultType Result;

        /// <summary>
        /// Initialises a new instance of the <see cref="InstructionResult"/> struct.
        /// </summary>
        /// <param name="result">The result of the last instruction.</param>
        /// <param name="jumpAddress">The address to jump to, if the outcome is a jump instruction.</param>
        public InstructionResult(InstructionResultType result, int jumpAddress)
        {
            Result = result;

            JumpAddress = jumpAddress;
        }
    }
}