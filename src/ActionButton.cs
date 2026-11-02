using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RemoteDaktilo
{
	internal sealed class ActionButton : Button
	{
		public ActionButton()
		{
			base.FlatStyle = FlatStyle.Flat;
			base.FlatAppearance.BorderSize = 0;
			this.BackColor = Brand.Panel;
			this.ForeColor = Brand.Text;
			this.Font = Brand.Font(10f, false);
			this.Cursor = Cursors.Hand;
			base.Height = 46;
			base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			this.hover = true;
			base.Invalidate();
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			this.hover = false;
			base.Invalidate();
			base.OnMouseLeave(e);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Color color = (this.Primary ? Color.FromArgb(230, 38, 57) : (this.Destructive ? Color.FromArgb(65, 37, 45) : (this.Selected ? Color.FromArgb(51, 35, 44) : (this.Quiet ? base.Parent.BackColor : Color.FromArgb(43, 51, 65)))));
			Color color2 = (this.Primary ? Color.FromArgb(255, 89, 105) : (this.Destructive ? Color.FromArgb(192, 84, 98) : (this.Selected ? Color.FromArgb(101, 54, 67) : Brand.Line)));
			Color color3 = (this.Destructive ? Color.FromArgb(255, 189, 199) : Brand.Text);
			if (this.hover && base.Enabled)
			{
				color = ControlPaint.Light(color, 0.12f);
			}
			if (!base.Enabled)
			{
				color = Color.FromArgb(30, 34, 42);
				color3 = Color.FromArgb(126, 139, 159);
				color2 = Brand.Line;
			}
			using (GraphicsPath graphicsPath = Brand.Round(new Rectangle(0, 0, base.Width - 1, base.Height - 1), 8))
			{
				using (SolidBrush solidBrush = new SolidBrush(color))
				{
					using (Pen pen = new Pen(color2))
					{
						e.Graphics.FillPath(solidBrush, graphicsPath);
						if (!this.Quiet)
						{
							e.Graphics.DrawPath(pen, graphicsPath);
						}
					}
				}
			}
			TextRenderer.DrawText(e.Graphics, this.Text, this.Font, base.ClientRectangle, color3, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			if (this.Focused && this.ShowFocusCues)
			{
				ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, base.Width - 8, base.Height - 8), Brand.Text, color);
			}
		}

		public bool Primary;

		public bool Destructive;

		public bool Quiet;

		public bool Selected;

		private bool hover;
	}
}
