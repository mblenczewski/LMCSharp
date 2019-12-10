namespace LmcLib
{
    /// <summary>
    /// The addressing modes of an operand.
    /// </summary>
    public enum AddressModes
    {
        /// <summary>
        /// Direct addressing. The operand is at the given address in memory.
        /// </summary>
        Direct,

        /// <summary>
        /// Indirect addressing. The operand is at the location in memory given by the value at the given address in memory.
        /// </summary>
        Indirect,

        /// <summary>
        /// Immediate addressing. The operand is the value passed to the instruction.
        /// </summary>
        Immediate,

        /// <summary>
        /// Relative addressing. The operand is at the given offset to the current program counter.
        /// </summary>
        Relative,
    }
}