using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RemoteDaktilo
{
	internal sealed class Card : Panel
	{
		public Card()
		{
			this.DoubleBuffered = true;
			this.BackColor = Brand.Bg;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle rectangle = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
			if (rectangle.Width < 24 || rectangle.Height < 24)
			{
				return;
			}
			using (GraphicsPath graphicsPath = Brand.Round(rectangle, this.Radius))
			{
				using (SolidBrush solidBrush = new SolidBrush(this.Fill))
				{
					using (Pen pen = new Pen(this.Border))
					{
						e.Graphics.FillPath(solidBrush, graphicsPath);
						e.Graphics.DrawPath(pen, graphicsPath);
					}
				}
			}
		}

		public Color Fill = Brand.Panel;

		public Color Border = Brand.Line;

		public int Radius = 12;
	}
}
