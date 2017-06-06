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

			DumpBuildProperties();
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

		private string GetPath(string fileName, string category = null)
		{
			return !string.IsNullOrWhiteSpace(category) ? Path.Combine(_mod.Name, category, fileName) : Path.Combine(_mod.Name, fileName);
		}
	}
}
