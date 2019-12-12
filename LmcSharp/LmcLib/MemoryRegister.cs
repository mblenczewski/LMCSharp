using System;

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
                ArchitectureWidth.Default => new byte[8],
                _ => new byte[8],
            };

            Name = name;
        }

        /// <summary>
        /// Returns the contents of the register, converted to a <see cref="long"/>.
        /// </summary>
        /// <returns>The current contents of the register.</returns>
        public long GetContents()
        {
            return BitConverter.ToInt64(Contents);
        }

        /// <summary>
        /// Sets the contents of the register.
        /// </summary>
        /// <param name="value">The value to set the register to.</param>
        public void SetContents(long value)
        {
            BitConverter.GetBytes(value).CopyTo(Contents, 0);
        }
    }
}