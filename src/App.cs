using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using MrDaktilo.Files;
using MrDaktilo.Performance;
using MrDaktilo.Startup;

namespace RemoteDaktilo
{
	internal sealed class App : Form
	{
		public App(bool preview = false)
		{
			this.previewMode = preview;
			this.BuildUI();
		}

		private void UI(Action a)
		{
			try
			{
				if (!base.IsDisposed && base.IsHandleCreated)
				{
					base.BeginInvoke(a);
				}
			}
			catch (InvalidOperationException)
			{
			}
		}

		private void State(string s)
		{
			this.UI(delegate
			{
				this.SetStatusText(s);
			});
		}

private void Start(bool sharing) {
 if(!sharing)throw new InvalidOperationException("Client only supports hosting");
 if(active)return;active=true;UI(()=>SetPhase("connecting"));int id=++generation;
 host.Enabled=false;join.Enabled=false;ip.Enabled=false;stop.Enabled=true;
 new Thread(()=>{try{
  var tcp=new TcpClient();Transport.ConfigureTcp(tcp);pending=tcp;
  var connected=tcp.BeginConnect("158.220.98.93",45892,null,null);
  try{if(!connected.AsyncWaitHandle.WaitOne(10000))throw new IOException("Eşleştirme sunucusuna erişilemiyor.");tcp.EndConnect(connected);}finally{connected.AsyncWaitHandle.Close();}
  var tls=new SslStream(tcp.GetStream(),false,(sender,cert,chain,errors)=>{
   if(cert==null)return false;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(cert.GetRawCertData())).Replace("-","")=="FE1C4DDF01235F36F5ED5B4CD1E164C50219D2D741566A424FF0C71B078AC8A4";});
  tls.ReadTimeout=30000;tls.WriteTimeout=15000;tls.AuthenticateAsClient("RemoteDaktilo",null,SslProtocols.Tls12,false);
  RelayWrite(tls,"{\"op\":\"host\"}");string own=RelayValue(RelayRead(tls),"code");
  if(!Regex.IsMatch(own,"^[0-9]{9}$"))throw new IOException("Geçersiz bağlantı kodu");
  UI(()=>key.Text=own.Substring(0,3)+" "+own.Substring(3,3)+" "+own.Substring(6,3));State("Kodunuzu karşı tarafa verin. Ekranınız henüz paylaşılmıyor.");
  tls.ReadTimeout=-1;string secret=RelayValue(RelayRead(tls),"pair");tls.ReadTimeout=30000;
  if(!active||generation!=id){tls.Dispose();return;}
  var wire=new Channel(tcp,tls,secret,true);channel=wire;var bridge=new UsbBridge(wire,true,3240);usb=bridge;
  UI(()=>ClientSessionReady());wire.Send(new byte[]{2});State("BAĞLANTI AKTİF — Karşı taraf ekranınızı görebiliyor. Durdur ile kapatın.");
  new Thread(()=>CaptureScreen(wire,id)){IsBackground=true}.Start();
  while(active&&generation==id){var data=wire.Receive();if(!bridge.Handle(data))lock(inputLock){if(active&&generation==id&&control)Input(data);}}
 }catch(Exception error){if(active&&generation==id)State(error.Message);}
 finally{UI(()=>{if(generation==id)Stop(false);});}}){IsBackground=true}.Start();
}


