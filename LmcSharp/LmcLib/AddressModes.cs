namespace LmcLib
{
    /// <summary>
    /// The addressing modes of an operand.
    /// </summary>
    public enum AddressModes : ushort
    {
        /// <summary>
        /// Direct addressing. The operand is at the given address in memory.
        /// </summary>
        Direct = 0,

        /// <summary>
        /// Indirect addressing. The operand is at the location in memory given by the value at the given address in memory.
        /// </summary>
        Indirect = 1,

        /// <summary>
        /// Immediate addressing. The operand is the value passed to the instruction.
        /// </summary>
        Immediate = 2,

        /// <summary>
        /// Relative addressing. The operand is at the given offset to the current program counter.
        /// </summary>
        Relative = 3,
    }
}