using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using Hexalyzer.Datatypes;


/*
 * TODO:
 * 
 * - Remark textbox: Allow to navigate using mouse w/o closing it
 * 
 */


namespace Hexalyzer
{

	/// <summary>
	/// Interaction logic for ProjectView.xaml
	/// </summary>
	public partial class ProjectView : UserControl
	{

		public ProjectFile Project
		{
			get { return _Project; }
			set
			{
				bool changed = (_Project != value);

				_Project = value;

				if (changed)
				{
					_StopAnalyzer();

					if (_Project == null && _IsCaretVisible != null)
						_HideCaret();

					_ClearSelection();

					_Manager.Update(_Project != null ? Project.Nodes : null);

					// Trigger re-rendering
					Offset = 0;
				}
			}
		}

		public delegate void OffsetChangedHandler(object sender, long from_offset, long to_offset);
		public event OffsetChangedHandler OffsetChanged;
		public long Offset
		{
			get { return _Offset; }
			set
			{
				if (_IsAnalyzerActive)
					_StopAnalyzer();

				if (_Project == null)
					return;

				long delta = _Offset - value;
				_Offset = value;

				long from = -1, to = -1;
				if (_Offset >= 0)
				{
					_Manager.MoveToOffset(_Offset);

					_Render();

					from = _Offset;
					to   = _Manager.LastVisibleRow.Offset + _Manager.LastVisibleRow.Length - 1;
				}

				OffsetChanged?.Invoke(this, from, to);

				if (_MouseLeftPos >= 0)
				{
					if (_IsCaretVisible != null)
					{
						_HideCaret();
						_CaretPos = _OffsetToPixels(_MouseLeftPos, _MouseLeftScope);
						_ShowCaret();
					}

					if (_SelectionStart >= 0)
					{
						_ShowSelection();
					}

					if (_RemarkTextBox != null)
					{
						ProjectNode node = _Project.FindNodeByOffset(_MouseLeftPos);
						if (node != null)
						{
							Position pos = _Manager.OffsetToChars(node.Offset);
							double y = (pos.Row * RenderHelper.RowHeight) + RenderHelper.HeaderHeight - 3;
							if (y <= RenderHelper.HeaderHeight)
								y = RenderHelper.HeaderHeight;
							else if (y > (ActualHeight - _RemarkTextBox.Height))
								y = ActualHeight - _RemarkTextBox.Height;

							Thickness margin = _RemarkTextBox.Margin;
							margin.Top = y;
							_RemarkTextBox.Margin = margin;
						}
					}
				}

				// Let analyzer know offset was changed
				if (delta != 0 && _IsAnalyzerActive)
					_StartAnalyzer();
			}
		}

		public delegate void SelectionChangedHandler(object sender, long from_offset, long to_offset);
		public event SelectionChangedHandler SelectionChanged;
		public long Selection
		{
			get	{ return _MouseLeftPos; }
			set
			{
				long from = -1, to = -1;
				if (_MouseLeftScope != Scope.Remark)
				{
					if (_SelectionStart >= 0)
					{
						from = _SelectionStart;
						to   = _MouseLeftPos;
						if (from > to)
						{
							long swap = from;
							from = to;
							to   = swap;
						}
						to--;
					}
					else if (_MouseLeftPos >= 0)
					{
						from = _MouseLeftPos;
						to   = 0;
					}
				}
				SelectionChanged?.Invoke(this, from, to);
			}
		}

		//public delegate void CurrentNodeChangedHandler(object sender, ProjectNode node);
		//public event CurrentNodeChangedHandler CurrentNodeChanged;
		public ProjectNode CurrentNode
		{
			get
			{
				if (_MouseLeftPos < 0 || _Project == null)
					return null;
				return _Project.FindNodeByOffset(_MouseLeftPos);
			}
			//set
			//{
			//	CurrentNodeChanged?.Invoke(this, CurrentNode);
			//}
		}

		public bool IsAnalyzerActive
		{
			get { return _IsAnalyzerActive; }
			set
			{
				_IsAnalyzerActive = value;
				if (_IsAnalyzerActive)
				{
					//_StartAnalyzer();
					//=> Redrawing will trigger analyzer
					Offset = _Offset;
				}
				else
				{
					_StopAnalyzer();
					_BackingStoreAnalyzer.Children.Clear();
				}
			}
		}

		public string[] Info
		{
			get
			{
				return new string[] {
					"Offset   : " + _Offset.ToOffsetString(),
					"Mouse ofs: " + _MouseLeftPos.ToOffsetString(),
					"Selection: " + _SelectionStart.ToOffsetString(),
					"Row manager:",
					string.Format("- Total   : {0}", _Manager.TotalRows),
					string.Format("- Top     : {0}", _Manager.FirstVisibleIndex),
					string.Format("- Visible : {0}", _Manager.VisibleRows),
					string.Format("- Last    : {0}", _Manager.LastVisibleIndex),
					string.Format("- Memory* : {0}", _Manager.TotalRows * RowManager.Row.SizeOf),
					"Scroll bar:",
					string.Format("- Value   : {0}", scroller.Value),
					string.Format("- Maximum : {0}", scroller.Maximum),
					string.Format("- Viewport: {0}", scroller.ViewportSize),
					"Benchmarks:",
					string.Format("- Render  : {0} ms",
						(_RenderStopWatch != null) ? _RenderStopWatch.ElapsedMilliseconds.ToString() : "./."),
					string.Format("- Analyzer: {0} ms", 
						(_AnalyzerStopWatch != null) ? _AnalyzerStopWatch.ElapsedMilliseconds.ToString() : "./."),
				};
			}
		}


		public ProjectView()
		{
			_Project = null;
			_Offset = 0;
			_MouseLeftPos = -1;
			_SelectionStart = -1;

			Initialized += _Initialized;
			Loaded += _Loaded;

			InitializeComponent();
		}

		~ProjectView()
		{
			_ShutdownAnalyzerThread();

			_IsCaretVisible = null;
			if (_CaretAnimTimer != null)
				_CaretAnimTimer.Dispose();
			_CaretAnimTimer = null;
		}

		static ProjectView()
		{
			_HoverRectColor = new Color() { R = 0xEE, G = 0xEE, B = 0xEE, A = 0x60 };

			_HoverRectBrush = new SolidColorBrush(_HoverRectColor);
			_HoverRectBrush.Freeze();

			_CaretPenShow = new Pen(Brushes.Black, 1);
			_CaretPenShow.Freeze();
			_CaretPenErase = new Pen(Brushes.Transparent, 1);
			_CaretPenErase.Freeze();
		}



		public void DoKeyDown(KeyEventArgs e)
		{
			OnKeyDown(e);
		}

		public void DoKeyUp(KeyEventArgs e)
		{
			OnKeyUp(e);
		}


		/// <summary>
		/// Add new resource at currently selected position
		/// </summary>
		/// <param name="type">Type of resource to add</param>
		public void AddResource(Type type)
		{
			if (_MouseLeftPos < 0)
				return;
			
			ProjectNode source = CurrentNode;
			if (source == null)
				return;

			long offset = _MouseLeftPos;

			long split_at = offset - source.Offset;
			ProjectNode[] added = _Project.InsertAt(source, type, split_at, -1);
			if (added == null)
				return;

			// Move caret to end of newly added node
			// (or end of previous node if an un-typed split was done)
			ProjectNode selected = null;
			foreach (ProjectNode node in added)
			{
				if (node.Offset == source.Offset + split_at && node.Type == type)
				{
					selected = node;
					break;
				}
			}
			if (selected != null)
			{
				// End of new node for typed, start of node for un-typed ones
				long length = (type != null) ? selected.Length : 0;
				_MouseLeftPos += length;

				_CaretPos = _OffsetToPixels(_MouseLeftPos, _MouseLeftScope);
				_ShowCaret();
			}

			// Adjust managed cache
			_Manager.Add(source, added);

			// Trigger redraw
			Offset = _Offset;
		}

		/// <summary>
		/// Remove resource currently selected
		/// </summary>
		public void RemoveResource()
		{
			ProjectNode source = CurrentNode;
			if (source == null)
				return;

			ProjectNode[] removed;
			ProjectNode replacement = _Project.Remove(source, out removed);
			if (replacement == null || removed == null)
				return;

			_HideCaret();

			// Adjust managed cache
			_Manager.Remove(removed, replacement);

			// Trigger redraw
			Offset = _Offset;
		}



