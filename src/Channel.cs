using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MrDaktilo.Performance;

namespace RemoteDaktilo
{
	internal sealed class Channel : IDisposable
	{
		private static byte[] Random(int n)
		{
			byte[] array = new byte[n];
			using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
			{
				randomNumberGenerator.GetBytes(array);
			}
			return array;
		}

		public static string Key()
		{
			return BitConverter.ToString(Channel.Random(16)).Replace("-", "");
		}

		private static byte[] T(string s)
		{
			return Encoding.UTF8.GetBytes(s);
		}

		private static byte[] Join(params byte[][] xs)
		{
			byte[] array2;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				foreach (byte[] array in xs)
				{
					memoryStream.Write(array, 0, array.Length);
				}
				array2 = memoryStream.ToArray();
			}
			return array2;
		}

		private static byte[] H(byte[] k, byte[] b)
		{
			byte[] array;
			using (HMACSHA256 hmacsha = new HMACSHA256(k))
			{
				array = hmacsha.ComputeHash(b);
			}
			return array;
		}

		private static bool Eq(byte[] a, byte[] b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}
			int num = 0;
			for (int i = 0; i < a.Length; i++)
			{
				num |= (int)(a[i] ^ b[i]);
			}
			return num == 0;
		}

		private static byte[] Read(Stream s, int n)
		{
			byte[] array = new byte[n];
			int num;
			for (int i = 0; i < n; i += num)
			{
				num = s.Read(array, i, n - i);
				if (num == 0)
				{
					throw new IOException("Bağlantı kapandı.");
				}
			}
			return array;
		}

		public Channel(TcpClient c, string key, bool host)
			: this(c, c.GetStream(), key, host)
		{
		}

		public Channel(TcpClient c, Stream transport, string key, bool host)
		{
			this.tcp = c;
			this.stream = transport;
			this.stream.ReadTimeout = 30000;
			this.stream.WriteTimeout = 15000;
			c.NoDelay = true;
			key = key.Replace(" ", "").Replace("-", "");
			if (key.Length != 32)
			{
				throw new IOException("Anahtar 32 onaltılık karakter olmalı.");
			}
			byte[] array = new byte[16];
			for (int i = 0; i < 16; i++)
			{
				array[i] = Convert.ToByte(key.Substring(i * 2, 2), 16);
			}
			byte[] array2 = Channel.Random(32);
			this.stream.Write(array2, 0, 32);
			byte[] array3 = Channel.Read(this.stream, 32);
			byte[] array4 = (host ? Channel.Join(new byte[][] { array2, array3 }) : Channel.Join(new byte[][] { array3, array2 }));
			byte[] array5 = Channel.H(array, Channel.Join(new byte[][]
			{
				Channel.T("RemoteDaktilo-v1"),
				array4
			}));
			this.enc = Channel.H(array5, Channel.T("encryption"));
			this.mac = Channel.H(array5, Channel.T("authentication"));
			this.tx = (host ? "host" : "viewer");
			this.rx = (host ? "viewer" : "host");
			byte[] array6 = Channel.H(array5, Channel.Join(new byte[][]
			{
				Channel.T(this.tx),
				array4
			}));
			this.stream.Write(array6, 0, 32);
			if (!Channel.Eq(Channel.Read(this.stream, 32), Channel.H(array5, Channel.Join(new byte[][]
			{
				Channel.T(this.rx),
				array4
			}))))
			{
				throw new IOException("Anahtar eşleşmiyor.");
			}
			Array.Clear(array, 0, 16);
			Array.Clear(array5, 0, array5.Length);
			Transport.Bootstrap(this);
		}

		private byte[] Seal(byte[] p, long n, string role)
		{
			byte[] array = Channel.Random(16);
			byte[] array2;
			using (Aes aes = Aes.Create())
			{
				aes.Key = this.enc;
				aes.IV = array;
				using (ICryptoTransform cryptoTransform = aes.CreateEncryptor())
				{
					array2 = cryptoTransform.TransformFinalBlock(p, 0, p.Length);
				}
			}
			byte[] array3 = Channel.Join(new byte[][]
			{
				BitConverter.GetBytes(n),
				array,
				array2
			});
			return Channel.Join(new byte[][]
			{
				array3,
				Channel.H(this.mac, Channel.Join(new byte[][]
				{
					Channel.T(role),
					array3
				}))
			});
		}

		private byte[] Open(byte[] f, long n, string role)
		{
			if (f.Length < 72)
			{
				throw new IOException("Geçersiz paket.");
			}
			byte[] array = new byte[f.Length - 32];
			byte[] array2 = new byte[32];
			Buffer.BlockCopy(f, 0, array, 0, array.Length);
			Buffer.BlockCopy(f, array.Length, array2, 0, 32);
			if (!Channel.Eq(array2, Channel.H(this.mac, Channel.Join(new byte[][]
			{
				Channel.T(role),
				array
			}))) || BitConverter.ToInt64(array, 0) != n)
			{
				throw new IOException("Paket doğrulaması başarısız.");
			}
			byte[] array3 = new byte[16];
			Buffer.BlockCopy(array, 8, array3, 0, 16);
			byte[] array4;
			using (Aes aes = Aes.Create())
			{
				aes.Key = this.enc;
				aes.IV = array3;
				using (ICryptoTransform cryptoTransform = aes.CreateDecryptor())
				{
					array4 = cryptoTransform.TransformFinalBlock(array, 24, array.Length - 24);
				}
			}
			return array4;
		}

		public void Send(byte[] b)
		{
			Transport.Send(this, b);
		}

		public byte[] Receive()
		{
			int num = BitConverter.ToInt32(Channel.Read(this.stream, 4), 0);
			if (num < 72 || num > 8388608)
			{
				throw new IOException("Paket boyutu sınır dışında.");
			}
			byte[] array = Channel.Read(this.stream, num);
			long num2;
			this.received = (num2 = this.received) + 1L;
			return this.Open(array, num2, this.rx);
		}

		public void Dispose()
		{
			Transport.Close(this);
			this.stream.Dispose();
			this.tcp.Close();
			Array.Clear(this.enc, 0, this.enc.Length);
			Array.Clear(this.mac, 0, this.mac.Length);
		}

		public static void Test()
		{
			TcpListener l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			int port = ((IPEndPoint)l.LocalEndpoint).Port;
			string k = Channel.Key();
			Exception error = null;
			Thread thread = new Thread(delegate()
			{
				try
				{
					using (Channel channel2 = new Channel(l.AcceptTcpClient(), k, true))
					{
						if (Encoding.UTF8.GetString(channel2.Receive()) != "hello")
						{
							throw new Exception();
						}
						channel2.Send(Channel.T("ok"));
						byte[] array = channel2.Seal(Channel.T("test"), 0L, "test");
						byte[] array2 = array;
						int num = 25;
						array2[num] ^= 1;
						bool flag = false;
						try
						{
							channel2.Open(array, 0L, "test");
						}
						catch (IOException)
						{
							flag = true;
						}
						if (!flag)
						{
							throw new Exception();
						}
						array = channel2.Seal(Channel.T("test"), 0L, "test");
						flag = false;
						try
						{
							channel2.Open(array, 1L, "test");
						}
						catch (IOException)
						{
							flag = true;
						}
						if (!flag)
						{
							throw new Exception();
						}
					}
				}
				catch (Exception ex)
				{
					error = ex;
				}
			});
			thread.IsBackground = true;
			thread.Start();
			TcpClient tcpClient = new TcpClient();
			Transport.ConfigureTcp(tcpClient);
			using (TcpClient tcpClient2 = tcpClient)
			{
				tcpClient2.Connect(IPAddress.Loopback, port);
				using (Channel channel = new Channel(tcpClient2, k, false))
				{
					channel.Send(Channel.T("hello"));
					if (Encoding.UTF8.GetString(channel.Receive()) != "ok")
					{
						throw new Exception();
					}
				}
			}
			if (!thread.Join(10000))
			{
				throw new Exception();
			}
			l.Stop();
			if (error != null)
			{
				throw error;
			}
		}

		private void SendRaw(byte[] b)
		{
			lock (this.stream)
			{
				long num;
				this.sent = (num = this.sent) + 1L;
				byte[] array = this.Seal(b, num, this.tx);
				byte[] bytes = BitConverter.GetBytes(array.Length);
				this.stream.Write(bytes, 0, 4);
				this.stream.Write(array, 0, array.Length);
			}
		}

		private TcpClient tcp;

		private Stream stream;

		private byte[] enc;

		private byte[] mac;

		private string tx;

		private string rx;

		private long sent;

		private long received;
	}
}

