using System;
using System.IO;
using System.Windows.Forms;

namespace RemoteDaktilo
{
	internal static class Program
	{
		[STAThread]
		private static void Main(string[] args)
		{
			if (args.Length > 0)
			{
				if (!(args[0] == "--self-test"))
				{
					if (!(args[0] == "--usb-self-test" || args[0] == "--power-self-test"))
					{
						goto IL_0065;
					}
				}
				try
				{
					if (args[0] == "--usb-self-test" || args[0] == "--power-self-test")
					{
						if(args[0]=="--power-self-test")MrDaktilo.Startup.KeepAwake.Test();else UsbBridge.Test();
					}
					else
					{
						Channel.Test();
					}
					Environment.Exit(0);
				}
				catch (Exception ex)
				{
					if (args.Length > 1)
					{
						File.WriteAllText(args[1], ex.ToString());
					}
					Environment.Exit(1);
				}
				return;
			}
			IL_0065:
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (args.Length > 0 && args[0] == "--ui-self-test")
			{
				try
				{
					using (App app = new App(true))
					{
						app.VerifyUI();
					}
					Environment.Exit(0);
				}
				catch (Exception ex2)
				{
					if (args.Length > 1)
					{
						File.WriteAllText(args[1], ex2.ToString());
					}
					Environment.Exit(1);
				}
				return;
			}
			if (args.Length > 1 && args[0] == "--ui-snapshot")
			{
				using (App app2 = new App(true))
				{
					app2.SaveSnapshot(args[1], (args.Length > 2) ? args[2] : "default");
				}
				return;
			}
			using(var app=new App(false))MrDaktilo.Startup.KeepAwake.Run(app);
		}

		public const bool ClientMode = true;
	}
}