		private static void RelayWrite(Stream s, string text)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
			s.Write(bytes, 0, bytes.Length);
		}

		private static string RelayRead(Stream s)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				for (int i = 0; i < 1024; i++)
				{
					int num = s.ReadByte();
					if (num < 0)
					{
						throw new IOException("Sunucu bağlantısı kapandı.");
					}
					if (num == 10)
					{
						return Encoding.UTF8.GetString(memoryStream.ToArray());
					}
					memoryStream.WriteByte((byte)num);
				}
				throw new IOException("Geçersiz sunucu yanıtı.");
			}
			
		}

		private static string RelayValue(string text, string name)
		{
			Match match = Regex.Match(text, "\"" + name + "\"\\s*:\\s*\"([A-Za-z0-9]+)\"");
			if (!match.Success)
			{
				throw new IOException(text.Contains("rate_limit") ? "Çok fazla deneme. Bir dakika bekleyin." : (text.Contains("code_unavailable") ? "Kod bulunamadı veya kullanımda." : "Sunucu eşleştirmesi başarısız."));
			}
			return match.Groups[1].Value;
		}

		private void CaptureScreen(Channel w, int id)
		{
			AdaptiveCapture.Run(this, w, id);
		}

		private void Stop(bool message)
		{
			this.active = false;
			this.UI(delegate
			{
				this.SessionEnded(message);
			});
			try
			{
				if (this.usb != null)
				{
					this.usb.Dispose();
				}
			}
			catch
			{
			}
			this.usb = null;
			lock (this.inputLock)
			{
				foreach (byte b in this.pressed)
				{
					App.keybd_event(b, 0, 2U, UIntPtr.Zero);
				}
				this.pressed.Clear();
				App.mouse_event(20U, 0U, 0U, 0U, UIntPtr.Zero);
			}
			this.generation++;
			try
			{
				if (this.channel != null)
				{
					this.channel.Dispose();
				}
			}
			catch
			{
			}
			try
			{
				if (this.pending != null)
				{
					this.pending.Close();
				}
			}
			catch
			{
			}
			this.channel = null;
			this.pending = null;
			this.host.Enabled = (this.join.Enabled = (this.ip.Enabled = (this.key.Enabled = true)));
			this.stop.Enabled = false;
			this.key.ReadOnly = true;
			this.key.Text = "— — —";
			if (message)
			{
				this.SetStatusText("Oturum kapalı. Yeni kod için uygulamayı yeniden açın.");
			}
			foreach (Form form in base.OwnedForms)
			{
				form.Close();
			}
		}

		[DllImport("user32.dll")]
		private static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);

		[DllImport("user32.dll")]
		private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

		private void Input(byte[] b)
		{
			if (b.Length != 10 || b[0] != 3)
			{
				if (b.Length == 3 && b[0] == 4)
				{
					if (b[2] == 0)
					{
						this.pressed.Add(b[1]);
					}
					else
					{
						this.pressed.Remove(b[1]);
					}
					App.keybd_event(b[1], 0, (b[2] == 0) ? 0U : 2U, UIntPtr.Zero);
					this.lastRemoteInput = DateTime.UtcNow;
				}
				return;
			}
			float num = BitConverter.ToSingle(b, 2);
			float num2 = BitConverter.ToSingle(b, 6);
			if (float.IsNaN(num) || float.IsNaN(num2) || num < 0f || num > 1f || num2 < 0f || num2 > 1f)
			{
				return;
			}
			Rectangle bounds = Screen.PrimaryScreen.Bounds;
			App.SetCursorPos(bounds.Left + (int)(num * (float)(bounds.Width - 1)), bounds.Top + (int)(num2 * (float)(bounds.Height - 1)));
			uint num3 = ((b[1] == 1) ? 2U : ((b[1] == 2) ? 4U : ((b[1] == 3) ? 8U : ((b[1] == 4) ? 16U : 0U))));
			if (num3 != 0U)
			{
				App.mouse_event(num3, 0U, 0U, 0U, UIntPtr.Zero);
			}
			this.lastRemoteInput = DateTime.UtcNow;
		}

		private Label MakeLabel(Control parent, string text, float size, Color color)
		{
			Label label = Brand.Label(text, size, color);
			parent.Controls.Add(label);
			return label;
		}

		private ActionButton MakeButton(Control parent, string text, EventHandler click)
		{
			ActionButton actionButton = new ActionButton
			{
				Text = text
			};
			parent.Controls.Add(actionButton);
			if (click != null)
			{
				actionButton.Click += click;
			}
			return actionButton;
		}

		private void BuildUI()
		{
			Brand.StyleForm(this);
			this.Text = "DaktiloClient • MR. Daktilo";
			base.StartPosition = FormStartPosition.CenterScreen;
			base.ClientSize = new Size(1020, 740);
			this.MinimumSize = new Size(840, 710);
			this.shell = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Brand.Bg
			};
			base.Controls.Add(this.shell);
			this.footer = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 36,
				BackColor = Color.FromArgb(23, 26, 33)
			};
			base.Controls.Add(this.footer);
			this.footerText = this.MakeLabel(this.footer, "MR. Daktilo • Uzak destek", 9f, Brand.Sub);
			this.reducedMotion = new CheckBox
			{
				Text = "Hareketi azalt",
				AutoSize = false,
				ForeColor = Brand.Sub,
				BackColor = this.footer.BackColor,
				Font = Brand.Font(9f, false),
				Checked = !SystemInformation.IsMenuAnimationEnabled
			};
			this.footer.Controls.Add(this.reducedMotion);
			this.footer.Resize += delegate(object s, EventArgs e)
			{
				this.footerText.SetBounds(18, 8, Math.Max(100, this.footer.Width - 210), 22);
				this.reducedMotion.SetBounds(Math.Max(0, this.footer.Width - 160), 6, 145, 24);
			};
			this.brandPanel = new Panel
			{
				BackColor = Color.FromArgb(18, 19, 23)
			};
			this.shell.Controls.Add(this.brandPanel);
			this.logo = new PictureBox
			{
				Image = Brand.Logo(),
				SizeMode = PictureBoxSizeMode.Zoom,
				AccessibleName = "MR. Daktilo — orijinal logo"
			};
			this.brandPanel.Controls.Add(this.logo);
			this.workPanel = new Panel
			{
				BackColor = Brand.Bg
			};
			this.shell.Controls.Add(this.workPanel);
			this.BuildClient();
			this.shell.Resize += delegate(object s, EventArgs e)
			{
				this.LayoutUI();
			};
			base.FormClosing += delegate(object s, FormClosingEventArgs e)
			{
				this.Stop(false);
			};
			base.HandleCreated += delegate(object s, EventArgs e)
			{
				Brand.DarkTitle(this);
			};
			this.animationTimer = new global::System.Windows.Forms.Timer
			{
				Interval = 50
			};
			this.animationTimer.Tick += delegate(object s, EventArgs e)
			{
				this.AnimationTick();
			};
			this.animationTimer.Start();
			this.heartbeatTimer = new global::System.Windows.Forms.Timer
			{
				Interval = 5000
			};
			this.heartbeatTimer.Tick += delegate(object s, EventArgs e)
			{
			};
			this.heartbeatTimer.Start();
			base.Shown += delegate(object s, EventArgs e)
			{
				if (!this.previewMode)
				{
					AutoUsb.Begin(this, true);
				}
			};
			this.LayoutUI();
			this.SetPhase("connecting");
			FileFeature.BuildUi(this);
			Transport.FormInit(this);
		}

		private void BuildClient()
		{
			this.roleLabel = this.MakeLabel(this.workPanel, "DAKTİLOCLIENT", 10f, Brand.Sub);
			this.mainHeading = this.MakeLabel(this.workPanel, "Bağlantı hazırlanıyor.", 24f, Brand.Text);
			this.orb = new StatusOrb();
			this.workPanel.Controls.Add(this.orb);
			this.statusTitle = this.MakeLabel(this.workPanel, "Kod alınıyor", 13f, Brand.Text);
			this.status = this.MakeLabel(this.workPanel, "Sunucu bağlantısı kuruluyor.", 10f, Brand.Sub);
			this.codeCard = new Card();
			this.workPanel.Controls.Add(this.codeCard);
			this.MakeLabel(this.codeCard, "Sizin bağlantı kodunuz", 10f, Brand.Sub).SetBounds(20, 15, 330, 23);
			this.key.BorderStyle = BorderStyle.None;
			this.key.ReadOnly = true;
			this.key.BackColor = Brand.Panel;
			this.key.ForeColor = Brand.Text;
			this.key.Font = Brand.Font(30f, false);
			this.key.Text = "— — —";
			this.key.TabStop = true;
			this.key.AccessibleName = "Bağlantı kodunuz";
			this.codeCard.Controls.Add(this.key);
			this.copyButton = this.MakeButton(this.codeCard, "Kopyala", delegate(object s, EventArgs e)
			{
				if (!Regex.IsMatch(this.key.Text, "^\\d{3} \\d{3} \\d{3}$"))
				{
					return;
				}
				try
				{
					Clipboard.SetText(this.key.Text);
					this.copyButton.Text = "Kopyalandı ✓";
				}
				catch
				{
					this.status.Text = "Kod kopyalanamadı; kodu elle seçip kopyalayabilirsiniz.";
				}
			});
			this.codeHint = this.MakeLabel(this.codeCard, "Kodu bilen kişi ekranınıza erişebilir.", 9f, Brand.Sub);
			this.screenAccess = this.MakeLabel(this.workPanel, "▣   Ekran paylaşımı                                      Hazırlanıyor", 10f, Brand.Sub);
			this.allow = new BrandToggle
			{
				Text = "↖   Klavye ve fare kontrolü",
				Checked = true,
				ForeColor = Brand.Text,
				BackColor = Brand.Bg,
				Font = Brand.Font(10f, false),
				AutoSize = false,
				AccessibleDescription = "Açıksa kodu bilen bağlantı kurulduğunda klavye ve fare kullanabilir."
			};
			this.workPanel.Controls.Add(this.allow);
			this.control = true;
			this.allow.CheckedChanged += delegate(object s, EventArgs e)
			{
				this.control = this.allow.Checked;
				if (!this.control)
				{
					lock (this.inputLock)
					{
						foreach (byte b in this.pressed)
						{
							App.keybd_event(b, 0, 2U, UIntPtr.Zero);
						}
						this.pressed.Clear();
						App.mouse_event(20U, 0U, 0U, 0U, UIntPtr.Zero);
					}
				}
			};
			this.usbState = this.MakeLabel(this.workPanel, "↟   USB bileşeni: " + (UsbBridge.IsInstalled(true) ? "Kurulu" : "Kurulu değil"), 10f, Brand.Sub);
			this.stop = this.MakeButton(this.workPanel, "■   Paylaşımı durdur", delegate(object s, EventArgs e)
			{
				this.Stop(true);
			});
			((ActionButton)this.stop).Destructive = true;
			this.stop.Enabled = false;
			this.localDriverButton = this.MakeButton(this.workPanel, "USB bileşenleri", delegate(object s, EventArgs e)
			{
				UsbBridge.Install(true, this);
				this.RefreshComponents();
			});
			((ActionButton)this.localDriverButton).Quiet = true;
			ActionButton actionButton = this.MakeButton(this.workPanel, "Kaynak ve lisanslar", delegate(object s, EventArgs e)
			{
				UsbBridge.ExportSources(this);
			});
			actionButton.Name = "licenses";
			actionButton.Quiet = true;
			this.brandHeading = this.MakeLabel(this.brandPanel, "UZAK DESTEK\n\nSiz buradasınız.\nBiz yanınızdayız.", 17f, Brand.Text);
			this.brandCaption = this.MakeLabel(this.brandPanel, "USB bileşeni eksikse yönetici izniyle kurulur.\nKod; ekran, dosya ve USB erişimi verir.", 10f, Brand.Sub);
			this.brandFoot = this.MakeLabel(this.brandPanel, "MR. DAKTİLO • REMOTE SUPPORT", 8f, Brand.Sub);
		}




		private void RefreshComponents()
		{
			this.usbState.Text = "↟   USB bileşeni: " + (UsbBridge.IsInstalled(true) ? "Kurulu" : "Kurulu değil");
		}

		private int ScalePx(int n)
		{
			return (int)Math.Round((double)n * ((double)base.DeviceDpi / 96.0));
		}

		private void LayoutUI()
		{
			if (this.shell == null || this.workPanel == null)
			{
				return;
			}
			this.footerText.SetBounds(this.ScalePx(18), this.ScalePx(8), Math.Max(100, this.footer.Width - this.ScalePx(210)), this.ScalePx(22));
			this.reducedMotion.SetBounds(Math.Max(0, this.footer.Width - this.ScalePx(160)), this.ScalePx(6), this.ScalePx(145), this.ScalePx(24));
			int width = this.shell.ClientSize.Width;
			int height = this.shell.ClientSize.Height;
			int num = (int)((double)width * 0.36);
			this.brandPanel.SetBounds(0, 0, num, height);
			this.workPanel.SetBounds(num + 1, 0, Math.Max(1, width - num - 1), height);
			int num2 = this.ScalePx(30);
			int num3 = this.workPanel.Width - 2 * num2;
			int num4 = this.ScalePx(26);
			int num5 = num - 2 * num4;
			this.logo.SetBounds(num4, this.ScalePx(34), num5, (int)((double)num5 * 2.0 / 3.0));
			int num6 = this.logo.Bottom + this.ScalePx(54);
			this.brandHeading.SetBounds(num4 + this.ScalePx(10), num6, num5 - this.ScalePx(10), this.ScalePx(136));
			this.brandCaption.SetBounds(num4 + this.ScalePx(10), num6 + this.ScalePx(145), num5 - this.ScalePx(10), this.ScalePx(80));
			this.brandFoot.SetBounds(num4, height - this.ScalePx(50), num5, this.ScalePx(25));
			this.roleLabel.SetBounds(num2, this.ScalePx(28), num3, this.ScalePx(22));
			this.mainHeading.SetBounds(num2, this.ScalePx(68), num3, this.ScalePx(50));
			this.orb.SetBounds(num2, this.ScalePx(129), this.ScalePx(48), this.ScalePx(48));
			this.statusTitle.SetBounds(num2 + this.ScalePx(63), this.ScalePx(126), num3 - this.ScalePx(63), this.ScalePx(31));
			this.status.SetBounds(num2 + this.ScalePx(63), this.ScalePx(157), num3 - this.ScalePx(63), this.ScalePx(44));
			this.codeCard.SetBounds(num2, this.ScalePx(210), num3, this.ScalePx(145));
			this.key.SetBounds(this.ScalePx(20), this.ScalePx(43), Math.Max(30, num3 - this.ScalePx(142)), this.ScalePx(55));
			this.copyButton.SetBounds(num3 - this.ScalePx(108), this.ScalePx(48), this.ScalePx(88), this.ScalePx(43));
			this.codeHint.SetBounds(this.ScalePx(20), this.ScalePx(110), num3 - this.ScalePx(40), this.ScalePx(28));
			this.screenAccess.SetBounds(num2, this.ScalePx(378), num3, this.ScalePx(29));
			this.allow.SetBounds(num2, this.ScalePx(416), num3, this.ScalePx(29));
			this.usbState.SetBounds(num2, this.ScalePx(454), num3, this.ScalePx(29));
			this.stop.SetBounds(num2, this.ScalePx(501), num3, this.ScalePx(47));
			this.localDriverButton.SetBounds(num2, this.ScalePx(564), num3 / 2, this.ScalePx(38));
			Control control = this.workPanel.Controls["licenses"];
			if (control != null)
			{
				control.SetBounds(num2 + num3 / 2, this.ScalePx(564), num3 / 2, this.ScalePx(38));
			}
		}




		private void SetPhase(string next)
		{
			this.phase = next;
			this.orb.Phase = next;
			this.orb.Invalidate();
			this.mainHeading.Text = ((next == "waiting") ? "Bağlantınız hazır." : ((next == "connected") ? "Bağlantı kuruldu." : ((next == "working") ? "MR. Daktilo iş başında." : ((next == "connecting") ? "Bağlantı hazırlanıyor." : "Paylaşım durduruldu."))));
			this.statusTitle.Text = ((next == "waiting") ? "Bağlantı bekleniyor" : ((next == "connected") ? "Bağlandı" : ((next == "working") ? "MR. Daktilo çalışıyor" : ((next == "connecting") ? "Kod alınıyor" : "Durduruldu"))));
			this.screenAccess.Text = "▣   Ekran paylaşımı: " + ((next == "waiting" || next == "connected" || next == "working") ? "Açık" : ((next == "connecting") ? "Hazırlanıyor" : "Kapalı"));
			this.codeHint.Text = ((next == "stopped") ? "Paylaşım kapalı; bu kod artık kullanılamaz." : "Kodu bilen kişi ekranınıza erişebilir.");
		}

		private void SetStatusText(string text)
		{
			this.status.Text = text;
			if (text.StartsWith("Kodunuzu"))
			{
				this.SetPhase("waiting");
			}
			if (text.StartsWith("BAĞLANTI AKTİF") || text.StartsWith("Bağlandı"))
			{
				this.sessionConnected = true;
				this.SetPhase("connected");
			}
		}

		private void ClientSessionReady()
		{
			this.sessionConnected = true;
			this.lastRemoteInput = DateTime.MinValue;
			this.SetPhase("connected");
			Transport.SessionReady(this);
		}




		private void SessionEnded(bool byUser)
		{
			this.sessionConnected = false;
			this.manualStop = byUser;
			this.SetPhase("stopped");
			this.copyButton.Text = "Kopyala";
			Bitmap bitmap = Interlocked.Exchange<Bitmap>(ref this.queuedFrame, null);
			if (bitmap != null)
			{
				bitmap.Dispose();
			}
			Transport.SessionEnded(this, byUser);
		}




		private void AnimationTick()
		{
			if (base.IsDisposed)
			{
				return;
			}
			this.animationStep = (this.animationStep + 4) % 360;
			this.orb.ReduceMotion = this.reducedMotion.Checked;
			this.orb.Angle = (float)this.animationStep;
			if (!this.reducedMotion.Checked && (this.phase == "connecting" || this.phase == "waiting" || this.phase == "working"))
			{
				this.orb.Invalidate();
			}
			if (this.sessionConnected)
			{
				bool flag = (DateTime.UtcNow - this.lastRemoteInput).TotalMilliseconds < 1400.0;
				string text = (flag ? "working" : "connected");
				if (text != this.phase)
				{
					this.SetPhase(text);
					this.status.Text = (flag ? "Uzak klavye veya fare etkinliği var." : "Karşı taraf ekranınızı görüntüleyebilir.");
				}
			}
			this.copyButton.Enabled = Regex.IsMatch(this.key.Text, "^\\d{3} \\d{3} \\d{3}$") && this.active;
		}

		public void SaveSnapshot(string path, string state)
		{
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			base.Location = new Point(-20000, -20000);
			base.Show();
			Application.DoEvents();
			this.key.Text = "482 719 635";
			this.active = true;
			this.stop.Enabled = true;
			this.SetPhase((state == "working") ? "working" : ((state == "connected") ? "connected" : "waiting"));
			this.status.Text = ((state == "working") ? "Tasarım kontrolü: örnek uzak giriş etkinliği." : "Tasarım kontrolü: gerçek paylaşım yapılmıyor.");
			this.footerText.Text = "Görsel kontrol • Örnek veriler, gerçek bağlantı yok";
			this.LayoutUI();
			Application.DoEvents();
			using (Bitmap bitmap = new Bitmap(base.Width, base.Height))
			{
				base.DrawToBitmap(bitmap, new Rectangle(0, 0, base.Width, base.Height));
				bitmap.Save(path, ImageFormat.Png);
			}
			base.Close();
		}

		public void VerifyUI()
		{
			base.ShowInTaskbar = false;
			base.StartPosition = FormStartPosition.Manual;
			base.Location = new Point(-20000, -20000);
			base.Show();
			Application.DoEvents();
			this.animationTimer.Stop();
			this.heartbeatTimer.Stop();
			if (this.logo.Image == null || base.Icon == null)
			{
				throw new Exception("Brand assets missing");
			}
			if (!this.allow.Checked || !this.control)
			{
				throw new Exception("Client control default incorrect");
			}
			this.SetPhase("waiting");
			if (this.statusTitle.Text != "Bağlantı bekleniyor")
			{
				throw new Exception("Waiting state incorrect");
			}
			this.sessionConnected = true;
			this.lastRemoteInput = DateTime.UtcNow;
			this.AnimationTick();
			if (this.phase != "working")
			{
				throw new Exception("Activity state not triggered");
			}
			this.lastRemoteInput = DateTime.UtcNow.AddSeconds(-5.0);
			this.AnimationTick();
			if (this.phase != "connected")
			{
				throw new Exception("Activity state did not settle");
			}
			foreach (Size size in new Size[]
			{
				new Size(1020, 740),
				new Size(840, 675)
			})
			{
				base.ClientSize = size;
				this.LayoutUI();
				if (this.stop.Bottom > this.workPanel.ClientSize.Height || this.stop.Right > this.workPanel.ClientSize.Width)
				{
					throw new Exception("Stop clipped");
				}
			}
			if (this.join.Parent != null)
			{
				throw new Exception("Client exposes outbound connect");
			}
			base.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (this.animationTimer != null)
				{
					this.animationTimer.Dispose();
				}
				if (this.heartbeatTimer != null)
				{
					this.heartbeatTimer.Dispose();
				}
				if (this.logo != null && this.logo.Image != null)
				{
					this.logo.Image.Dispose();
					this.logo.Image = null;
				}
				Bitmap bitmap = Interlocked.Exchange<Bitmap>(ref this.queuedFrame, null);
				if (bitmap != null)
				{
					bitmap.Dispose();
				}
			}
			base.Dispose(disposing);
		}

		private TextBox ip = new TextBox();

		private TextBox key = new TextBox();

		private Label status = new Label();

		private Button host = new Button();

		private Button join = new Button();

		private Button stop = new Button();

		private CheckBox allow = new CheckBox();

		private TcpClient pending;

		private Channel channel;

		private UsbBridge usb;

		private volatile bool active;

		private volatile bool control;

		private int generation;

		private HashSet<byte> pressed = new HashSet<byte>();

		private object inputLock = new object();

		private bool previewMode;

		private bool sessionConnected;

		private bool manualStop;

		private string phase = "connecting";

		

		private DateTime lastRemoteInput = DateTime.MinValue;

		private int animationStep;

		

		private Bitmap queuedFrame;

		private Panel shell;

		private Panel brandPanel;

		private Panel workPanel;

		private Panel footer;

		

		

		

		private Card codeCard;

		

		

		private PictureBox logo;

		private Label roleLabel;

		private Label mainHeading;

		private Label statusTitle;

		private Label screenAccess;

		private Label usbState;

		private Label footerText;

		private Label brandHeading;

		private Label brandCaption;

		private Label brandFoot;

		private Label codeHint;

		

		

		

		

		

		private StatusOrb orb;

		private ActionButton copyButton;

		

		

		

		

		private CheckBox reducedMotion;

		private global::System.Windows.Forms.Timer animationTimer;

		private global::System.Windows.Forms.Timer heartbeatTimer;

		private Control localDriverButton;
	}
}

