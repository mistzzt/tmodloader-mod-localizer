using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Mod.Localizer
{
    public sealed class TmodFileWrapper
    {
        public const string Namespace = "Terraria.ModLoader.IO";
        public const string TypeName = "TmodFile";

        private readonly TmodFileImplementation _implementation;

        public TmodFileWrapper(Assembly modLoaderAssembly)
        {
            var modFileType = modLoaderAssembly.GetType($"{Namespace}.{TypeName}") ?? throw new NotSupportedException();

            _implementation = new TmodFileImplementation(modFileType);
        }

        public ITmodFile LoadFile(string path)
        {
            var file = new TmodFile(path, _implementation);
            file.Read();

            return file;
        }

        private sealed class TmodFileImplementation
        {
            public const string GetName = "get_name";
            public const string SetName = "set_name";
            public const string GetVersion = "get_version";
            public const string SetVersion = "set_version";

            public const string Path = "path";
            public const string ReadException = "readException";
            public const string Files = "files";

            public const string HasFile = nameof(HasFile);
            public const string GetFile = nameof(GetFile);
            public const string AddFile = nameof(AddFile);
            public const string RemoveFile = nameof(RemoveFile);
            public const string Read = nameof(Read);
            public const string GetMainAssembly = nameof(GetMainAssembly);
            public const string GetMainPdb = "GetMainPDB";

            private readonly ConstructorInfo _ctor;

            private readonly Dictionary<string, FieldInfo> _fields;
            private readonly string[] _fieldsList;

            private readonly Dictionary<string, MethodInfo> _methods;
            private readonly string[] _methodsList;

            private readonly Type _modFileType;

            public TmodFileImplementation(Type modFileType)
            {
                _modFileType = modFileType ?? throw new ArgumentNullException(nameof(modFileType));

                _ctor = _modFileType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();

                _fields = new Dictionary<string, FieldInfo>();
                _fieldsList = new[]
                {
                    Path,
                    ReadException,
                    Files
                };
                LoadFields();

                _methods = new Dictionary<string, MethodInfo>();
                _methodsList = new[]
                {
                    HasFile,
                    GetFile,
                    AddFile,
                    RemoveFile,
                    Read,
                    GetMainAssembly,
                    GetMainPdb,

                    GetName,
                    SetName,

                    GetVersion,
                    SetVersion
                };
                LoadMethods();
            }

            public object CreateTmodFile(string path)
            {
                return _ctor.Invoke(new object[] { path });
            }

            private void LoadMethods()
            {
                foreach (var methodName in _methodsList)
                {
                    var method = _modFileType.GetMethod(methodName);
                    if (method == null)
                    {
                        method = _modFileType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                    }

                    _methods.Add(methodName, method);
                }
            }

            private void LoadFields()
            {
                foreach (var fieldName in _fieldsList)
                {
                    var field = _modFileType.GetField(fieldName);
                    if (field == null)
                    {
                        field = _modFileType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    }

                    _fields.Add(fieldName, field);
                }
            }

            public T GetFieldValue<T>(object instance, string field)
            {
                if (instance?.GetType() != _modFileType)
                {
                    throw new ArgumentOutOfRangeException(nameof(instance));
                }

                if (!_fields.ContainsKey(field))
                {
                    throw new ArgumentOutOfRangeException(nameof(field));
                }

                return (T)_fields[field].GetValue(instance);
            }

            public T InvokeMethod<T>(object instance, string method, params object[] parameters)
            {
                if (instance?.GetType() != _modFileType)
                {
                    throw new ArgumentOutOfRangeException(nameof(instance));
                }

                if (!_methods.ContainsKey(method))
                {
                    throw new ArgumentOutOfRangeException(method);
                }

                return (T)_methods[method].Invoke(instance, parameters);
            }

            [Obsolete("would be replaced on newer tModLoader possibly")]
            public object CreateHandler(MethodInfo method)
            {
                const string handlerTypeName = "ReadStreamingAsset";

                var type = _modFileType.GetNestedType(handlerTypeName, BindingFlags.NonPublic);
                return Delegate.CreateDelegate(type, method);
            }
        }

        private sealed class TmodFile : ITmodFile
        {
            private readonly TmodFileImplementation _impl;
            private readonly object _modFile;

            public TmodFile(string path, TmodFileImplementation impl)
            {
                _impl = impl;
                _modFile = impl.CreateTmodFile(path);
            }

            public string Name
            {
                get => _impl.InvokeMethod<string>(_modFile, TmodFileImplementation.GetName);
                set => _impl.InvokeMethod<object>(_modFile, TmodFileImplementation.SetName, value);
            }

            public string Path => _impl.GetFieldValue<string>(_modFile, TmodFileImplementation.Path);

            public Version Version
            {
                get => _impl.InvokeMethod<Version>(_modFile, TmodFileImplementation.GetVersion);
                set => _impl.InvokeMethod<object>(_modFile, TmodFileImplementation.SetVersion, value);
            }

            public void AddFile(string fileName, byte[] data)
            {
                _impl.InvokeMethod<object>(_modFile, TmodFileImplementation.AddFile, fileName, data);
            }

            public byte[] GetMainAssembly(bool? windows = null)
            {
                // Currently we cannot directly call the built-in mechanism as it will call .cctor
                // of ModLoader class, throwing an exception

                const string allPlatformName = "All.dll";

                return HasFile(allPlatformName) ? GetFile(allPlatformName) :
                    windows.GetValueOrDefault(true) ? GetFile("Windows.dll") : GetFile("Mono.dll");
            }

            public byte[] GetMainPdb(bool? windows = null)
            {
                const string allPlatformName = "All.pdb";

                return HasFile(allPlatformName) ? GetFile(allPlatformName) :
                    windows.GetValueOrDefault(true) ? GetFile("Windows.pdb") : GetFile("Mono.pdb");
            }

            public IDictionary<string, byte[]> Files =>
                _impl.GetFieldValue<IDictionary<string, byte[]>>(_modFile, TmodFileImplementation.Files);

            public bool HasFile(string fileName) =>
                _impl.InvokeMethod<bool>(_modFile, TmodFileImplementation.HasFile, fileName);

            public byte[] GetFile(string fileName) =>
                _impl.InvokeMethod<byte[]>(_modFile, TmodFileImplementation.GetFile, fileName);

            public void Read()
            {
#pragma warning disable CS0618 // Type or member is obsolete
                var handler = _impl.CreateHandler(GetType().GetMethod(nameof(ReadStreamingHandler)));
#pragma warning restore CS0618 // Type or member is obsolete

                // invoke TmodFile.Read() with state and handler
                // state will be random number greater than 4 (see TmodFile.cs#L19)
                _impl.InvokeMethod<object>(_modFile, TmodFileImplementation.Read, int.MaxValue, handler);

                var ex = _impl.GetFieldValue<Exception>(_modFile, TmodFileImplementation.ReadException);
                if (ex != null)
                {
                    throw ex;
                }
            }

            public void RemoveFile(string fileName)
            {
                _impl.InvokeMethod<object>(_modFile, TmodFileImplementation.RemoveFile, fileName);
            }

            public void Save()
            {
                throw new NotImplementedException();
            }

            public void Write(string path)
            {
                throw new NotImplementedException();
            }

            // ReSharper disable MemberCanBePrivate.Local
            // ReSharper disable UnusedParameter.Local

            /// <summary>
            /// Serves as the non-null 2nd argument for <code>TmodFile.Read()</code>
            /// </summary>
            public static void ReadStreamingHandler(string path, int len, BinaryReader reader) { }

            // ReSharper restore UnusedParameter.Local
            // ReSharper restore MemberCanBePrivate.Local
        }

        public interface ITmodFile
        {
            string Name { get; set; }

            string Path { get; }

            Version Version { get; set; }

            IDictionary<string, byte[]> Files { get; }

            bool HasFile(string fileName);

            byte[] GetFile(string fileName);

            void AddFile(string fileName, byte[] data);

            void RemoveFile(string fileName);

            void Save();

            void Write(string path);

            byte[] GetMainAssembly(bool? windows = null);

            byte[] GetMainPdb(bool? windows = null);
        }
    }
}