		protected override void OnDpiChanged(DpiScale oldDpiScaleInfo, DpiScale newDpiScaleInfo)
		{
			base.OnDpiChanged(oldDpiScaleInfo, newDpiScaleInfo);

			// Renew caches
			RenderHelper.UpdateCache(newDpiScaleInfo);

			_Manager.VisibleRows = (int)((ActualHeight - RenderHelper.HeaderHeight) / RenderHelper.RowHeight);
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			if (sizeInfo.HeightChanged)
			{
				_Manager.VisibleRows = (int)((sizeInfo.NewSize.Height - RenderHelper.HeaderHeight) / RenderHelper.RowHeight);
			}

			//_Render(); => base will trigger this
			base.OnRenderSizeChanged(sizeInfo);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			_Render();

			// Draw in order of importance with lowest first
			drawingContext.DrawDrawing(_BackingStoreHovering);
			drawingContext.DrawDrawing(_BackingStoreAnalyzer);
			drawingContext.DrawDrawing(_BackingStoreSelection);
			drawingContext.DrawDrawing(_BackingStoreContent);
			drawingContext.DrawDrawing(_BackingStoreCaret);
		}

		protected override void OnMouseWheel(MouseWheelEventArgs e)
		{
			e.Handled = true;

			if (_Project == null)
				return;

			// Received a 120 for a single move
			// Make sure this is always the case
			int lines = -e.Delta / 20;

			Offset = _Manager.MoveBy(lines);
		}

