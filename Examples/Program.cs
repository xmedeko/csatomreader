using CsAtomReader;
using System;
using System.IO;
using System.Net.Http;

namespace Experiment
{
    internal class Program
    {
        /// <summary>
        /// Shared static client.
        /// </summary>
        private static readonly HttpClient client = new HttpClient();

        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                PrintUsage();
                return;
            }
            string fileName = args[0];
            string getName = null;
            if (fileName == "-t")
            {
                getName = AtomReader.TitleTypeName;
                if (args.Length < 2)
                {
                    PrintUsage();
                    return;
                }
                fileName = args[1];
            }

            if (fileName.StartsWith("http"))
            {
                if (getName == null)
                    HttpPrintAll(fileName);
                else
                    HttpGet(fileName, getName);
            }
            else
            {
                if (getName == null)
                    FilePrintAll(fileName);
                else
                    FileGet(fileName, getName);
            }
            Console.WriteLine("Press any key to continue ...");
            Console.ReadKey();
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Args: [-t] mp4file|uri");
        }

        private static void HttpGet(string fileName, object titleTypeName)
        {
            throw new NotImplementedException();
        }

        private static void FilePrintAll(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open))
                AtomPrintAll(stream);
        }

        private static void HttpPrintAll(string url)
        {
            using (PartialHttpStream stream = new PartialHttpStream(url))
            {
                AtomPrintAll(stream);
                Console.WriteLine($"#http requests: {stream.HttpRequestsCount}");
            }
        }

        private static void AtomPrintAll(Stream stream)
        {
            var mp4Reader = new AtomReader(stream);
            foreach (AtomEvent atom in mp4Reader.ParseAtoms())
            {
                string data = null;
                if (atom.Name == AtomReader.TitleTypeName || atom.Name == AtomReader.SynopsisTypeName)
                    data = mp4Reader.GetCurrentAtomStringData();
                Console.WriteLine($"{atom.Name} ({atom.Flags}) {atom.Size} {data}");
            }
        }

        private static void FileGet(string fileName, string atomTypeName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            {
                var mp4Reader = new AtomReader(stream);
                string value = mp4Reader.GetMetaAtomValue(atomTypeName);
                Console.WriteLine($"{atomTypeName}: {value}");
            }
        }

        private static void HttpGet(string url, string atomTypeName)
        {
            using (PartialHttpStream stream = new PartialHttpStream(url))
            {
                var mp4Reader = new AtomReader(stream);
                string value = mp4Reader.GetMetaAtomValue(atomTypeName);
                Console.WriteLine($"{atomTypeName}: {value}");
                Console.WriteLine($"#http requests: {stream.HttpRequestsCount}");
            }
        }
    }
}