using PhoneRemote.Core;
using PhoneRemote.Interop.Windows;
using System;
using System.Linq;

namespace PhoneRemote.ConsoleTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var server = new PhoneRemoteServer();

			while (true)
			{
				Console.Write("x,y=");
				string input = Console.ReadLine();

				var vals = input.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Take(2).ToList();

				if (vals.Count == 2 && int.TryParse(vals[0], out int x) && int.TryParse(vals[1], out int y))
				{
					CursorUtils.MoveCursor(x, y);
				}

				Console.Clear();
			}
		}
	}
}
