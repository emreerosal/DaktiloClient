using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using MrDaktilo.Files;
using MrDaktilo.Performance;

namespace RemoteDaktilo
{
	internal sealed class UsbBridge : IDisposable
	{
		public int Port { get; private set; }

		public event Action<string> Report;

public UsbBridge(Channel c,bool h,int port=3240){if(!h)throw new InvalidOperationException("Host-only Client build");channel=c;host=true;backendPort=port;}


		private void Say(string s)
		{
			Action<string> report = this.Report;
			if (report != null)
			{
				report(s);
			}
		}

		private void Send(byte kind, int id, byte[] body)
		{
			if (this.closed)
			{
				return;
			}
			body = body ?? new byte[0];
			byte[] array = new byte[5 + body.Length];
			array[0] = kind;
			Buffer.BlockCopy(BitConverter.GetBytes(id), 0, array, 1, 4);
			Buffer.BlockCopy(body, 0, array, 5, body.Length);
			this.channel.Send(array);
		}




		private void ReadLink(int id, TcpClient c)
		{
			new Thread(delegate()
			{
				try
				{
					NetworkStream stream = c.GetStream();
					byte[] array = new byte[32768];
					while (!this.closed)
					{
						int num = stream.Read(array, 0, array.Length);
						if (num == 0)
						{
							break;
						}
						byte[] array2 = new byte[num];
						Buffer.BlockCopy(array, 0, array2, 0, num);
						this.Send(41, id, array2);
					}
				}
				catch (Exception)
				{
				}
				finally
				{
					this.CloseLink(id);
					try
					{
						this.Send(42, id, null);
					}
					catch
					{
					}
				}
			})
			{
				IsBackground = true
			}.Start();
		}

		private void CloseLink(int id)
		{
			TcpClient tcpClient;
			if (this.links.TryRemove(id, out tcpClient))
			{
				tcpClient.Close();
			}
		}

		public bool Handle(byte[] b)
		{
			return Transport.Dispatch(this, b);
		}

		private static void Background(Action a)
		{
			new Thread(delegate()
			{
				try
				{
					a();
				}
				catch (Exception)
				{
				}
			})
			{
				IsBackground = true
			}.Start();
		}




		public static bool IsInstalled(bool server)
		{
			bool flag;
			try
			{
				flag = UsbBridge.Find(server) != null;
			}
			catch
			{
				flag = false;
			}
			return flag;
		}

private static string Find(bool server){if(!server)return null;string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"usbipd-win","usbipd.exe");return File.Exists(path)?path:null;}


