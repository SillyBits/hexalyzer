using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace Hexalyzer
{

	/// <summary>
	/// Extension methods
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Helper to convert offset into string using our configured format
		/// </summary>
		/// <param name="offset">Offset to convert</param>
		/// <param name="with_type">If type specifier 'h' should be appended</param>
		/// <returns>String representation of offset given</returns>
		public static string ToOffsetString(this long offset, bool with_type = true)
		{
			string str = offset.ToString(Settings.OFFSET_FORMAT);
			if (with_type)
				str += "h";
			return str;
		}

		/// <summary>
		/// Checks if a call to Dispatcher.[Begin]Invoke is required
		/// </summary>
		/// <param name="dispatcher">Dispatcher to check with</param>
		/// <returns>If true calling .[Begin]Invoke is required</returns>
		public static bool IsInvokeRequired(this Dispatcher dispatcher)
		{
			return (dispatcher.Thread != Thread.CurrentThread);
		}

		/// <summary>
		/// Clone given point
		/// </summary>
		/// <param name="pt">Point to clone</param>
		/// <returns>Cloned instance</returns>
		public static Point Clone(this Point pt)
		{
			return new Point(pt.X, pt.Y);
		}
	}


	/// <summary>
	/// Scopes known
	/// </summary>
	public enum Scope { None=0, Hex, Ascii, Data, Offset, Value, Remark };


	/// <summary>
	/// Character-based position
	/// </summary>
	public class Position
	{
		public long Row;
		public long Col;


		public Position(int row, int col)
		{
			Row = row;
			Col = col;
		}

		public Position(Position pos)
		{
			Row = pos.Row;
			Col = pos.Col;
		}

		/// <summary>
		/// Return new Position instance for given character-based position
		/// </summary>
		/// <param name="chars">Character position</param>
		/// <param name="is_start">If true, position given is start, else end of a selection</param>
		/// <param name="source">Source scope for offset passed in</param>
		public Position(long chars, bool is_start, Scope source)
		{
			//int line, byteno;
			long ofs;

			switch (source)
			{
				case Scope.Hex:

					Row = chars / Settings.CHARS_PER_ROW;

					ofs = chars % Settings.CHARS_PER_ROW;
					long col = ofs / (Settings.CHARS_PER_COL + Settings.COL_SEP_CHARS);
					ofs -= col * (Settings.CHARS_PER_COL + Settings.COL_SEP_CHARS);
					//if(!is_start)
					//	ofs = ((ofs + 2) / 3) * 3;
					Col = (ofs / 3) + (col * Settings.BYTES_PER_COL);

					break;

				case Scope.Ascii:

					if (!is_start)
						--chars;

					Row = chars / Settings.BYTES_PER_ROW;

					Col = chars % Settings.BYTES_PER_ROW;
					if (!is_start && Col >= Settings.BYTES_PER_ROW)
						--Col;

					break;

				case Scope.Data:

					if (!is_start)
						--chars;

					Row = chars / Settings.BYTES_PER_ROW;

					Col = chars % Settings.BYTES_PER_ROW;

					if (!is_start && Col == 0)
					{
						Col = Settings.BYTES_PER_ROW - 1;
						Row--;
					}

					break;
			}
		}


		/// <summary>
		/// Clone ourself
		/// </summary>
		/// <returns>New Position instance</returns>
		public Position Clone()
		{
			return new Position(this);
		}


		/// <summary>
		/// Find character-based offset
		/// </summary>
		/// <param name="is_start">If true, treat as start position, else end of a selection</param>
		/// <param name="target">Target scope for offset returned</param>
		/// <returns>Offset in chars</returns>
		public long Offset(bool is_start, Scope target)
		{
			switch (target)
			{
				case Scope.Hex:
					long offset = (Row * Settings.CHARS_PER_ROW) 
						        + (Col * 3) - 1 
								+ ((Col / Settings.BYTES_PER_COL) * Settings.COL_SEP_CHARS)
								;
					if (!is_start)
						offset += 2;
					return offset;

				case Scope.Ascii:
					return (Row * Settings.BYTES_PER_ROW) + Col;

				case Scope.Data:
					return (Row * Settings.BYTES_PER_ROW) + Col;
			}
			throw new ArgumentException();
		}


		public static bool operator < (Position a, Position b)
		{
			if (a.Row < b.Row)
				return true;
			else if (a.Row > b.Row)
				return false;
			return (a.Col < b.Col);
		}

		public static bool operator > (Position a, Position b)
		{
			if (a.Row > b.Row)
				return true;
			else if (a.Row < b.Row)
				return false;
			return (a.Col > b.Col);
		}

		public override bool Equals(object obj)
		{
			Position pos = obj as Position;
			if (pos == null)
				return base.Equals(obj);
			return (pos.Col == Col) && (pos.Row == Row);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("[Position] R{0:D2} / C{1:D2}", Row, Col);
		}
	}


	/*public class Difference
	{
		public int NumRows;
		public int NumCols;


		public Difference(int numrows, int numcols)
		{
			NumRows = numrows;
			NumCols = numcols;
		}

		public Difference(Position start, Position end)
		{
			NumRows = Math.Abs(end.Row - start.Row);
			NumCols = Math.Abs(end.Col - start.Col);
		}

		public override string ToString()
		{
			return string.Format("[Difference] {0} / {1}", NumCols, NumRows);
		}
	}*/

}
