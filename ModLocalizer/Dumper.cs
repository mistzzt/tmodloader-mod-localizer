using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModLocalizer.ModLoader;
using Newtonsoft.Json;

namespace ModLocalizer
{
	internal sealed class Dumper
	{
		private readonly TmodFile _mod;

		public Dumper(TmodFile mod)
		{
			_mod = mod;
		}

		public void Run()
		{
			Directory.CreateDirectory(_mod.Name);
			Directory.CreateDirectory(GetPath("Items"));
			Directory.CreateDirectory(GetPath("NPCs"));

			DumpBuildProperties();
			DumpTmodProperties();
		}

		private void DumpBuildProperties()
		{
			var infoData = _mod.GetFile("Info");

			var properties = BuildProperties.ReadBytes(infoData);

			using (var fs = new FileStream(GetPath("Info.json"), FileMode.Create))
			{
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(JsonConvert.SerializeObject(properties, Formatting.Indented));
				}
			}
		}

		private void DumpTmodProperties()
		{
			var properties = _mod.Properties;

			using (var fs = new FileStream(GetPath("ModInfo.json"), FileMode.Create))
			{
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(JsonConvert.SerializeObject(properties, Formatting.Indented));
				}
			}
		}

		private string GetPath(params string[] paths) => Path.Combine(_mod.Name, Path.Combine(paths));
	}
}
