using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RemoteDaktilo
{
	internal static class Brand
	{
		public static Font Font(float size, bool emphasis = false)
		{
			return new Font("Segoe UI", size, emphasis ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point);
		}

		public static GraphicsPath Round(Rectangle r, int radius)
		{
			GraphicsPath graphicsPath = new GraphicsPath();
			int num = radius * 2;
			graphicsPath.AddArc(r.Left, r.Top, num, num, 180f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Top, num, num, 270f, 90f);
			graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
			graphicsPath.AddArc(r.Left, r.Bottom - num, num, num, 90f, 90f);
			graphicsPath.CloseFigure();
			return graphicsPath;
		}

		public static Image Logo()
		{
			Image image2;
			using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Daktilo.Brand.Logo"))
			{
				using (Image image = Image.FromStream(manifestResourceStream))
				{
					image2 = new Bitmap(image);
				}
			}
			return image2;
		}

		public static Icon AppIcon()
		{
			Icon icon;
			using (Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Daktilo.Brand.Icon"))
			{
				icon = new Icon(manifestResourceStream);
			}
			return icon;
		}

		public static Label Label(string text, float size, Color color)
		{
			return new Label
			{
				Text = text,
				Font = Brand.Font(size, false),
				ForeColor = color,
				BackColor = Color.Transparent,
				AutoSize = false,
				UseMnemonic = false
			};
		}

		public static void StyleForm(Form form)
		{
			form.BackColor = Brand.Bg;
			form.ForeColor = Brand.Text;
			form.Font = Brand.Font(10f, false);
			form.AutoScaleDimensions = new SizeF(96f, 96f);
			form.AutoScaleMode = AutoScaleMode.Dpi;
			form.Icon = Brand.AppIcon();
		}

		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

		public static void DarkTitle(Form form)
		{
			try
			{
				int num = 1;
				Brand.DwmSetWindowAttribute(form.Handle, 20, ref num, 4);
			}
			catch
			{
			}
		}

		public static readonly Color Bg = Color.FromArgb(16, 18, 22);

		public static readonly Color Panel = Color.FromArgb(25, 28, 34);

		public static readonly Color Line = Color.FromArgb(61, 66, 77);

		public static readonly Color Text = Color.FromArgb(245, 246, 250);

		public static readonly Color Sub = Color.FromArgb(185, 193, 207);

		public static readonly Color Red = Color.FromArgb(240, 52, 69);

		public static readonly Color Green = Color.FromArgb(114, 221, 177);

		public static readonly Color Amber = Color.FromArgb(244, 193, 120);
	}
}
