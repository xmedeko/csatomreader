using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsAtomReader
{
    /// <summary>
    /// Simple MP4 Atom metadata reader. Code trucated to reading only wanted items.
    /// <para>
    /// Rewritten from https://github.com/ActiveState/code/tree/master/recipes/Python/496984_Iterate_over_MP4_atoms/ http://code.activestate.com/recipes/496984-iterate-over-mp4-atoms/
    /// See ffmpeg source libavformat/movenc.c mov_write_ilst_tag() for the list of mp4 supported tags.
    /// ffmpeg does not support custom tags, see https://trac.ffmpeg.org/ticket/4209
    /// </para>
    /// </summary>
    public partial class AtomReader
    {
        public const string MoovTypeName = "moov";
        public const string UdtaTypeName = "udta";
        public const string MetaTypeName = "meta";
        public const string IlstTypeName = "ilst";

        public const string TitleTypeName = "\xa9nam";
        public const string SynopsisTypeName = "ldes";

        // Latin 1, any other ASCII 8 bit encoding would do the job, too
        private static readonly Encoding ASCII8Encoding = Encoding.GetEncoding(28591);

        private static readonly List<AtomType> TypeList = new List<AtomType>
        {
            new AtomType("ftyp", AtomTypeFlags.None),
            new AtomType(MoovTypeName, AtomTypeFlags.Container),
            new AtomType("mdat", AtomTypeFlags.None),
            new AtomType(UdtaTypeName, AtomTypeFlags.Container),
            new AtomType(MetaTypeName, AtomTypeFlags.Container | AtomTypeFlags.Skipper),
            new AtomType(IlstTypeName, AtomTypeFlags.Container),
            new AtomType("trak", AtomTypeFlags.Container),
            new AtomType("mdia", AtomTypeFlags.Container),
            new AtomType("minf", AtomTypeFlags.Container),
            new AtomType("wide", AtomTypeFlags.Tagitem),
            new AtomType(TitleTypeName, AtomTypeFlags.Tagitem), // title
            new AtomType(SynopsisTypeName, AtomTypeFlags.Tagitem), // Synopsis
        };

        /// <summary>
        /// Known atom types by atom type.
        /// </summary>
        private static readonly Dictionary<string, AtomType> Types = TypeList.ToDictionary(a => a.Name);

        private readonly Stream stream;
        private byte[] buff8 = new byte[8];

        public AtomReader(Stream stream)
        {
            this.stream = stream;
        }

        public AtomEvent CurrentAtom { get; private set; }

        /// <summary>
        /// Parse all atoms.
        /// </summary>
        public IEnumerable<AtomEvent> ParseAtoms()
        {
            return ParseAtoms(-1);
        }

        /// <summary>
        /// Get current atom data as string.
        /// </summary>
        public string GetCurrentAtomStringData()
        {
            CheckCurrentAtom();
            if (CurrentAtom.Flags.HasFlag(AtomTypeFlags.Container | AtomTypeFlags.ContainerEnd))
                throw new InvalidOperationException("Cannot get data for container");

            Skip(16);
            long dataLen = CurrentAtom.DataSize - 16;
            string data = ReadStr((int)dataLen);
            return data;
        }

        /// <summary>
        /// Ship current atom parsing.
        /// </summary>
        public void SkipCurrentAtom()
        {
            CheckCurrentAtom();
            if (CurrentAtom.Flags.HasFlag(AtomTypeFlags.ContainerEnd))
                return; // no skip fake atom
            long dataLen = CurrentAtom.DataSize;
            Skip(dataLen);
        }

        /// <summary>
        /// Get simple meta atom value or null if not found.
        /// </summary>
        public string GetMetaAtomValue(string atomTypeName)
        {
            var conts = new HashSet<string> {
                MoovTypeName,
                UdtaTypeName,
                MetaTypeName,
                IlstTypeName,
            };

            foreach (AtomEvent atom in ParseAtoms())
            {
                if (atom.Name == atomTypeName)
                {
                    // found
                    string data = GetCurrentAtomStringData();
                    return data;
                }
                if (atom.Flags.HasFlag(AtomTypeFlags.Container))
                {
                    if (!conts.Contains(atom.Name))
                        SkipCurrentAtom();
                }
                else if (atom.Flags.HasFlag(AtomTypeFlags.ContainerEnd))
                {
                    if (conts.Contains(atom.Name))
                        break; // not found, early quit
                }
            }
            return null; // not found
        }

        private void CheckCurrentAtom()
        {
            if (CurrentAtom == null)
                throw new NullReferenceException("No current atom.");
            if (CurrentAtom.Consumed)
                throw new InvalidOperationException("Current atom already consumed.");
            CurrentAtom.Consume();
        }

        /// <summary>
        /// Parse atoms to the given size. -1 means until the stream end.
        /// </summary>
        private IEnumerable<AtomEvent> ParseAtoms(long size)
        {
            long offset = 0;
            while (size < 0 || offset < size)
            {
                AtomEvent atom = CurrentAtom = ReadAtom();
                if (atom == null)
                    yield break; // end of stream
                yield return atom;

                offset += atom.Size;
                if (atom.Consumed)
                    continue;

                if (atom.Flags.HasFlag(AtomTypeFlags.Container))
                {
                    // default is to dive into container
                    long containerSize = atom.DataSize;
                    if (atom.Flags.HasFlag(AtomTypeFlags.Skipper))
                    {
                        containerSize -= 4;
                        Skip(4);
                    }

                    // recurse to read childern atoms
                    foreach (var atom2 in ParseAtoms(containerSize))
                        yield return atom2;

                    // yield atom end
                    var atomEnd = new AtomEvent(atom.Name, AtomTypeFlags.ContainerEnd, atom.Size, atom.DataSize);
                    atom.Consume();
                    yield return atomEnd;
                }
                else
                {
                    // default is to skip other atom types
                    SkipCurrentAtom();
                }
            }
        }

        /// <summary>
        /// Read atom header, add known flags.
        /// </summary>
        private AtomEvent ReadAtom()
        {
            // get size
            uint sizeShort;
            if (!TryReadUint(out sizeShort))
                return null; // end of stream
            long size = sizeShort;
            long dataSize = size - 8; // header length
            // get type
            string name = ReadAsciiStr(4);
            if (string.IsNullOrEmpty(name))
                return null;
            // check for wide atom
            if (sizeShort == 0)
                // Should not be hard to support it but dont need it yet.
                throw new InvalidOperationException("Open atom not supported.");
            if (sizeShort == 1)
            {
                // wide atom (should be "mdat")
                ulong sizeLong;
                if (!TryReadUlong(out sizeLong))
                    return null; // end of stream
                size = (long)sizeLong; // better to have size ulong, bot other methods expect long
                dataSize = size - 16;
            }

            // get known flags
            AtomTypeFlags flags = AtomTypeFlags.None;
            AtomType type = Types.GetValueOrDefault(name);
            if (type != null)
                flags = type.Flags;

            return new AtomEvent(name, flags, size, dataSize);
        }

        /// <summary>
        /// Read big endiand uint32. Returns false if cannot read.
        /// </summary>
        private bool TryReadUint(out uint value)
        {
            int len = stream.Read(buff8, 0, 4);
            value = 0;
            if (len < 4)
                return false; // end of stream
            Array.Reverse(buff8, 0, 4); // big endian
            value = BitConverter.ToUInt32(buff8);
            return true;
        }

        /// <summary>
        /// Read big endiand uint64. Returns false if cannot read.
        /// </summary>
        private bool TryReadUlong(out ulong value)
        {
            int len = stream.Read(buff8);
            value = 0;
            if (len < 8)
                return false; // end of stream
            Array.Reverse(buff8); // big endian
            value = BitConverter.ToUInt64(buff8);
            return true;
        }

        /// <summary>
        /// Read string of given size.
        /// </summary>
        private string ReadStr(int size)
        {
            var buff = new byte[size];
            int len = stream.Read(buff);
            return Encoding.UTF8.GetString(buff, 0, len);
        }

        /// <summary>
        /// Read string of given size.
        /// </summary>
        private string ReadAsciiStr(int size)
        {
            var buff = new byte[size];
            int len = stream.Read(buff);
            return ASCII8Encoding.GetString(buff, 0, len);
        }

        private void Skip(long size)
        {
            stream.Seek(size, SeekOrigin.Current);
        }

        /// <summary>
        /// Registered atom type info.
        /// </summary>
        private class AtomType
        {
            public AtomType(string name, AtomTypeFlags flags)
            {
                Name = name;
                Flags = flags;
            }

            public string Name { get; }
            public AtomTypeFlags Flags { get; }
        }
    }
}