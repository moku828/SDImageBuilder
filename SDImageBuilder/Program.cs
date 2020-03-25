using System.IO;
using DiscUtils;
using DiscUtils.Partitions;
using DiscUtils.Fat;

namespace SDImageBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            SDImageBuilder builder = new SDImageBuilder();
            builder.InputRootDirectory = args[0];
            builder.Build();
            builder.OutputSDImageStream.CopyTo(new FileStream(args[1], FileMode.Create));
        }
    }

    public class SDImageBuilder
    {
        public string InputRootDirectory;
        public Stream OutputSDImageStream;

        public void Build()
        {
            OutputSDImageStream = new MemoryStream();
            long capacity = 8 * 1024 * 1024;
            Geometry geometry = new Geometry(capacity, 4, 32, 512);
            BiosPartitionedDiskBuilder builder = new BiosPartitionedDiskBuilder(capacity, geometry);
            builder.PartitionTable.Create(WellKnownPartitionType.WindowsFat, false);
            SparseStream partitionContent = SparseStream.FromStream(new MemoryStream((int)(builder.PartitionTable[0].SectorCount * 512)), Ownership.Dispose);
            FatFileSystem ffs = FatFileSystem.FormatPartition(partitionContent, "TEST", geometry, 0, 16384, 0);
            CopyDirectoriesAndFiles(ffs, InputRootDirectory);
            builder.SetPartitionContent(0, partitionContent);
            OutputSDImageStream = builder.Build();
        }

        private void CopyDirectoriesAndFiles(FatFileSystem ffs, string parentDirectory)
        {
            foreach (var file in Directory.GetFiles(parentDirectory))
            {
                SparseStream ss = ffs.OpenFile(file.Replace(InputRootDirectory, ""), FileMode.CreateNew);
                FileStream fs = new FileStream(file, FileMode.Open);
                byte[] bytes = new byte[fs.Length];
                fs.Read(bytes, 0, (int)fs.Length);
                ss.Write(bytes, 0, (int)fs.Length);
                ss.Flush();
                ss.Close();
                ffs.SetAttributes(file.Replace(InputRootDirectory, ""), FileAttributes.Normal);
            }
            foreach (var directory in Directory.GetDirectories(parentDirectory))
            {
                ffs.CreateDirectory(directory.Replace(InputRootDirectory, ""));
                CopyDirectoriesAndFiles(ffs, directory);
            }
        }
    }
}