		protected override void OnMouseLeave(MouseEventArgs e)
		{
			base.OnMouseLeave(e);

			_HideRowFeedback();

			Cursor = null;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (_Project == null)
			{
				_HideRowFeedback();
				return;
			}

			Point pos = e.GetPosition(this);

			_UpdateRowFeedback(pos);

			if (_ActiveColumn != null)
			{
				_ActiveColumn.Width = pos.X - _ActiveColumn.Left;
				if (_ActiveColumn.Width < 10)
					_ActiveColumn.Width = 10;

				_UpdateColumns();
				_Render();
			}
			else if (e.LeftButton == MouseButtonState.Pressed)
			{
				_HideCaret();
				_CloseRemarkTextbox();

				Position chars;
				Scope scope = _PixelsToChars(pos, false, out chars);
				if (scope == _MouseLeftScope && (scope == Scope.Hex || scope == Scope.Ascii))
				{
					if (_SelectionStart < 0)
						_SelectionStart = _MouseLeftPos;
					_MouseLeftPos = _Manager.CharsToOffset(chars);
					_ShowSelection();
				}
				else
				{
					_ClearSelection();
				}
			}
			else
			{
				if (pos.Y <= RenderHelper.HeaderHeight)
				{
					TextColumn active = null;
					foreach (TextColumn column in _Columns)
					{
						double right = column.Left + column.ActualWidth;
						if (right - 2 <= pos.X && pos.X <= right + 2)
						{
							active = column;
							break;
						}
					}
					Cursor = (active != null) ? Cursors.SizeWE : null;
				}
				else
				{
					Cursor = null;
				}
			}
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			if (_Project == null)
				return;

			Point pos = e.GetPosition(this);

			if (_RemarkTextBox != null && _RemarkTextBox == InputHitTest(pos))
			{
				// Let's see if bailing out here will do the trick with textbox accepting clicks
				return;
			}
			else if (pos.Y <= RenderHelper.HeaderHeight)
			{
				if (!IsMouseCaptured)
					CaptureMouse();

				_ClearSelection();
				_MouseLeftScope = Scope.None;
				if (Cursor != null)
				{
					_ActiveColumn = null;
					foreach (TextColumn column in _Columns)
					{
						double right = column.Left + column.ActualWidth;
						if (right - 2 <= pos.X && pos.X <= right + 2)
						{
							_ActiveColumn = column;
							break;
						}
					}
				}
				else
					_ActiveColumn = null;
			}
			else
			{
				Position chars;
				_MouseLeftScope = _PixelsToChars(pos, out chars);
				_MouseLeftPos = _Manager.CharsToOffset(chars);
				_ClearSelection();
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);

			if (IsMouseCaptured)
				ReleaseMouseCapture();

			if (_Project == null)
				return;

			Point pos = e.GetPosition(this);

			if (_RemarkTextBox != null && _RemarkTextBox == InputHitTest(pos))
			{
				// Let's see if bailing out here will do the trick with textbox accepting clicks
				return;
			}
			else if (_ActiveColumn != null)
			{
				_ActiveColumn = null;
				Cursor = null;
			}
			//else if (_SelectionStart != null)
			//{
			//	//TODO: Trigger event for main window
			//}
			else
			{
				Position up;
				Scope target = _PixelsToChars(pos, out up);
				if (target == _MouseLeftScope && _MouseLeftPos == _Manager.CharsToOffset(up))
				{
					if (target == Scope.Hex || target == Scope.Ascii)
					{
						// Adjust column if needed
						var info = _Manager[_Manager.FirstVisibleIndex + up.Row];
						int start = info.StartCol;
						if (start > up.Col)
						{
							up.Col = start;
						}
						else
						{
							int end = info.EndCol;
							if (end < up.Col)
								up.Col = end;
						}
						_MouseLeftPos = _Manager.CharsToOffset(up);

						_CloseRemarkTextbox();
						_ShowCaret();
					}
					else if (target == Scope.Remark)
					{
						_HideCaret();

						_ShowRemarkTextbox();
					}
					else
					{
						_HideCaret();
						_CloseRemarkTextbox();
					}
				}
				else
				{ 
					_HideCaret();
					_CloseRemarkTextbox();
				}
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (_Project == null)
				return;

			if (_IsCaretVisible == null)
				return;

			bool shift = e.KeyboardDevice.Modifiers == ModifierKeys.Shift;
			bool ctrl  = e.KeyboardDevice.Modifiers == ModifierKeys.Control;

			int row = _Manager.FindRowIndexByOffset(_MouseLeftPos, false);
			int col = (int)(_MouseLeftPos % Settings.BYTES_PER_ROW);
			RowManager.Row r = _Manager[row];

			switch (e.Key)
			{
				case Key.Up:
					row--;
					break;

				case Key.Down:
					row++;
					break;

				case Key.Left:
					if (col > r.StartCol)
						col--;
					else
					{
						col = Settings.BYTES_PER_ROW - 1;
						row--;
					}
					break;

				case Key.Right:
					if (col < r.EndCol)
						col++;
					else
					{
						col = 0;
						row++;
					}
					break;

				case Key.Home:
					col = 0;
					if (ctrl)
						row = 0;
					break;

				case Key.End:
					col = Settings.BYTES_PER_ROW - 1;
					if (ctrl)
						row = _Manager.TotalRows - 1;
					break;

				case Key.PageUp:
					row -= _Manager.VisibleRows;
					break;

				case Key.PageDown:
					row += _Manager.VisibleRows;
					break;

				default:
					return;
			}

			if (row < 0)
			{
				row = 0;
				col = 0;
			}
			else if (row > _Manager.TotalRows - 1)
			{
				row = _Manager.TotalRows - 1;
				col = Settings.BYTES_PER_ROW - 1;
			}
			r = _Manager[row];

			if (col < r.StartCol)
				col = r.StartCol;
			else if (col > r.EndCol)
				col = r.EndCol;

			if (shift && _SelectionStart < 0)
				_SelectionStart = _MouseLeftPos;

			_MouseLeftPos = (r.Offset & Settings.OFFSET_MASK) + col;
			Selection = 0;

			if (row < _Manager.FirstVisibleIndex)
				Offset = r.Offset;
			else if (row > _Manager.TotalRows - _Manager.VisibleRows + 1)
				Offset = _Manager.MoveBy(_Manager.TotalRows - _Manager.VisibleRows + 1);
			else if (row >= _Manager.LastVisibleIndex)
				Offset = _Manager.MoveBy(row - _Manager.LastVisibleIndex);
			else
				Offset = _Offset;

			if (shift)
				_ShowSelection();
			else
				_ClearSelection();
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (_MouseLeftScope == Scope.Hex || _MouseLeftScope == Scope.Ascii)
			{
				if (e.KeyboardDevice.Modifiers != ModifierKeys.Control)
				{
					//Debug.WriteLine("KeyUp: Keycode={0}", e.Key);

					bool shift = (e.KeyboardDevice.Modifiers == ModifierKeys.Shift);
					bool handled = true;
					switch (e.Key)
					{
						case Key.OemPlus:
						case Key.Add: AddResource(null); break;

						case Key.S: AddResource(typeof(VarString)); break;
						case Key.A: AddResource(typeof(AsciiString)); break;
						case Key.U: AddResource(typeof(WideString)); break;

						case Key.D1: AddResource(shift ? typeof(byte)   : typeof(sbyte)); break;
						case Key.D2: AddResource(shift ? typeof(ushort) : typeof(short)); break;
						case Key.D4: AddResource(shift ? typeof(uint)   : typeof(int)  ); break;
						case Key.D8: AddResource(shift ? typeof(ulong)  : typeof(long) ); break;

						case Key.F: AddResource(typeof(float)); break;
						case Key.D: AddResource(typeof(double)); break;

						case Key.Delete: RemoveResource(); break;

						case Key.F2:
						case Key.R: _ShowRemarkTextbox(); break;

						default: handled = false; break;
					}
					e.Handled = handled;
				}
			}
			else if (_MouseLeftScope == Scope.Remark)
			{
				//TODO:
			}
		}


		private void _Initialized(object sender, EventArgs e)
		{
			_UpdateRendererCache(VisualTreeHelper.GetDpi(this));

			if (_Columns == null)
			{
				_Columns = new TextColumn[] {
					new TextColumn(Scope.Offset, "Offset" , new Helper.OffsetFormatter(), HorizontalAlignment.Center),
					new TextColumn(Scope.Hex   , null     , new Helper.HexFormatter()),
					new TextColumn(Scope.Ascii , null     , new Helper.AsciiFormatter()),
					new TextColumn(Scope.Value , "Value"  , new Helper.ValueFormatter(), HorizontalAlignment.Left, 100),
					new TextColumn(Scope.Remark, "Remarks", new Helper.RemarkFormatter(), HorizontalAlignment.Left, 0, true),
				};
			}
			_UpdateColumns();

			_Manager = new RowManager(scroller);
			//_Manager.VisibleRows = (long)((ActualHeight - RenderHelper.HeaderHeight) / RenderHelper.RowHeight);
			//=> Will be set when OnRenderSizeChanged is being triggered

			_InitAnalyzerThread();
		}

		private void _Loaded(object sender, EventArgs e)
		{
		}

		private void scroller_Scroll(object sender, ScrollEventArgs e)
		{
			Debug.WriteLine("Scroll: " + e.NewValue);

			Offset = _Manager.MoveBy((int)e.NewValue);
		}

		/// <summary>
		/// Triggers re-rendering content
		/// </summary>
		private void _Render()
		{
			_RenderStopWatch = Stopwatch.StartNew();

			DrawingContext dc = _BackingStoreContent.Open();
			if (_Project != null)
				_Render(dc);
			dc.Close();

			_RenderStopWatch.Stop();
		}

		/// <summary>
		/// Actual rendering
		/// </summary>
		/// <param name="dc"></param>
		private void _Render(DrawingContext dc)
		{
			if (_Offset < 0)
				_Offset = 0;

			_RenderHeaderRow(dc);

			Point pt = new Point(Margin.Left, Margin.Top + RenderHelper.HeaderHeight);
			Point pt0 = new Point(Margin.Left, 0);
			Point pt1 = new Point(ActualWidth - Margin.Right, 0);

			int row_index = _Manager.FirstVisibleIndex;
			while (row_index <= _Manager.LastVisibleIndex)
			{
				RowManager.Row row = _Manager[row_index];
				pt.Y = _RenderRow(dc, pt.Y, row);

				if (row.Last)
				{
					// Node separator
					pt0.Y = pt1.Y = pt.Y - 0.5;
					dc.DrawLine(RenderHelper.FramePen, pt0, pt1);
				}

				row_index++;
			}

			// Let analyzer know some aspect may have changed
			if (_IsAnalyzerActive)
				_StartAnalyzer();
		}

		private void _RenderHeaderRow(DrawingContext dc)
		{
			Point margin = new Point(Margin.Left, Margin.Top);
			Size dims = new Size(ActualWidth - Margin.Left - Margin.Right, 
								 ActualHeight - Margin.Top - Margin.Bottom);

			foreach (TextColumn column in _Columns)
				RenderHelper.RenderHeaderCell(dc, column, margin, dims);
		}

		private double _RenderRow(DrawingContext dc, double y, RowManager.Row row)
		{
			Rect rect = new Rect(Margin.Left, y, 0, RenderHelper.RowHeight);
			double w;

			foreach (TextColumn column in _Columns)
			{
				rect.X += TextColumn.Margin;

				// Protect against underflows with stretchable column moved out of view
				w = column.Stretch ? ActualWidth - Margin.Right - TextColumn.Margin - rect.X : column.ContentWidth;
				if (w < 0)
					break;
				rect.Width = w;

				RenderHelper.RenderText(dc, column.Formatter.Format(row.Offset, row.Node), rect, column.Formatter.Color(row.Offset, row.Node),
					HorizontalAlignment.Left, VerticalAlignment.Center, rect.Width, rect.Height);

				rect.X += rect.Width + TextColumn.Margin;
			}

			return y + RenderHelper.RowHeight;
		}


		private void _ShowCaret()
		{
			// Save current state in case we've to pick up deletion
			bool? lastCaretState = _IsCaretVisible;

			// Kill current timer
			_IsCaretVisible = null;
			if (_CaretAnimTimer != null)
				_CaretAnimTimer.Dispose();

			// Setup new position
			Point pos = _OffsetToPixels(_MouseLeftPos, _MouseLeftScope);
			if (pos != _CaretPos)
			{
				// Trigger changed event
				Selection = 0;
			}
			_CaretPos = pos;

			// Pickup previous state or start new
			_IsCaretVisible = lastCaretState.GetValueOrDefault(false);

			// Re-start timer
			_CaretAnimTimer = new Timer(_AnimateCaret, null, 0, 400);

			//ProjectNode node = null;
			//if (_Project != null)
			//	node = _Project._FindNodeByOffset(_Offset + (_MouseLeftPos.Row * Settings.BYTES_PER_ROW));
			//if (node != _SelectedNode)
			//	Selection = node;
		}

		private void _HideCaret()
		{
			// Clear visible caret before killing timer
			if (_IsCaretVisible == true)
				_AnimateCaret();

			_IsCaretVisible = null;
			if (_CaretAnimTimer != null)
				_CaretAnimTimer.Dispose();
			_CaretAnimTimer = null;
		}

		private void _AnimateCaret(object obj = null)
		{
			if (_IsCaretVisible == null)
				return;

			if (Dispatcher.IsInvokeRequired())
			{
				// In case timer was ended in between
				// (this saves us from doing proper synchronsation :angel:)
				try
				{
					Dispatcher.Invoke(() => { _AnimateCaret(obj); });
				}
				catch
				{ }
			}
			else
			{
				if (_IsCaretVisible == null)
					return;

				_IsCaretVisible = !_IsCaretVisible;
				Pen pen;
				Point pt0;
				if (_IsCaretVisible == true)
				{
					_CaretPosLast = _CaretPos;
					pen = _CaretPenShow;
					pt0 = _CaretPos;
				}
				else
				{
					pen = _CaretPenErase;
					pt0 = _CaretPosLast;
				}
				Point pt1 = new Point(pt0.X, pt0.Y + RenderHelper.RowHeight);

				DrawingContext dc = _BackingStoreCaret.Open();
				dc.DrawLine(pen, pt0, pt1);
				dc.Close();
			}
		}


		private void _UpdateRowFeedback(Point pos)
		{
			if (pos.Y <= RenderHelper.HeaderHeight)
				return;

			Position chars;
			_PixelsToChars(pos, out chars);

			Point start = new Point(0, RenderHelper.HeaderHeight + (chars.Row * RenderHelper.RowHeight));
			Point end   = new Point(ActualWidth, start.Y + RenderHelper.RowHeight);

			_RowHoverRect = ShapeProvider.CreateRect(start, end, _HoverRectBrush);

			DrawingContext dc = _BackingStoreHovering.Open();
			dc.DrawDrawing(_RowHoverRect);
			dc.Close();
		}

		private void _HideRowFeedback()
		{
			_RowHoverRect = null;
			_BackingStoreHovering.Children.Clear();
		}


		private void _ShowSelection()
		{
			if (_MouseLeftPos == _SelectionStart || _SelectionStart < 0)
				return;

			long start = _SelectionStart;
			long end   = _MouseLeftPos;
			if (start > end)
			{
				long swap = start;
				start = end;
				end   = swap;
			}

			Point start1 = _OffsetToPixels(start, _MouseLeftScope);
			Point end1   = _OffsetToPixels(end  , _MouseLeftScope, false);
			end1.Y += RenderHelper.RowHeight;

			Scope opposite = (_MouseLeftScope == Scope.Hex) ? Scope.Ascii : Scope.Hex;
			Point start2 = _OffsetToPixels(start, opposite);
			Point end2   = _OffsetToPixels(end  , opposite, false);
			end2.Y += RenderHelper.RowHeight;

			Point start3 = new Point(TextColumn.Margin / 2, start1.Y);
			Point end3   = new Point(_Columns[0].ActualWidth - TextColumn.Margin / 2, end1.Y);

			Drawing selection1, selection2;
			if (start1.Y == end1.Y - RenderHelper.RowHeight)
			{
				// Selected target first
				selection1 = ShapeProvider.CreateRect(start1, end1, Brushes.Aqua);
				// Opposite target next
				selection2 = ShapeProvider.CreateRect(start2, end2, Brushes.AliceBlue);
			}
			else
			{
				Func<TextColumn, Point, Point, Brush, Drawing> create = (col, a, b, brush) => {
					double min_x = col.Left + TextColumn.Margin;
					double max_x = min_x + col.ContentWidth;
					return ShapeProvider.CreatePoly(a, b, min_x, max_x, brush);
				};

				// Selected target first
				selection1 = create(_GetColumnForTarget(_MouseLeftScope), start1, end1, Brushes.Aqua);
				// Opposite target next
				selection2 = create(_GetColumnForTarget(opposite), start2, end2, Brushes.AliceBlue);
			}

			// Offset last (same for all)
			Drawing offset_sel = ShapeProvider.CreateRect(start3, end3, Brushes.AliceBlue);

			DrawingContext dc = _BackingStoreSelection.Open();
			dc.DrawDrawing(selection1);
			dc.DrawDrawing(selection2);
			dc.DrawDrawing(offset_sel);
			dc.Close();

			Selection = 0;
		}

		private void _ClearSelection()
		{
			_SelectionStart = -1;
			_BackingStoreSelection.Children.Clear();

			Selection = 0;
		}


		private void _ShowRemarkTextbox()
		{
			_CloseRemarkTextbox();

			TextColumn column = _GetColumnForTarget(Scope.Remark);
			if (column == null)
				return;

			long offset = _MouseLeftPos;
			ProjectNode node = _Project.FindNodeByOffset(offset);
			if (node == null)
				return;

			long row = _Manager.FindRowIndexByOffset(node.Offset) - _Manager.FirstVisibleIndex;
			if (row < 0)
				return;

			_RemarkTextBox = new TextBox() {
				Width = ActualWidth - column.Left - (2 * TextColumn.Margin),
				Height = RenderHelper.RowHeight + 2,
				Background = Brushes.Snow,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(column.Left + (TextColumn.Margin / 2), RenderHelper.HeaderHeight + (row * RenderHelper.RowHeight) - 3, 0, 0),
				MaxLines = 1,
				Tag = node,
				Text = node.Remark,
			};

			_RemarkTextBox.LostFocus += (sender,args) => {
				_CloseRemarkTextbox();
				_Render();
			};
			_RemarkTextBox.KeyDown += (sender,args) => {
				if (args.Key == Key.Enter || args.Key == Key.Escape)
				{
					args.Handled = true;
					_CloseRemarkTextbox();
					_Render();
				}
			};

			grid.Children.Add(_RemarkTextBox);
			_RemarkTextBox.Focus();
		}

		private void _CloseRemarkTextbox()
		{
			_SaveRemarkTextbox();

			if (_RemarkTextBox != null)
				grid.Children.Remove(_RemarkTextBox);

			_RemarkTextBox = null;
		}

		private void _UpdateRemarkTextbox()
		{
			if (_RemarkTextBox == null)
				return;

			TextColumn column = _GetColumnForTarget(Scope.Remark);
			if (column == null)
				return;

			ProjectNode node = _RemarkTextBox.Tag as ProjectNode;
			if (node == null)
				return;

			long row = _Manager.FindRowIndexByOffset(node.Offset);
			if (row < 0)
				return;

			Thickness th = _RemarkTextBox.Margin;
			th.Left = column.Left + TextColumn.Margin;
			th.Top  = RenderHelper.HeaderHeight + (row * RenderHelper.RowHeight) - 2;
		}

		private void _SaveRemarkTextbox()
		{
			if (_RemarkTextBox == null)
				return;

			ProjectNode node = _RemarkTextBox.Tag as ProjectNode;
			node.Remark = _RemarkTextBox.Text;
		}


		private void _InitAnalyzerThread()
		{
			if (_AnalyzerThread != null)
			{
				Debug.Fail("Analyzer thread created already!");
				return;
			}

			_AnalyzerThread = new Thread(_AnalyzerThreadFunc);
			_AnalyzerThread.Name = "AnalyzerThread";
			_AnalyzerThread.IsBackground = true;

			_AnalyzerWakeup = new ManualResetEvent(false);
			_AnalyzerCancelWork = new ManualResetEvent(false);
			_AnalyzerThreadStopped = false;

			//_AnalyzerThread.Start();
			//=> Delayed until first use
		}

		private void _ShutdownAnalyzerThread()
		{
			if (_AnalyzerThread == null)
			{
				Debug.Fail("Analyzer destroyed already!");
				return;
			}

			_IsAnalyzerActive = false;

			if (_AnalyzerThread.IsAlive)
			{
				Debug.WriteLine("Stopping Analyzer thread");

				_AnalyzerThreadStopped = true;
				//if (force)
				//	_AnalyzerThread.Abort();
				//_AnalyzerThread.Interrupt();
				_AnalyzerCancelWork.Set();
				_AnalyzerWakeup.Set();

				//while (_AnalyzerThread.ThreadState == System.Threading.ThreadState.Running)
				while (_AnalyzerThread.IsAlive)
					Thread.Yield();

				Debug.WriteLine("Analyzer thread stopped");
			}

			_AnalyzerThread = null;

			if (_AnalyzerWakeup != null)
				_AnalyzerWakeup.Dispose();
			_AnalyzerWakeup = null;

			if (_AnalyzerCancelWork != null)
				_AnalyzerCancelWork.Dispose();
			_AnalyzerCancelWork = null;
		}

		private void _StartAnalyzer()
		{
			if (_AnalyzerThread == null)
			{
				Debug.Fail("Analyzer thread wasn't created!");
				return;
			}

			// Late starting
			if (!_AnalyzerThread.IsAlive)
				_AnalyzerThread.Start();

			_AnalyzerWakeup.Set();
		}

		private void _StopAnalyzer(bool force = false)
		{
			if (_AnalyzerThread.IsAlive)
				_AnalyzerCancelWork.Set();

			_AnalyzerStopWatch = null;
		}

		private void _AnalyzerThreadFunc()
		{
			Debug.WriteLine("!! Started");

			long first_vis = -1;

			List<Drawing> findings = new List<Drawing>();
			Analyzers.Finding finding = null;

			TextColumn hex = _GetColumnForTarget(Scope.Hex);
			TextColumn ascii = _GetColumnForTarget(Scope.Ascii);
			Action<Analyzers.Finding, Brush> inject = (find, brush) => {
				long ofs = find.NodeOffset + find.DataOffset;

				long row = _Manager.FindRowIndexByOffset(ofs, false) - first_vis;
				long col = ofs % Settings.BYTES_PER_ROW;
				Position start = new Position((int)row, (int)col);

				row = _Manager.FindRowIndexByOffset(ofs + find.DataLength - 1, false) - first_vis;
				col = (ofs + find.DataLength - 1) % Settings.BYTES_PER_ROW;
				Position end = new Position((int)row, (int)col);

				Debug.WriteLine("!! Finding {0}, ofs:{1:X8}+{2:X8}, len:{3} -> {4} - {5}",
					find.Type, find.NodeOffset, find.DataOffset, find.DataLength, start, end);

				Point hex_pt1 = hex.CharsToPixels(start);
				Point hex_pt2 = hex.CharsToPixels(end, false);
				hex_pt2.Y += RenderHelper.RowHeight;

				Point ascii_pt1 = ascii.CharsToPixels(start);
				Point ascii_pt2 = ascii.CharsToPixels(end, false);
				ascii_pt2.Y += RenderHelper.RowHeight;

				if (start.Row == end.Row)
				{
					findings.Add(ShapeProvider.CreateRect(hex_pt1, hex_pt2, brush));
					findings.Add(ShapeProvider.CreateRect(ascii_pt1, ascii_pt2, brush));
				}
				else
				{
					Func<TextColumn, Point, Point, Drawing> create = (column, a, b) => {
						double min_x = column.Left + TextColumn.Margin;
						double max_x = min_x + column.ContentWidth;
						return ShapeProvider.CreatePoly(a, b, min_x, max_x, brush);
					};

					findings.Add(create(hex, hex_pt1, hex_pt2));
					findings.Add(create(ascii, ascii_pt1, ascii_pt2));
				}
			};


			while (!_AnalyzerThreadStopped)
			{
				if (_AnalyzerStopWatch != null)
					_AnalyzerStopWatch.Stop();

				// Wait for activation
				Debug.WriteLine("!! Sleeping");
				_AnalyzerWakeup.WaitOne();
				_AnalyzerWakeup.Reset();
				Debug.WriteLine("!! Woke up");

				_AnalyzerStopWatch = Stopwatch.StartNew();

				if (_AnalyzerThreadStopped)
					break;
				if (_Project == null)
					continue;// Cease work for now

				ProjectNode node = _Manager.FirstVisibleRow.Node;
				if (node == null)
					continue;// Cease work for now
				first_vis = _Manager.FirstVisibleIndex;
				long start_offset = _Manager.FirstVisibleRow.Offset;
				long end_offset = _Manager.LastVisibleRow.Offset + _Manager.LastVisibleRow.Length - 1;
				IReadOnlyList<byte> data = node.Data;

				findings.Clear();

				long offset = start_offset;

				// Skip any typed node
				if (node.Type != null)
					offset = node.Offset + node.Length;

				while (offset <= end_offset)
				{
					if (_Project == null || _AnalyzerThreadStopped || _AnalyzerCancelWork.WaitOne(0))
						break;

					if (offset >= node.Offset + node.Length)
					{
						// Node exhausted, move to next node
						if (_Project == null)
							break;
						node = _Project.FindNodeByOffset(offset);
						if (node == null)
							break;

						// Skip any typed node
						if (node.Type != null)
						{
							offset = node.Offset + node.Length;
							continue;
						}

						data = node.Data;
					}

					if (_Project == null || _AnalyzerThreadStopped || _AnalyzerCancelWork.WaitOne(0))
						break;

					// Filesize checking
					finding = Analyzers.CheckFilesizeCandidate(data, offset - node.Offset, _Project.Filesize);
					if (finding != null)
					{
						finding.NodeOffset = node.Offset;
						inject(finding, Brushes.Azure);
					}
					if (_AnalyzerThreadStopped || _AnalyzerCancelWork.WaitOne(0))
						break;

					// String checking
					finding = Analyzers.CheckStringCandidate(data, offset - node.Offset);
					if (finding != null)
					{
						finding.NodeOffset = node.Offset;
						inject(finding, Brushes.Beige);
					}
					if (_Project == null || _AnalyzerThreadStopped || _AnalyzerCancelWork.WaitOne(0))
						break;

					offset++;
					if (offset % Settings.BYTES_PER_ROW == 0)
						Thread.Yield();
				}

				if (_Project == null || _AnalyzerThreadStopped)
					break;

				if (!_AnalyzerCancelWork.WaitOne(0))
				{
					Dispatcher.Invoke(() => {
						DrawingContext dc = _BackingStoreAnalyzer.Open();
						foreach (Drawing d in findings)
						{
							if (_Project == null || _AnalyzerThreadStopped || _AnalyzerCancelWork.WaitOne(0))
								break;
							dc.DrawDrawing(d);
						}
						dc.Close();
					});
				}

				if (_AnalyzerCancelWork.WaitOne(0))
				{
					//Dispatcher.Invoke(() => _BackingStoreAnalyzer.Children.Clear());
					_AnalyzerCancelWork.Reset();
				}
			}

			if (_AnalyzerStopWatch != null)
				_AnalyzerStopWatch.Stop();

			// End of thread
			Debug.WriteLine("!! Ended");
		}


		private Scope _PixelsToChars(Point pixels, out Position chars)
		{
			return _PixelsToChars(pixels, true, out chars);
		}

		private Scope _PixelsToChars(Point pixels, bool as_start, out Position chars)
		{
			double x = 0;
			foreach (TextColumn column in _Columns)
			{
				if ((!column.Stretch && x <= pixels.X && pixels.X <= x + column.ActualWidth)
				 || ( column.Stretch && x <= pixels.X && pixels.X <= ActualWidth))
				{
					chars = column.PixelsToChars(pixels, as_start);
					return column.Type;
				}

				x += column.ActualWidth;
			}

			chars = new Position(0, 0);
			return Scope.None;
		}

		private Point _CharsToPixels(Position chars, Scope target, bool as_start = true)
		{
			TextColumn column = _GetColumnForTarget(target);
			if (column == null)
				throw new ArgumentException(string.Format("No column for target '{0}'!", target));
			if (target != column.Type)
				throw new ArgumentException(string.Format("Wrong column for target '{0}'!", target));

			return column.CharsToPixels(chars, as_start);
		}

		private Point _OffsetToPixels(long offset, Scope target, bool as_start = true)
		{
			Position pos = _Manager.OffsetToChars(offset, as_start);
			return _CharsToPixels(pos, target, as_start);
		}


		private TextColumn _GetColumn(Point pixels)
		{
			return _Columns.FirstOrDefault(c => {
				double right = c.Stretch ? ActualWidth : c.Right;
				return (c.Left <= pixels.X && pixels.X <= right);
			});
		}

		private TextColumn _GetColumnForTarget(Scope target)
		{
			return _Columns.FirstOrDefault(c => c.Type == target);
		}


		private void _UpdateRendererCache(DpiScale dpiscale)
		{
			RenderHelper.UpdateCache(dpiscale);

			//if (_Manager != null && ActualHeight > 0)
			//	_Manager.VisibleRows = (long)((ActualHeight - RenderHelper.HeaderHeight) / RenderHelper.RowHeight);
		}

		private void _UpdateColumns()
		{
			double x = 0;
			foreach (TextColumn col in _Columns)
				x += col.Update(x);

			if (_IsCaretVisible != null)
				_CaretPos = _OffsetToPixels(_MouseLeftPos, _MouseLeftScope);
		}


		private ProjectFile _Project;
		private long _Offset;
		private RowManager _Manager;

		private DrawingGroup _BackingStoreContent = new DrawingGroup();
		private Stopwatch _RenderStopWatch;

		private long _MouseLeftPos;
		private Scope _MouseLeftScope;

		private DrawingGroup _BackingStoreSelection = new DrawingGroup();
		private long _SelectionStart;

		private DrawingGroup _BackingStoreHovering = new DrawingGroup();
		private Drawing _RowHoverRect;
		private static Color _HoverRectColor;
		private static Brush _HoverRectBrush;

		private static TextColumn[] _Columns;
		private TextColumn _ActiveColumn;

		private DrawingGroup _BackingStoreCaret = new DrawingGroup();
		private static Pen _CaretPenShow;
		private static Pen _CaretPenErase;
		private bool? _IsCaretVisible;
		private Point _CaretPos;
		private Point _CaretPosLast;
		private Timer _CaretAnimTimer;

		private TextBox _RemarkTextBox;

		private DrawingGroup _BackingStoreAnalyzer = new DrawingGroup();
		private bool _IsAnalyzerActive;
		private Thread _AnalyzerThread;
		private volatile bool _AnalyzerThreadStopped = false;
		private ManualResetEvent _AnalyzerWakeup;
		private ManualResetEvent _AnalyzerCancelWork;
		private Stopwatch _AnalyzerStopWatch;

	}