		private static string Run(string exe, string args, bool elevate)
		{
			string text;
			try
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(exe, args);
				processStartInfo.UseShellExecute = elevate;
				processStartInfo.CreateNoWindow = !elevate;
				processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
				if (elevate)
				{
					processStartInfo.Verb = "runas";
				}
				else
				{
					processStartInfo.RedirectStandardOutput = true;
					processStartInfo.RedirectStandardError = true;
				}
				using (Process process = new Process())
				{
					process.StartInfo = processStartInfo;
					StringBuilder output = new StringBuilder();
					object gate = new object();
					if (!elevate)
					{
						DataReceivedEventHandler dataReceivedEventHandler = delegate(object s, DataReceivedEventArgs e)
						{
							if (e.Data != null)
							{
								lock (gate)
								{
									if (output.Length < 262144)
									{
										output.AppendLine(e.Data);
									}
								}
							}
						};
						process.OutputDataReceived += dataReceivedEventHandler;
						process.ErrorDataReceived += dataReceivedEventHandler;
					}
					process.Start();
					if (!elevate)
					{
						process.BeginOutputReadLine();
						process.BeginErrorReadLine();
					}
					if (!process.WaitForExit(30000))
					{
						try
						{
							process.Kill();
						}
						catch
						{
						}
						text = "USB işlemi 30 saniyede tamamlanmadı.";
					}
					else
					{
						process.WaitForExit();
						text = (elevate ? ((process.ExitCode == 0) ? "USB cihazı paylaşılabilir yapıldı. Destek uygulamasından cihaz bağlantısı kurulabilir." : ("Paylaşım etkinleştirilemedi; çıkış kodu: " + process.ExitCode)) : output.ToString());
					}
				}
			}
			catch (Exception ex)
			{
				text = ex.Message;
			}
			return text;
		}

		public static void ExportSources(IWin32Window owner)
		{
			try
			{
				using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
				{
					folderBrowserDialog.Description = "USB kaynak ve lisanslarının kaydedileceği klasörü seçin";
					if (folderBrowserDialog.ShowDialog(owner) == DialogResult.OK)
					{
						foreach (string text in Assembly.GetExecutingAssembly().GetManifestResourceNames())
						{
							if (text.StartsWith("Daktilo.License.") || text.StartsWith("Daktilo.Source."))
							{
								string text2 = text.Substring(text.IndexOf('.', 8) + 1);
								string text3 = Path.Combine(folderBrowserDialog.SelectedPath, text2);
								if (File.Exists(text3))
								{
									throw new IOException("Dosya zaten var: " + text3);
								}
								using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(text))
								{
									using (FileStream fileStream = new FileStream(text3, FileMode.CreateNew))
									{
										manifestResourceStream.CopyTo(fileStream);
									}
								}
							}
						}
						MessageBox.Show(owner, "USB kaynak ve lisansları kaydedildi.");
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show(owner, ex.Message);
			}
		}

public static void Install(bool server,IWin32Window owner){if(!server)throw new InvalidOperationException("Host-only build");MrDaktilo.Startup.AutoUsb.Begin(owner as Form,true);}

public static void Test(){
 if(typeof(UsbBridge).GetMethod("Attach")!=null)throw new Exception("Outbound receiver leaked");
 var backend=new TcpListener(IPAddress.Loopback,0);backend.Start();int bp=((IPEndPoint)backend.LocalEndpoint).Port;
 var transport=new TcpListener(IPAddress.Loopback,0);transport.Start();int tp=((IPEndPoint)transport.LocalEndpoint).Port;
 string key=Channel.Key();Exception failure=null;byte[] payload=Encoding.ASCII.GetBytes("USB-HOST-TEST");
 var echo=new Thread(()=>{try{using(var peer=backend.AcceptTcpClient()){var data=new byte[payload.Length];int n=0;while(n<data.Length){int k=peer.GetStream().Read(data,n,data.Length-n);if(k==0)throw new IOException("Early backend close");n+=k;}peer.GetStream().Write(data,0,data.Length);}}catch(Exception e){failure=e;}});echo.IsBackground=true;echo.Start();
 var hostThread=new Thread(()=>{try{using(var wire=new Channel(transport.AcceptTcpClient(),key,true))using(var bridge=new UsbBridge(wire,true,bp)){while(true){byte[] packet=wire.Receive();bridge.Handle(packet);if(packet[0]==42)break;}}}catch(Exception e){failure=e;}});hostThread.IsBackground=true;hostThread.Start();
 using(var socket=new TcpClient()){socket.Connect(IPAddress.Loopback,tp);using(var peer=new Channel(socket,key,false)){
  peer.Send(new byte[]{40,1,0,0,0});var packet=new byte[payload.Length+5];packet[0]=41;packet[1]=1;Buffer.BlockCopy(payload,0,packet,5,payload.Length);peer.Send(packet);
  byte[] answer;do{answer=peer.Receive();}while(answer[0]==5);if(answer[0]!=41||Encoding.ASCII.GetString(answer,5,answer.Length-5)!=Encoding.ASCII.GetString(payload))throw new Exception("USB host roundtrip mismatch");peer.Send(new byte[]{42,1,0,0,0});}}
 if(!hostThread.Join(10000)||!echo.Join(10000))throw new Exception("USB test timed out");transport.Stop();backend.Stop();if(failure!=null)throw failure;
}


		public void Dispose()
		{
			Transport.CloseBridge(this);
			Transport.FilesDisconnectedAsync(this);
			if (this.closed)
			{
				return;
			}
			this.closed = true;
			try
			{
				if (this.listener != null)
				{
					this.listener.Stop();
				}
			}
			catch
			{
			}
			foreach (int num in this.links.Keys)
			{
				this.CloseLink(num);
			}
			if (!this.host)
			{
				
			}
		}

		private bool HandleRaw(byte[] b)
		{
			if (FileFeature.Intercept(this, b))
			{
				return true;
			}
			if (b.Length == 0 || b[0] < 40 || b[0] > 46)
			{
				return false;
			}
			if (b.Length < 5)
			{
				throw new IOException("Geçersiz USB paketi");
			}
			int num = BitConverter.ToInt32(b, 1);
			byte b2 = b[0];
			if (b2 == 40 && this.host)
			{
				if (this.links.Count >= 16)
				{
					this.Send(42, num, null);
					return true;
				}
				try
				{
					TcpClient tcpClient = new TcpClient();
					Transport.ConfigureTcp(tcpClient);
					TcpClient tcpClient2 = tcpClient;
					IAsyncResult asyncResult = tcpClient2.BeginConnect(IPAddress.Loopback, this.backendPort, null, null);
					if (!asyncResult.AsyncWaitHandle.WaitOne(5000))
					{
						tcpClient2.Close();
						throw new IOException("Yerel USB servisi yanıt vermiyor");
					}
					tcpClient2.EndConnect(asyncResult);
					asyncResult.AsyncWaitHandle.Close();
					tcpClient2.NoDelay = true;
					tcpClient2.GetStream().WriteTimeout = 15000;
					this.links[num] = tcpClient2;
					this.ReadLink(num, tcpClient2);
				}
				catch
				{
					this.Send(42, num, null);
				}
				return true;
			}
			else
			{
				if (b2 == 41)
				{
					TcpClient tcpClient3;
					if (this.links.TryGetValue(num, out tcpClient3))
					{
						try
						{
							tcpClient3.GetStream().Write(b, 5, b.Length - 5);
						}
						catch
						{
							this.CloseLink(num);
							this.Send(42, num, null);
						}
					}
					return true;
				}
				if (b2 == 42)
				{
					this.CloseLink(num);
					return true;
				}
				if (b2 == 43 && this.host)
				{
					UsbBridge.Background(delegate
					{
						string text = UsbBridge.Find(true);
						string text2 = ((text == null) ? "Client bilgisayarında USB paylaşım sürücüsü kurulu değil. Client penceresindeki kurulum düğmesini kullanın." : UsbBridge.Run(text, "list", false));
						this.Send(44, 0, Encoding.UTF8.GetBytes(text2));
					});
					return true;
				}
				if (b2 == 44 && !this.host)
				{
					this.Say(Encoding.UTF8.GetString(b, 5, b.Length - 5));
					return true;
				}
				if (b2 == 45 && this.host)
				{
					string bus = Encoding.UTF8.GetString(b, 5, b.Length - 5);
					if (!Regex.IsMatch(bus, "^\\d{1,4}-\\d{1,4}$"))
					{
						this.Send(46, 0, Encoding.UTF8.GetBytes("Geçersiz USB bus ID"));
						return true;
					}
					UsbBridge.Background(delegate
					{
						string text3 = UsbBridge.Find(true);
						string text4 = ((text3 == null) ? "USB paylaşım sürücüsü eksik." : UsbBridge.Run(text3, "bind --busid " + bus, true));
						this.Send(46, 0, Encoding.UTF8.GetBytes(text4));
					});
					return true;
				}
				else
				{
					if (b2 == 46 && !this.host)
					{
						this.Say(Encoding.UTF8.GetString(b, 5, b.Length - 5));
						return true;
					}
					return true;
				}
			}
		}

		private readonly Channel channel;

		private readonly bool host;

		private readonly int backendPort;

		private readonly ConcurrentDictionary<int, TcpClient> links = new ConcurrentDictionary<int, TcpClient>();

		private readonly List<int> attached = new List<int>();

		private TcpListener listener;

		private volatile bool closed;

		private int serial;
	}
}


