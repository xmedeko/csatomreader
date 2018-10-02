namespace CsAtomReader
{
    /// <summary>
    /// Event of atom parsing.
    /// </summary>
    public class AtomEvent
    {
        internal AtomEvent(string name, AtomTypeFlags flags, long size, long dataSize)
        {
            Name = name;
            Flags = flags;
            Size = size;
            DataSize = dataSize;
        }

        public string Name { get; }
        public AtomTypeFlags Flags { get; }

        /// <summary>
        /// Whole atom size.
        /// </summary>
        public long Size { get; }

        /// <summary>
        /// Atom data size only.
        /// </summary>
        public long DataSize { get; }

        public bool Consumed { get; private set; }

        internal void Consume()
            => Consumed = true;
    }
}