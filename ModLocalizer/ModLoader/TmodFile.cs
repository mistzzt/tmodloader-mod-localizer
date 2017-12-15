using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ionic.Zlib;

namespace ModLocalizer.ModLoader
{
    internal sealed class TmodFile : IEnumerable<KeyValuePair<string, byte[]>>
    {
        private const string AllPlatformAssemblyFileName = "All.dll";

        private const string WindowsPlatformAssemblyFileName = "Windows.dll";

        private const string MonoPlatformAssemblyFileName = "Mono.dll";

        public const string InfoFileName = "Info";

        public const char PathSeparator = '/';

        private const string MagicHeader = "TMOD";

        private readonly string _path;

        private readonly IDictionary<string, byte[]> _files = new Dictionary<string, byte[]>();

        private byte[] _signature = new byte[256];

        private Version _modLoaderVersion;

        private byte[] _hash;

        public string Name { get; private set; }

        public Version Version
        {
            get; private set;
        }

        internal TmodFile(string path)
        {
            _path = path;
        }

        public bool HasFile(string fileName) => _files.ContainsKey(fileName.Replace('\\', '/'));

        public byte[] GetFile(string fileName)
        {
            _files.TryGetValue(fileName.Replace('\\', '/'), out byte[] data);
            return data;
        }

        internal void AddFile(string fileName, byte[] data)
        {
            var dataCopy = new byte[data.Length];
            data.CopyTo(dataCopy, 0);
            _files[fileName.Replace('\\', '/')] = dataCopy;
        }

        internal void RemoveFile(string fileName)
        {
            _files.Remove(fileName.Replace('\\', '/'));
        }

        public IEnumerator<KeyValuePair<string, byte[]>> GetEnumerator() => _files.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int FileCount => _files.Count;

        public void Save(string path = null, bool overrideModLoaderVersion = false)
        {
            using (var dataStream = new MemoryStream())
            {
                using (var writerStream = new DeflateStream(dataStream, CompressionMode.Compress))
                using (var writer = new BinaryWriter(writerStream))
                {
                    writer.Write(Name);
                    writer.Write(Version.ToString());

                    writer.Write(_files.Count);
                    foreach (var entry in _files)
                    {
                        writer.Write(entry.Key);
                        writer.Write(entry.Value.Length);
                        writer.Write(entry.Value);
                    }
                }
                var data = dataStream.ToArray();
                _hash = SHA1.Create().ComputeHash(data);

                using (var fileStream = File.Create(path ?? _path))
                using (var fileWriter = new BinaryWriter(fileStream))
                {
                    fileWriter.Write(Encoding.ASCII.GetBytes(MagicHeader));
                    fileWriter.Write(overrideModLoaderVersion ? DefaultConfigurations.ModLoaderVersion.ToString() : _modLoaderVersion.ToString());
                    fileWriter.Write(_hash);
                    fileWriter.Write(_signature);
                    fileWriter.Write(data.Length);
                    fileWriter.Write(data);
                }
            }
        }

        internal void Read()
        {
            byte[] data;
            using (var fileStream = File.OpenRead(_path))
            {
                using (var reader = new BinaryReader(fileStream))
                {
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != MagicHeader)
                        throw new Exception($"Magic Header != \"{MagicHeader}\"");

                    _modLoaderVersion = new Version(reader.ReadString());
                    _hash = reader.ReadBytes(20);
                    _signature = reader.ReadBytes(256);
                    data = reader.ReadBytes(reader.ReadInt32());
                    var verifyHash = SHA1.Create().ComputeHash(data);
                    if (!verifyHash.SequenceEqual(_hash))
                        throw new Exception("Hash mismatch, data blob has been modified or corrupted");
                }
            }

            using (var memoryStream = new MemoryStream(data))
            {
                using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                {
                    using (var reader = new BinaryReader(deflateStream))
                    {
                        Name = reader.ReadString();
                        Version = new Version(reader.ReadString());

                        var count = reader.ReadInt32();
                        for (var i = 0; i < count; i++)
                            AddFile(reader.ReadString(), reader.ReadBytes(reader.ReadInt32()));
                    }
                }
            }

            if (!HasFile(InfoFileName))
                throw new Exception($"Missing {InfoFileName} file");

            if (!HasFile(AllPlatformAssemblyFileName) && !(HasFile(WindowsPlatformAssemblyFileName) && HasFile(MonoPlatformAssemblyFileName)))
                throw new Exception($"Missing {AllPlatformAssemblyFileName} or {WindowsPlatformAssemblyFileName} and {MonoPlatformAssemblyFileName}");
        }

        public byte[] GetPrimaryAssembly(bool monoOnly)
        {
            byte[] data;
            if (monoOnly)
            {
                data = GetFile(MonoPlatformAssemblyFileName);
            }
            else
            {
                data = HasFile(AllPlatformAssemblyFileName) ? GetFile(AllPlatformAssemblyFileName) : GetFile(WindowsPlatformAssemblyFileName);
            }

            return data;
        }

        public void WritePrimaryAssembly(byte[] data, bool monoOnly)
        {
            var dataCopy = new byte[data.Length];
            data.CopyTo(dataCopy, 0);

            var fileName = monoOnly ? MonoPlatformAssemblyFileName :
                (HasFile(AllPlatformAssemblyFileName) ? AllPlatformAssemblyFileName : WindowsPlatformAssemblyFileName);
            _files[fileName] = dataCopy;
        }

        public void AddResourceFile(string path, byte[] data)
        {
            var dataCopy = new byte[data.Length];
            data.CopyTo(dataCopy, 0);

            path = Path.Combine(ResourceFolderPrefix, path);
            _files[path.Replace('\\', PathSeparator)] = dataCopy;
        }

        public IEnumerable<string> GetResourceFiles()
        {
            return _files.Keys.Where(x => x.StartsWith(ResourceFolderPrefix));
        }

        public TmodProperties Properties
        {
            get => new TmodProperties
            {
                Name = Name,
                ModLoaderVersion = _modLoaderVersion.ToString(),
                ModVersion = Version.ToString()
            };

            set
            {
                Name = value.Name ?? Name;
                Version = Version.Parse(value.ModVersion);
                _modLoaderVersion = Version.Parse(value.ModLoaderVersion);
            }
        }

        public readonly string ResourceFolderPrefix = typeof(Program).Namespace ?? throw new InvalidOperationException();
    }
}