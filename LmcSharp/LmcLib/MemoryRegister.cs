namespace LmcLib
{
    /// <summary>
    /// Describes a processor register.
    /// </summary>
    public struct MemoryRegister
    {
        /// <summary>
        /// Represents a null memory register.
        /// </summary>
        public static readonly MemoryRegister NullRegister = new MemoryRegister(ArchitectureWidth.Default, "NULL");

        /// <summary>
        /// The contents of the register.
        /// </summary>
        public readonly byte[] Contents;

        /// <summary>
        /// The name of the register.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Initialises a new instance of the <see cref="MemoryRegister"/> struct.
        /// </summary>
        /// <param name="width">The width of the register.</param>
        /// <param name="name">The name of the register.</param>
        public MemoryRegister(ArchitectureWidth width, string name)
        {
            Contents = width switch
            {
                ArchitectureWidth.Default => new byte[4],
                _ => new byte[4],
            };

            Name = name;
        }
    }
}