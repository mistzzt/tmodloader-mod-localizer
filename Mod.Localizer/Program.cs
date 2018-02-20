using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Mod.Localizer
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            var wrapper = new TmodFileWrapper(Assembly.Load("Terraria, Version=1.3.5.1, Culture=neutral, PublicKeyToken=null"));

            var mod = wrapper.LoadFile("Test.tmod");

            Console.WriteLine(mod.Name);
            Console.WriteLine(mod.Version);
            Console.WriteLine(mod.HasFile("Windows.dll"));
            Console.WriteLine(mod.HasFile("Info"));

            var files = mod.Files;
            foreach (var filesKey in files.Values)
            {
                Console.WriteLine(filesKey.Length);
            }

            Console.Read();
        }
    }
}
