using System;

namespace CsAtomReader
{
    /// <summary>
    /// Flags how to parse atoms.
    /// </summary>
    [Flags]
    public enum AtomTypeFlags
    {
        None = 0,
        /// <summary>
        /// Contains other atoms.
        /// </summary>
        Container = 1,
        /// <summary>
        /// Ignore first 4 bytes.
        /// </summary>
        Skipper = 2,
        /// <summary>
        /// A real "tag" with data.
        /// </summary>
        Tagitem = 4,
        /// <summary>
        /// Datum is 8 bytes (2 4-bytes BE integers).
        /// </summary>
        Novern = 8,
        /// <summary>
        /// Datum is a triplet (I believe) of "mean", "name", "data" items.
        /// </summary>
        Xtagitem = 16,
        /// <summary>
        /// Fake type for event that container ends.
        /// </summary>
        ContainerEnd = 32,
    }
}