	internal class TextColumn
	{
		internal Scope Type;
		internal string Title;
		internal double Width;
		internal double ActualWidth;
		internal Helper.ITextFormatter Formatter;
		internal HorizontalAlignment Alignment;
		internal bool Stretch;
		internal double Left;
		internal const int Margin = 10;
		internal const int HeaderMargin = 2;
		internal const int MinWidth = (2 * Margin) + 10;

		internal double ContentWidth { get { return ActualWidth - (2 * Margin); } }
		internal double Right { get { return Left + ActualWidth; } }


		internal TextColumn(Scope type, string title, Helper.ITextFormatter formatter, 
			HorizontalAlignment alignment = HorizontalAlignment.Left, double width = 0, bool stretch = false)
		{
			Type = type;
			Title = title;
			Width = width;
			Formatter = formatter;
			Alignment = alignment;
			Stretch = stretch;
			
			Update(0);

			if (Title == null)
			{
				bool hex = (Type == Scope.Hex);
				StringBuilder sb = new StringBuilder(Settings.CHARS_PER_ROW);
				for (int i = 0; i < Settings.BYTES_PER_ROW; )
				{
					sb.Append(i.ToString(hex ? "X2" : "X1"));

					++i;
					if (hex)
					{
						if (i > 0 && i % 4 == 0)
						{
							if (i < Settings.BYTES_PER_ROW)
								sb.Append(Settings.COL_SPACER);
						}
						else
							sb.Append(' ');
					}
				}
				Title = sb.ToString();
			}
		}

