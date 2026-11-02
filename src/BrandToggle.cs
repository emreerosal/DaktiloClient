using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RemoteDaktilo
{
	internal sealed class BrandToggle : CheckBox
	{
		public BrandToggle()
		{
			this.AutoSize = false;
			base.Height = 29;
			this.Font = Brand.Font(10f, false);
			this.ForeColor = Brand.Text;
			this.BackColor = Brand.Bg;
			base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush solidBrush = new SolidBrush(this.BackColor))
			{
				e.Graphics.FillRectangle(solidBrush, base.ClientRectangle);
			}
			Rectangle rectangle = new Rectangle(0, 0, Math.Max(10, base.Width - 52), base.Height);
			TextRenderer.DrawText(e.Graphics, this.Text, this.Font, rectangle, base.Enabled ? this.ForeColor : Brand.Sub, TextFormatFlags.VerticalCenter);
			Rectangle rectangle2 = new Rectangle(base.Width - 38, (base.Height - 20) / 2, 36, 20);
			using (GraphicsPath graphicsPath = Brand.Round(rectangle2, 10))
			{
				using (SolidBrush solidBrush2 = new SolidBrush(base.Checked ? Brand.Red : Brand.Line))
				{
					e.Graphics.FillPath(solidBrush2, graphicsPath);
				}
			}
			using (SolidBrush solidBrush3 = new SolidBrush(Brand.Text))
			{
				e.Graphics.FillEllipse(solidBrush3, rectangle2.X + (base.Checked ? 19 : 3), rectangle2.Y + 3, 14, 14);
			}
			if (this.Focused && this.ShowFocusCues)
			{
				ControlPaint.DrawFocusRectangle(e.Graphics, base.ClientRectangle, Brand.Text, this.BackColor);
			}
		}
	}
}
