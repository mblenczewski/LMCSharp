using System;
using System.Collections.Concurrent;
using System.IO;
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

        private static InstructionResult LmcAdd(in ArraySegment<byte> globalMemory,
                    in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc addition!");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcBra(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc unconditional jump!");

            return new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value));
        }

        private static InstructionResult LmcBrp(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc conditional jump (x >= 0)!");

            return new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value));
        }

        private static InstructionResult LmcBrz(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc conditional jump (x == 0)!");

            return new InstructionResult(InstructionResultType.Jump, BitConverter.ToInt32(operands[0].Value));
        }

        private static InstructionResult LmcHlt(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc halt!");

            return InstructionResult.Halt;
        }

        private static InstructionResult LmcInp(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc input!");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcLda(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc load!");

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
            debugOutput.WriteLine("Lmc ASCII character output!");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcOut(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc value output!");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcSta(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc store!");

            return InstructionResult.Continue;
        }

        private static InstructionResult LmcSub(in ArraySegment<byte> globalMemory,
            in ConcurrentDictionary<string, MemoryRegister> registers, in InstructionOperand[] operands,
            in TextReader input, in TextWriter output)
        {
            debugOutput.WriteLine("Lmc subtraction!");

            return InstructionResult.Continue;
        }

        private static void Main()
        {
            Console.WriteLine("Hello World!");

            Computer lmc = new Computer();

            lmc.TryRegisterInstruction(new Instruction(0, 0, "HLT", LmcHlt));
            lmc.TryRegisterInstruction(new Instruction(1, 1, "ADD", LmcAdd));
            lmc.TryRegisterInstruction(new Instruction(2, 1, "SUB", LmcSub));
            lmc.TryRegisterInstruction(new Instruction(3, 1, "STA", LmcSta));
            lmc.TryRegisterInstruction(new Instruction(4, 1, "NUL", LmcNul));
            lmc.TryRegisterInstruction(new Instruction(5, 1, "LDA", LmcLda));
            lmc.TryRegisterInstruction(new Instruction(6, 1, "BRA", LmcBra));
            lmc.TryRegisterInstruction(new Instruction(7, 1, "BRZ", LmcBrz));
            lmc.TryRegisterInstruction(new Instruction(8, 1, "BRP", LmcBrp));
            lmc.TryRegisterInstruction(new Instruction(9, 1, "INP", LmcInp));
            lmc.TryRegisterInstruction(new Instruction(10, 1, "OUT", LmcOut));
            lmc.TryRegisterInstruction(new Instruction(11, 1, "OTC", LmcOtc));

            lmc.SetInput(Console.In);
            lmc.SetOutput(Console.Out);

            byte[] lmcProgram =
            {
                9, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                3, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                10, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0
            };

            lmc.LoadProgram(lmcProgram);
            lmc.Execute();

            Console.WriteLine("Goodbye World!");
        }
    }
}