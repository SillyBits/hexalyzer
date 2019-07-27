using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;


namespace Hexalyzer.Helper
{

	public interface ITextFormatter
	{
		string Format(long offset, ProjectNode node);
		Brush Color(long offset, ProjectNode node);
	}


	public class OffsetFormatter : ITextFormatter
	{
		public string Format(long offset, ProjectNode node)
		{
			offset -= offset % Settings.BYTES_PER_ROW;
			return offset.ToOffsetString(false);
		}

		public Brush Color(long offset, ProjectNode node)
		{
			if (node.Type != null)
				return Brushes.DarkGreen;
			return Brushes.Black;
		}
	}

	public class HexFormatter : ITextFormatter
	{
		public string Format(long offset, ProjectNode node)
		{
			StringBuilder sb = new StringBuilder(Settings.CHARS_PER_ROW);

			long col = offset % Settings.BYTES_PER_ROW;
			offset -= node.Offset;
			if (offset < 0)
				offset = 0;

			if (col > 0)
			{
				for (int i = 0; i < col;)
				{
					sb.Append("  ");

					++i;
					if (i > 0 && i % 4 == 0)
					{
						if (col < Settings.BYTES_PER_ROW)
							sb.Append(Settings.COL_SPACER);
					}
					else
						sb.Append(' ');
				}
			}

			while (col < Settings.BYTES_PER_ROW)
			{
				if (offset >= node.Length)
					break;
				byte b = node[offset];//data[(int)offset];
				offset++;

				sb.Append(b.ToString("X2"));

				col++;
				if (col > 0 && col % 4 == 0)
				{
					if (col < Settings.BYTES_PER_ROW)
						sb.Append(Settings.COL_SPACER);
				}
				else
					sb.Append(' ');
			}

			return sb.ToString();
		}

		public Brush Color(long offset, ProjectNode node)
		{
			return Brushes.Black;
		}
	}

	public class AsciiFormatter : ITextFormatter
	{
		public string Format(long offset, ProjectNode node)
		{
			StringBuilder sb = new StringBuilder(Settings.BYTES_PER_ROW);

			long col = offset % Settings.BYTES_PER_ROW;
			offset -= node.Offset;
			if (offset < 0)
				offset = 0;

			if (col > 0)
				sb.Append(' ', (int)col);

			while (col < Settings.BYTES_PER_ROW)
			{
				if (offset >= node.Length)
					break;
				byte b = node[offset];//data[(int)offset];
				offset++;

				sb.Append((32 <= b && b <= 127) ? (char)b : '.');

				col++;
			}

			return sb.ToString();
		}

		public Brush Color(long offset, ProjectNode node)
		{
			return Brushes.Black;
		}
	}

	public class ValueFormatter : ITextFormatter
	{
		public string Format(long offset, ProjectNode node)
		{
			// Display values on first line only
			if (offset == node.Offset)
				return Datatypes.Helpers.ToString(node.Type, node.Data);
			return "";
		}

		public Brush Color(long offset, ProjectNode node)
		{
			return Brushes.Black;
		}
	}

	public class RemarkFormatter : ITextFormatter
	{
		public string Format(long offset, ProjectNode node)
		{
			// Display remarks on first line only
			if (offset == node.Offset)
				return node.Remark;
			return "";
		}

		public Brush Color(long offset, ProjectNode node)
		{
			return Brushes.Black;
		}
	}


}