		internal double Update(double x)
		{
			if (Width == 0)
			{
				// Auto-size only non-fixed columns
				ActualWidth = RenderHelper.MaxWidth(Type) + (Margin * 2);
			}
			else
			{
				if (Width < MinWidth)
					Width = MinWidth;
				ActualWidth = Width;
				Stretch = false;
			}

			Left = x;

			return ActualWidth;
		}

		internal Position PixelsToChars(Point pixels, bool as_start = true)
		{
			Point pt = new Point(pixels.X - Left - Margin, pixels.Y);
			return RenderHelper.PixelsToChars(pt, Type, as_start);
		}

		internal Point CharsToPixels(Position chars, bool as_start = true)
		{
			Point pt = RenderHelper.CharsToPixels(chars, Type, as_start);
			pt.X += Left + Margin;
			return pt;
		}
	}


	/// <summary>
	/// Row manager will not only deal with navigation (which includes taking
	/// full control on scroll bar), but will also be used for rendering with
	/// feeding all info required to render a row of data.
	/// </summary>
	internal class RowManager
	{
		internal int TotalRows
		{
			get
			{
				return (_Rows != null) ? _Rows.Count : 0;
			}
		}

		internal int VisibleRows
		{
			get { return _VisibleRows; }
			set
			{
				_VisibleRows = value;

				_Scrollbar.Maximum = TotalRows - VisibleRows;
				_Scrollbar.ViewportSize = VisibleRows;

				FirstVisibleIndex = TotalRows - _VisibleRows;
			}
		}

