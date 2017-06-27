using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ModLocalizer.ModLoader
{
	[JsonObject(MemberSerialization.Fields)]
	internal sealed class BuildProperties
	{
		/// <summary>A ModSide enum defines how mods are synced between clients and servers. You can set your mod's ModSide from its build.txt file.</summary>
		public enum ModSide
		{
			/// <summary>The default value for ModSide. This means that the mod has effects both client-side and server-side. When a client connects to a server, this mod will be disabled if the server does not have it. If a client without this mod connects to a server with this mod, the server will send this mod to the client and enable it. In general all mods that add game content should use this.</summary>
			Both,
			/// <summary>This means that the mod only has effects client-side. This mod will not be disabled client-side if the server does not have it. This is useful for mods that only add controls (for example, hotkeys), change textures/musics, etc.</summary>
			Client,
			/// <summary>This means that the mod only has effects server-side. The server will not send this mod to every client that connects.</summary>
			Server,
			/// <summary>This means that the mod could have effects client-side and could have effects server-side. The client will not download this mod if it connects to a server with this mod, and the client will not disable this mod if it connects to a server without this mod. If a client connects to a server and both have this mod, then IDs will still be synchronized. This is useful if you want optional extra features when both the client and server have this mod.</summary>
			NoSync
		}

		[JsonObject(MemberSerialization.Fields)]
		public struct ModReference
		{
			public string Mod;
			public Version Target;

			public ModReference(string mod, Version target)
			{
				Mod = mod;
				Target = target;
			}

			public override string ToString() => Target == null ? Mod : string.Concat(Mod, "@", Target.ToString());

			public static ModReference Parse(string spec)
			{
				var split = spec.Split('@');
				if (split.Length == 1)
					return new ModReference(split[0], null);

				if (split.Length > 2)
					throw new Exception("Invalid mod reference: " + spec);

				try
				{
					return new ModReference(split[0], new Version(split[1]));
				}
				catch
				{
					throw new Exception("Invalid mod reference: " + spec);
				}
			}
		}

		internal string[] DllReferences = new string[0];
		internal ModReference[] ModReferences = new ModReference[0];
		internal ModReference[] WeakReferences = new ModReference[0];
		//this mod will load after any mods in this list
		//sortAfter includes (mod|weak)References that are not in sortBefore
		internal string[] SortAfter = new string[0];
		//this mod will load before any mods in this list
		internal string[] SortBefore = new string[0];
		internal string[] BuildIgnores = new string[0];
		internal string Author = "";
		internal string Version = new Version(1, 0).ToString();
		internal string DisplayName = "";
		internal bool NoCompile;
		internal bool HideCode;
		internal bool HideResources;
		internal bool IncludeSource;
		internal bool IncludePdb;
		internal bool EditAndContinue;
		internal int LanguageVersion = 4;
		internal string Homepage = "";
		internal string Description = "";
		internal ModSide Side;

		public IEnumerable<ModReference> Refs(bool includeWeak) =>
			includeWeak ? ModReferences.Concat(WeakReferences) : ModReferences;

		public IEnumerable<string> RefNames(bool includeWeak) => Refs(includeWeak).Select(dep => dep.Mod);

		private static IEnumerable<string> ReadList(BinaryReader reader)
		{
			var list = new List<string>();
			for (var item = reader.ReadString(); item.Length > 0; item = reader.ReadString())
				list.Add(item);

			return list;
		}

		private static void WriteList<T>(IEnumerable<T> list, BinaryWriter writer)
		{
			foreach (var item in list)
				writer.Write(item.ToString());

			writer.Write(string.Empty);
		}

		internal byte[] ToBytes()
		{
			byte[] data;
			using (var memoryStream = new MemoryStream())
			{
				using (var writer = new BinaryWriter(memoryStream))
				{
					if (DllReferences.Length > 0)
					{
						writer.Write("dllReferences");
						WriteList(DllReferences, writer);
					}
					if (ModReferences.Length > 0)
					{
						writer.Write("modReferences");
						WriteList(ModReferences, writer);
					}
					if (WeakReferences.Length > 0)
					{
						writer.Write("weakReferences");
						WriteList(WeakReferences, writer);
					}
					if (SortAfter.Length > 0)
					{
						writer.Write("sortAfter");
						WriteList(SortAfter, writer);
					}
					if (SortBefore.Length > 0)
					{
						writer.Write("sortBefore");
						WriteList(SortBefore, writer);
					}
					if (Author.Length > 0)
					{
						writer.Write("author");
						writer.Write(Author);
					}
					writer.Write("version");
					writer.Write(Version);
					if (DisplayName.Length > 0)
					{
						writer.Write("displayName");
						writer.Write(DisplayName);
					}
					if (Homepage.Length > 0)
					{
						writer.Write("homepage");
						writer.Write(Homepage);
					}
					if (Description.Length > 0)
					{
						writer.Write("description");
						writer.Write(Description);
					}
					if (NoCompile)
					{
						writer.Write("noCompile");
					}
					if (!HideCode)
					{
						writer.Write("!hideCode");
					}
					if (!HideResources)
					{
						writer.Write("!hideResources");
					}
					if (IncludeSource)
					{
						writer.Write("includeSource");
					}
					if (IncludePdb)
					{
						writer.Write("includePDB");
					}
					if (EditAndContinue)
					{
						writer.Write("editAndContinue");
					}
					if (Side != ModSide.Both)
					{
						writer.Write("side");
						writer.Write((byte)Side);
					}
					writer.Write("");
				}
				data = memoryStream.ToArray();
			}
			return data;
		}

		internal bool IgnoreFile(string resource)
		{
			return BuildIgnores.Any(fileMask => FitsMask(resource, fileMask));
		}

		internal static BuildProperties ReadBytes(byte[] data)
		{
			var properties = new BuildProperties();

			if (data.Length == 0)
				return properties;

			using (var reader = new BinaryReader(new MemoryStream(data)))
			{
				for (var tag = reader.ReadString(); tag.Length > 0; tag = reader.ReadString())
				{
					if (tag == "dllReferences")
					{
						properties.DllReferences = ReadList(reader).ToArray();
					}
					if (tag == "modReferences")
					{
						properties.ModReferences = ReadList(reader).Select(ModReference.Parse).ToArray();
					}
					if (tag == "weakReferences")
					{
						properties.WeakReferences = ReadList(reader).Select(ModReference.Parse).ToArray();
					}
					if (tag == "sortAfter")
					{
						properties.SortAfter = ReadList(reader).ToArray();
					}
					if (tag == "sortBefore")
					{
						properties.SortBefore = ReadList(reader).ToArray();
					}
					if (tag == "author")
					{
						properties.Author = reader.ReadString();
					}
					if (tag == "version")
					{
						properties.Version = reader.ReadString();
					}
					if (tag == "displayName")
					{
						properties.DisplayName = reader.ReadString();
					}
					if (tag == "homepage")
					{
						properties.Homepage = reader.ReadString();
					}
					if (tag == "description")
					{
						properties.Description = reader.ReadString();
					}
					if (tag == "noCompile")
					{
						properties.NoCompile = true;
					}
					if (tag == "!hideCode")
					{
						properties.HideCode = false;
					}
					if (tag == "!hideResources")
					{
						properties.HideResources = false;
					}
					if (tag == "includeSource")
					{
						properties.IncludeSource = true;
					}
					if (tag == "includePDB")
					{
						properties.IncludePdb = true;
					}
					if (tag == "editAndContinue")
					{
						properties.EditAndContinue = true;
					}
					if (tag == "side")
					{
						properties.Side = (ModSide)reader.ReadByte();
					}
				}
			}
			return properties;
		}

		private static bool FitsMask(string fileName, string fileMask)
		{
			var pattern =
				'^' +
				Regex.Escape(fileMask.Replace(".", "__DOT__")
						.Replace("*", "__STAR__")
						.Replace("?", "__QM__"))
					.Replace("__DOT__", "[.]")
					.Replace("__STAR__", ".*")
					.Replace("__QM__", ".")
				+ '$';
			return new Regex(pattern, RegexOptions.IgnoreCase).IsMatch(fileName);
		}
	}
}