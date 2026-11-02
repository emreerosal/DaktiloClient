using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MrDaktilo.Performance;

namespace RemoteDaktilo
{
	internal sealed class StatusOrb : Control
	{
		public StatusOrb()
		{
			this.DoubleBuffered = true;
			base.Size = new Size(48, 48);
			base.AccessibleRole = AccessibleRole.Graphic;
			base.AccessibleName = "Bağlantı durumu";
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			bool flag = this.Phase == "working";
			bool flag2 = this.Phase == "connected";
			bool flag3 = this.Phase == "waiting" || this.Phase == "connecting";
			Color color = (flag ? Color.FromArgb(255, 131, 144) : (flag2 ? Brand.Green : (flag3 ? Brand.Amber : Brand.Sub)));
			Color color2 = (flag ? Color.FromArgb(64, 38, 46) : (flag2 ? Color.FromArgb(28, 58, 48) : (flag3 ? Color.FromArgb(41, 34, 27) : Color.FromArgb(44, 51, 63))));
			using (SolidBrush solidBrush = new SolidBrush(color2))
			{
				e.Graphics.FillEllipse(solidBrush, 2, 2, base.Width - 4, base.Height - 4);
			}
			using (Pen pen = new Pen(color, 2f))
			{
				if (flag3)
				{
					e.Graphics.DrawArc(pen, 5f, 5f, (float)(base.Width - 11), (float)(base.Height - 11), this.ReduceMotion ? (-90f) : this.Angle, 235f);
				}
				else if (flag2)
				{
					e.Graphics.DrawEllipse(pen, 5, 5, base.Width - 11, base.Height - 11);
				}
			}
			if (flag)
			{
				using (SolidBrush solidBrush2 = new SolidBrush(color))
				{
					for (int i = 0; i < 3; i++)
					{
						int num = (this.ReduceMotion ? 12 : ((int)(9.0 + 11.0 * (0.5 + 0.5 * Math.Sin((double)this.Angle * 0.04 + (double)i)))));
						e.Graphics.FillRectangle(solidBrush2, 16 + i * 6, (base.Height - num) / 2, 3, num);
					}
					return;
				}
			}
			TextRenderer.DrawText(e.Graphics, flag2 ? "✓" : (flag3 ? "↙" : "■"), AdaptiveCapture.CachedFont(16f, false), base.ClientRectangle, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}

		public string Phase = "connecting";

		public float Angle;

		public bool ReduceMotion;
	}
}