		internal int FirstVisibleIndex
		{
			get { return _FirstVisible; }
			set
			{
				_FirstVisible = value;
				if (_FirstVisible < 0)
					_FirstVisible = 0;
				else if (_FirstVisible > TotalRows - VisibleRows)
					_FirstVisible = TotalRows - VisibleRows;

				_Scrollbar.Value = _FirstVisible;
			}
		}

		internal Row FirstVisibleRow
		{
			get
			{
				if (_FirstVisible < 0 || _FirstVisible >= TotalRows)
					return null;
				return this[_FirstVisible];
			}
		}

		internal int LastVisibleIndex
		{
			get
			{
				if (_FirstVisible < 0)
					return -1;
				int last = _FirstVisible + VisibleRows - 1;
				if (last >= TotalRows)
					last = TotalRows - 1;
				return last;
			}
		}

		internal Row LastVisibleRow
		{
			get
			{
				int last = LastVisibleIndex;
				if (last < 0)
					return null;
				return this[last];
			}
		}

		internal Row this[int row]
		{
			get
			{
				if (row < 0 || row >= TotalRows)
					return null;
				return _Rows[row];
			}
		}

		internal long Offset(int row)
		{
			Row r = this[_FirstVisible + row];
			return (r != null) ? r.Offset : -1;
		}


		internal RowManager(ScrollBar scrollbar)
		{
			_Scrollbar = scrollbar;
		}


		internal void Update(ProjectNodes nodes)
		{
			_Rows = new List<Row>();
			_FirstVisible = -1;

			if (nodes == null)
			{
				_Scrollbar.IsEnabled = false;
				_Scrollbar.Maximum = -1;
				return;
			}

			long offset, remain, column, length;
			foreach (ProjectNode node in nodes)
			{
				offset = node.Offset;
				remain = node.Length;
				column = offset % Settings.BYTES_PER_ROW;
				length = Settings.BYTES_PER_ROW - column;

				while (remain > 0)
				{
					if (length > remain)
						length = remain;

					_Rows.Add(new Row(node, offset, (int)length, (length == remain)));

					offset += length;
					remain -= length;
					column = 0;
					length = Settings.BYTES_PER_ROW;
				}
			}

			_FirstVisible = 0;

			_Scrollbar.Maximum = TotalRows - VisibleRows;
			_Scrollbar.ViewportSize = VisibleRows;
			_Scrollbar.IsEnabled = true;
		}

		internal void Add(ProjectNode removed, ProjectNode[] added)
		{
			// First up, find index where we will apply our changes
			Row row = _Rows.FirstOrDefault(n => n.Node == removed);
			if (row == null)
				throw new Exception("CORRUPT ROW CACHE! (node not found)");
			int index = _Rows.IndexOf(row);

			// Remove any reference to old node
			_Rows.RemoveAll(n => n.Node == removed);

			// Add any new node, starting at index discovered earlier
			long offset, remain, column, length;
			foreach (ProjectNode node in added)
			{
				offset = node.Offset;
				remain = node.Length;
				column = offset % Settings.BYTES_PER_ROW;
				length = Settings.BYTES_PER_ROW - column;

				while (remain > 0)
				{
					if (length > remain)
						length = remain;

					_Rows.Insert(index, new Row(node, offset, (int)length, (length == remain)));
					index++;

					offset += length;
					remain -= length;
					column = 0;
					length = Settings.BYTES_PER_ROW;
				}
			}

			_Scrollbar.Maximum = TotalRows - VisibleRows;
		}

		internal void Remove(ProjectNode[] removed, ProjectNode added)
		{
			// First up, find index where we will apply our changes
			Row row = _Rows.FirstOrDefault(n => n.Node == removed[0]);
			if (row == null)
				throw new Exception("CORRUPT ROW CACHE! (node not found)");
			int index = _Rows.IndexOf(row);

			// Remove any reference to old nodes
			foreach (ProjectNode node in removed)
				_Rows.RemoveAll(n => n.Node == node);

			// Add any new node, starting at index discovered earlier
			long offset = added.Offset;
			long remain = added.Length;
			long column = offset % Settings.BYTES_PER_ROW;
			long length = Settings.BYTES_PER_ROW - column;

			while (remain > 0)
			{
				if (length > remain)
					length = remain;

				_Rows.Insert(index, new Row(added, offset, (int)length, (length == remain)));
				index++;

				offset += length;
				remain -= length;
				column = 0;
				length = Settings.BYTES_PER_ROW;
			}

			_Scrollbar.Maximum = TotalRows - VisibleRows;
		}

		internal void MoveToOffset(long offset)
		{
			int row = FindRowIndexByOffset(offset, true);
			if (row == -1)
				row = FindRowIndexByOffset(offset, false);
			FirstVisibleIndex = row;
		}

		internal long MoveBy(int row_delta)
		{
			int row = _FirstVisible + row_delta;

			if (row < 0)
				row = 0;
			else if (row > (TotalRows - VisibleRows))
				row = TotalRows - VisibleRows;

			FirstVisibleIndex = row;

			return FirstVisibleRow.Offset;
		}


		internal int FindRowIndexByOffset(long offset, bool exact = true)
		{
			int row = 0;

			foreach (Row info in _Rows)
			{
				if (exact && offset == info.Offset)
					break;
				if (!exact && info.Offset <= offset && offset <= info.Offset + info.Length - 1)
					break;
				row++; 
			}

			if (row == TotalRows)
				row = -1;

			return row;
		}

		internal Row FindRowByOffset(long offset, bool exact = true)
		{
			return this[FindRowIndexByOffset(offset, exact)];
		}


		internal Position OffsetToChars(long offset, bool as_start = true)
		{
			int row_index = FindRowIndexByOffset(offset, false);
			//if (row_index < _FirstVisible)
			if (row_index < 0)
					return null;

			Row row = this[row_index];
			if (row == null)
				return null;

			//int col = (int)(offset - row.Offset);
			int col = (int)(offset % Settings.BYTES_PER_ROW);
			if (!as_start)
			{
				col--;
				if (col < 0)
				{
					row_index--;
					row = this[row_index];
					if (row == null)
						return null;
				}
			}
			if (col < row.StartCol)
				col = row.StartCol;
			else if (col > row.EndCol)
				col = row.EndCol;

			return new Position(row_index - _FirstVisible, col);
		}

		internal long CharsToOffset(Position chars, bool as_start = true)
		{
			long offset = -1;
			if (chars != null)
			{
				offset = Offset(chars.Row);
				if (offset >= 0 && chars.Col >= 0)
				{
					// Incorporate column into offset
					offset &= Settings.OFFSET_MASK;
					offset += chars.Col;
				}
				if (!as_start)
					offset--;
			}
			return offset;
		}


		internal class Row
		{
			internal ProjectNode Node { get; private set; }

			internal long Offset { get; private set; }

			internal int Length { get; private set; }

			internal bool Last { get; private set; }

