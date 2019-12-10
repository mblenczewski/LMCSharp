namespace LmcLib
{
    /// <summary>
    /// Describes the outcome of an instruction.
    /// </summary>
    public enum InstructionResultType
    {
        /// <summary>
        /// The computer should halt after the current instruction finishes execution.
        /// </summary>
        Halt,

        /// <summary>
        /// The computer should increment the program counter after the current instruction finishes execution.
        /// </summary>
        Continue,

        /// <summary>
        /// The computer should jump to a specified address.
        /// </summary>
        Jump,
    }
}