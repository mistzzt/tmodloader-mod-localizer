using System.IO;
using ModLocalizer.ModLoader;
using Newtonsoft.Json;

namespace ModLocalizer
{
	internal sealed class Patcher
	{
		private readonly TmodFile _mod;
		private readonly string _contentPath;
		private readonly byte[] _assembly;
		private readonly byte[] _monoAssembly;

		public Patcher(TmodFile mod, string contentPath)
		{
			_mod = mod;
			_contentPath = contentPath;

			_assembly = _mod.GetMainAssembly(false);
			_monoAssembly = _mod.GetMainAssembly(true);
		}

		public void Run()
		{
			ApplyBuildProperties();
			ApplyTmodProperties();

			Save();
		}

		private void ApplyBuildProperties()
		{
			if (!File.Exists(GetPath("Info.json")))
			{
				return;
			}

			var info = JsonConvert.DeserializeObject<BuildProperties>(File.ReadAllText(GetPath("Info.json")));
			var data = info.ToBytes();

			_mod.AddFile("Info", data);
		}

		private void ApplyTmodProperties()
		{
			if (!File.Exists(GetPath("ModInfo.json")))
			{
				return;
			}

			var prop = JsonConvert.DeserializeObject<TmodProperties>(File.ReadAllText(GetPath("ModInfo.json")));
			_mod.Properties = prop;
		}

		private void Save()
		{
			_mod.Save(_mod.Name + "_patched.tmod");
		}

		private string GetPath(params string[] paths) => Path.Combine(_contentPath, Path.Combine(paths));
	}
}