			internal int StartCol { get { return (int)(Offset % Settings.BYTES_PER_ROW); } }

			internal int EndCol { get { return (int)(StartCol + Length - 1); } }


			internal Row(ProjectNode node, long offset, int length, bool last)
			{
				Node = node;
				Offset = offset;
				Length = length;
				Last = last;
			}


			public override string ToString()
			{
				return string.Format("[Row.Info] {0}-{1} | {2}", 
					Offset.ToOffsetString(), (Offset+Length-1).ToOffsetString(), Node.ToString());
			}

			// Estimated memory size for storing a single column
			// (3 longs, a bool and a reference assumed to be a 64bit, w/o any alignment)
			internal static long SizeOf = (8 * 3) + 1 + 8;
		}


		private ScrollBar _Scrollbar;
		private List<Row> _Rows;
		private int _FirstVisible;
		private int _VisibleRows;

	}


	/// <summary>
	/// Helper for creating geometries
	/// </summary>
	internal static class ShapeProvider
	{

		/*
		 * Positions are treated as:
		 * 
		 *		R  ,C  | |R  ,C+1
		 *		-------+-+-------
		 *		       |X|
		 *		-------+-+-------
		 *		R+1,C  | |R+1,C+1
		 * 
		 * with X = digit to mark, so whole plot is actually a 
		 * 
		 *		Lines+1, BYTES_PER_ROW+1
		 */

		internal static Drawing CreateRect(Point start, Point end, Brush fill, Brush border = null, double thickness = 0)
		{
			var rectangle = new RectangleGeometry(new Rect(start, end));
			rectangle.Freeze();

			Pen pen = null;
			if (border != null)
			{
				pen = new Pen(border, thickness);
				pen.Freeze();
			}

			Drawing drawing = new GeometryDrawing(fill, pen, rectangle);
			drawing.Freeze();

			return drawing;
		}

		internal static Drawing CreatePoly(Point start, Point end, double min_bounds, double max_bounds,
										   Brush fill, Brush border = null, double thickness = 0)
		{
			double start_y = start.Y + 0.5;
			double end_y   = end.Y   - 0.5;

			// Start at top-left corner, traversing clock-wise
			List<Point> points = new List<Point>();
			points.Clear();
			points.Add(new Point(start.X   , start_y));
			points.Add(new Point(max_bounds, start_y));
			points.Add(new Point(max_bounds, end_y - RenderHelper.RowHeight));
			points.Add(new Point(end.X     , end_y - RenderHelper.RowHeight));
			points.Add(new Point(end.X     , end_y));
			points.Add(new Point(min_bounds, end_y));
			points.Add(new Point(min_bounds, start_y + RenderHelper.RowHeight));
			points.Add(new Point(start.X   , start_y + RenderHelper.RowHeight));

			return CreatePoly(points, fill, border, thickness);
		}

		internal static Drawing CreatePoly(IEnumerable<Point> points, Brush fill, Brush border = null, double thickness = 0)
		{
			List<Point> new_points = points.ToList();
			Point start = new_points[0];
			new_points.RemoveAt(0);
			PolyLineSegment lines = new PolyLineSegment(new_points, false);
			lines.Freeze();

			PathFigure figure = new PathFigure();
			figure.StartPoint = start;
			figure.Segments.Add(lines);
			figure.Freeze();

			PathGeometry path = new PathGeometry();
			path.Figures.Add(figure);
			path.FillRule = FillRule.EvenOdd;
			path.Freeze();

			Pen pen = null;
			if (border != null)
			{
				pen = new Pen(border, thickness);
				pen.Freeze();
			}

			Drawing drawing = new GeometryDrawing(fill, pen, path);
			drawing.Freeze();

			return drawing;
		}

	}


	internal static class Analyzers
	{
		internal class Finding
		{
			internal long NodeOffset;

			internal Type Type;
			internal long DataOffset;
			internal long DataLength;

			internal Finding(Type type, long offset, long length)
			{
				Type = type;
				DataOffset = offset;
				DataLength = length;
			}
		}


		internal static Finding CheckFilesizeCandidate(IReadOnlyList<byte> data, long offset, long filesize)
		{
			// Check most common type first: 32bit (u)int
			Type type;

			type = IsOfValue(data, offset, filesize, 4, 4, 0);
			if (type != null)
				return new Finding(type, offset, 4);

			type = IsOfValue(data, offset, filesize, 8, 8, 0);
			if (type != null)
				return new Finding(type, offset, 8);

			type = IsOfValue(data, offset, filesize, 2, 2, 0);
			if (type != null)
				return new Finding(type, offset, 2);

			return null;
		}

		internal static Finding CheckStringCandidate(IReadOnlyList<byte> data, long offset)
		{
			if (offset + 4 > data.Count)
				return null;

			int len = Helpers.ToInt32(data, (int)offset);
			if (len == 0)
				return null;

			long ofs = offset + 4;

			if (len < 0)
			{
				len = -len;
				return IsWideString(data, ofs, len)
					? new Finding(typeof(VarString), offset, (len * 2) + 4)
					: null
					;
			}
			if (IsAsciiString(data, ofs, len))
				return new Finding(typeof(AsciiString), offset, len + 4);

			if (IsWideString(data, ofs, len))
				return new Finding(typeof(WideString), offset, (len * 2) + 4);

			return null;
		}


		// More general tests following
		//

		/// <summary>
		/// Test data buffer against a specific value
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="value">Specific value to look for</param>
		/// <param name="minlen">Minimum length of check (default=1)</param>
		/// <param name="maxlen">Maximum length of check (default=8)</param>
		/// <param name="sign">Which type to check: -1:signed, 0:both, +1:unsigned</param>
		/// <returns>Type discovered, or null if no match found</returns>
		internal static Type IsOfValue(IReadOnlyList<byte> data, long offset, long value, int minlen = 1, int maxlen = 8, int sign = 0)
		{
			if (minlen <= 8 && maxlen >= 8 && offset + 8 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt64(data, (int)offset) == (ulong)value)
					return typeof(ulong);

				if (sign <= 0 && Helpers.ToInt64(data, (int)offset) == value)
					return typeof(long);
			}

			if (minlen <= 4 && maxlen >= 4 && value <= uint.MaxValue && offset + 4 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt32(data, (int)offset) == value)
					return typeof(uint);

				if (sign <= 0 && value <= int.MaxValue)
					if (Helpers.ToInt32(data, (int)offset) == value)
						return typeof(int);
			}

			if (minlen <= 2 && maxlen >= 2 && value < ushort.MaxValue && offset + 2 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToUInt16(data, (int)offset) == value)
					return typeof(ushort);

				if (sign <= 0 && value <= short.MaxValue)
					if (Helpers.ToInt16(data, (int)offset) == value)
						return typeof(short);
			}

			if (minlen <= 1 && maxlen >= 1 && value < byte.MaxValue && offset + 1 <= data.Count)
			{
				if (sign >= 0 && Helpers.ToByte(data, (int)offset) == value)
					return typeof(byte);

				if (sign <= 0 && value <= sbyte.MaxValue)
					if (Helpers.ToSByte(data, (int)offset) == value)
						return typeof(sbyte);
			}

			return null;
		}

		/// <summary>
		/// Check for valid 8bit string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		internal static bool IsAsciiString(IReadOnlyList<byte> data, long offset, long length)
		{
			if (offset + length > data.Count)
				return false;

			// Null-terminator present?
			if (data[(int)(offset + length - 1)] != 0)
				return false;

			for (long idx = offset; idx < offset + length - 1; ++idx)
			{
				byte b = data[(int)idx];

				if (b > 127)
					return false;
				if (b < 32 && !_ValidCtrlChars.Contains(b))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Check for valid 16bit string
		/// </summary>
		/// <param name="data">Data to check</param>
		/// <param name="offset">Where to start in data</param>
		/// <param name="length">Assumed length of string</param>
		/// <returns>Outcome</returns>
		internal static bool IsWideString(IReadOnlyList<byte> data, long offset, long length)
		{
			long len2 = length << 1;

			if (offset + len2 > data.Count)
				return false;

			// Null-terminator present?
			if (data[(int)(offset + len2 - 2)] != 0 || data[(int)(offset + len2 - 1)] != 0) 
				return false;

			for (long idx = offset; idx < offset + len2 - 2; idx += 2)
			{
				byte b1 = data[(int) idx     ];
				byte b2 = data[(int)(idx + 1)];

				if (b1 < 128)
				{
					if (b2 != 0)
						return false;
					if (b1 < 32 && !_ValidCtrlChars.Contains(b1))
						return false;
				}
				else if (0xC2 <= b1 && b1 <= 0xDF)
				{
					if (b2 < 0x80 || b2 > 0xBF)
						return false;
				}
			}

			return true;
		}

		private static byte[] _ValidCtrlChars = new byte[] { (byte)'\t', (byte)'\n', (byte)'\r' };

	}


	internal static class RenderHelper
	{
		internal static bool IsDirtyCache { get; set; }
		internal static double HeaderHeight { get; private set; }
		internal static double RowHeight { get; private set; }

		internal static Pen FramePen { get; private set; }


		internal static void RenderHeaderCell(DrawingContext dc, TextColumn column, Point margin, Size dimensions)
		{
			Rect rect = new Rect(margin.X + column.Left, margin.Y, 0, HeaderHeight - 0.5);

			// Protect against underflows with stretchable column moved out of view
			double w = column.Stretch ? dimensions.Width - column.Left : column.ActualWidth;
			if (w <= 0)
				return;
			rect.Width = w;

			dc.DrawRectangle(Brushes.LightGray, null/*_FramePen*/, rect);

			Point pt = rect.BottomRight;
			dc.DrawLine(FramePen, rect.BottomLeft, pt);
			pt.Y = dimensions.Height;
			dc.DrawLine(FramePen, rect.TopRight, pt);

			rect.Inflate(-TextColumn.Margin, -TextColumn.HeaderMargin);
			RenderText(dc, column.Title, rect, Brushes.Black, column.Alignment, VerticalAlignment.Center, rect.Width, _CharHeight);
		}

		internal static void RenderText(DrawingContext dc, string str, Rect rect, Brush color,
			HorizontalAlignment h_align, VerticalAlignment v_align = VerticalAlignment.Center, 
			double max_width = 0, double max_height = 0)
		{
			if (str == null)
				return;

			FormattedText text = new FormattedText(str, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, 
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, color/*Brushes.Black*/, null, TextFormattingMode.Display, 
				_DpiInfo.PixelsPerDip);

			// Protect against underflows with stretchable column moved out of view
			if (max_width > 0)
				text.MaxTextWidth = max_width;
			if (max_height > 0)
				text.MaxTextHeight = max_height;

			Point pt = new Point();

			switch (h_align)
			{
				case HorizontalAlignment.Left:		pt.X = 0; break;
				case HorizontalAlignment.Center:	pt.X = ((rect.Width - text.Width) / 2.0); break;
				case HorizontalAlignment.Right:		pt.X =   rect.Width - text.Width; break;
			}
			if (pt.X < 0)
				pt.X = 0;
			pt.X += rect.Left;

			switch (v_align)
			{
				case VerticalAlignment.Top:			pt.Y = 0; break;
				case VerticalAlignment.Center:		pt.Y = ((rect.Height - _CharHeight) / 2.0); break;
				case VerticalAlignment.Bottom:		pt.Y = rect.Width - text.Width; break;
			}
			if (pt.Y < 0)
				pt.Y = 0;
			pt.Y += rect.Top;

			dc.DrawText(text, pt);
		}


		/// <summary>
		/// Calculates character position
		/// </summary>
		/// <param name="pixels">Relative pixel position</param>
		/// <param name="source">Character scope for pixels passed in</param>
		/// <param name="as_start">True for treating as start pos, False for end pos</param>
		/// <returns>Character position</returns>
		internal static Position PixelsToChars(Point pixels, Scope source, bool as_start = true)
		{
			Position chars;

			if (source != Scope.None)
			{
				double x = pixels.X;
				double y = pixels.Y - HeaderHeight;

				// Hex target only: Slight adjustment towards next column
				double adjust = (source == Scope.Hex) ? 0.01 : 0.0;
				int col = (int)((x / _CharWidth) + adjust);
				int row = (int)(y / RowHeight);
				long offset;

				switch (source)
				{
					case Scope.Hex:
						offset = (row * Settings.CHARS_PER_ROW) + col;
						if (!as_start)
							offset++;
						chars = new Position(offset, true, source);
						break;

					case Scope.Ascii:
						offset = (row * Settings.BYTES_PER_ROW) + col;
						if (!as_start)
							offset++;
						chars = new Position(offset, true, source);
						break;

					case Scope.Remark:
						chars = new Position(row, -1);
						break;

					default:
						chars = new Position(row, col);
						break;
				}
			}
			else
			{
				chars = new Position(0, 0);
			}

			return chars;
		}

		/// <summary>
		/// Calculate raw pixel position
		/// (w/o margins nor column start!)
		/// </summary>
		/// <param name="chars">Character-based position to transform</param>
		/// <param name="target">Target scope for which to return pixels</param>
		/// <param name="as_start">True for treating as start pos, False for end pos</param>
		/// <returns>Raw pixel pos</returns>
		internal static Point CharsToPixels(Position chars, Scope target, bool as_start = true)
		{
			Point pt = new Point(0, HeaderHeight + (chars.Row * RowHeight));

			if (target == Scope.Hex)
			{
				long col = chars.Col / Settings.BYTES_PER_COL;
				long ofs = (((chars.Col % Settings.BYTES_PER_COL) * 3) - 1) 
							+ (col * (Settings.CHARS_PER_COL + Settings.COL_SEP_CHARS))
							+ 1
							;
				if (!as_start)
					ofs += 2;
				pt.X = ofs * _CharWidth;
			}
			else
			{
				long col = chars.Col;
				if (!as_start)
					col++;
				pt.X = col * _CharWidth;
			}

			return pt;
		}


		internal static double MaxWidth(Scope scope)
		{
			return _MaxWidth[(int)scope];
		}

		internal static void UpdateCache(DpiScale dpiscale)
		{
			if (!IsDirtyCache && _DpiInfo.Equals(dpiscale))
				return;

			_DpiInfo = dpiscale;

			StringBuilder sb = new StringBuilder();

			for (int i=0; i < Settings.BYTES_PER_COL; ++i)
			{
				if (sb.Length != 0)
					sb.Append(' ');
				sb.Append("XX");
			}
			string hex_block = sb.ToString();

			var formattedText = new FormattedText(hex_block, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			double block_width = formattedText.Width;
			formattedText = new FormattedText(hex_block + Settings.COL_SPACER, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_ColSepWidth = formattedText.Width - block_width;

			sb.Clear();
			for (int i=0; i < Settings.COLS_PER_ROW; ++i)
			{
				if (sb.Length != 0)
					sb.Append(Settings.COL_SPACER);
				sb.Append(hex_block);
			}
			string hex_line = sb.ToString();

			formattedText = new FormattedText(hex_line, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_MaxWidth[(int)Scope.Hex] = formattedText.Width;
			_CharWidth = formattedText.Width / Settings.CHARS_PER_ROW;
			double h = formattedText.Extent + (2 * formattedText.LineHeight);

			const int MAX_LINES = 10;
			for (int i = 0; i < MAX_LINES; ++i)
				sb.Append('\n').Append(hex_line);
			string hex_blob = sb.ToString();

			formattedText = new FormattedText(hex_blob, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_CharHeight = ((formattedText.Extent - h) / MAX_LINES) - (2 * formattedText.LineHeight);

			sb.Clear().Append('X', Settings.BYTES_PER_ROW);
			formattedText = new FormattedText(sb.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_MaxWidth[(int)Scope.Ascii] = formattedText.Width;

			formattedText = new FormattedText(0L.ToOffsetString(false), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_MaxWidth[(int)Scope.Offset] = formattedText.Width;

			formattedText = new FormattedText("Value", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_MaxWidth[(int)Scope.Value] = formattedText.Width;

			formattedText = new FormattedText("Remark", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
				Settings.FONT_TYPEFACE, Settings.FONT_SIZE, Brushes.Black, null, TextFormattingMode.Display, _DpiInfo.PixelsPerDip);
			_MaxWidth[(int)Scope.Remark] = formattedText.Width;


			RowHeight = Math.Ceiling(_CharHeight);
			RowHeight += RowHeight % 2;

			// Don't like referencing TextColumn here -.-
			HeaderHeight = RowHeight + (2 * TextColumn.HeaderMargin);


			IsDirtyCache = false;
		}


		static RenderHelper()
		{
			IsDirtyCache = true;

			FramePen = new Pen(Brushes.DarkGray, 1);
			FramePen.Freeze();

			_MaxWidth = new double[Enum.GetValues(typeof(Scope)).Length];
		}


		internal static DpiScale _DpiInfo;
		internal static double _CharWidth;
		internal static double _CharHeight;
		internal static double _ColSepWidth;
		internal static double[] _MaxWidth;

	}
}
