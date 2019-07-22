using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace Hexalyzer.Tools
{

	public class ValuePreview : ToolsWindow
	{

		public ProjectFile Project
		{
			set
			{
				_Project = value;
				if (_Project == null)
					SetValue(NodeProperty, null);
			}
		}

		public long Offset;

		public static readonly DependencyProperty NodeProperty = 
			DependencyProperty.Register("Node", typeof(ProjectNode), typeof(ValuePreview));


		public ValuePreview(ProjectView view)
		{
			InitializeComponent();

			Icon = new BitmapImage(new Uri("pack://application:,,,/Resources/icons8/Toolbar.View.ValuePreview.png"));
			Title = "Value preview";

			_View = view;

			Loaded += _Loaded;
		}


		// Non-public implementation following
		//

		private void _Loaded(object sender, RoutedEventArgs e)
		{
			_Grid = new Grid() {
				Width = double.NaN,
				Height = double.NaN,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,				 
			};
			_Grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
			_Grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
	
			Child = _Grid;


			Action<UIElement,UIElement> addRow = (a, b) => {
				int row = _Grid.RowDefinitions.Count;
				_Grid.RowDefinitions.Add(new RowDefinition() {
					Height = GridLength.Auto,
				});

				Grid.SetRow(a, row);
				Grid.SetColumn(a, 0);
				Grid.SetColumnSpan(a, (b == null) ? 2 : 1);
				_Grid.Children.Add(a);

				if (b != null)
				{
					Grid.SetRow(b, row);
					Grid.SetColumn(b, 1);
					_Grid.Children.Add(b);
				}
			};

			Action addSep = () => {
				Rectangle r = new Rectangle() {
					Width = double.NaN,
					Height =1,
					Fill = Brushes.DarkGray,
					Stretch = Stretch.Fill,
				};
				addRow(r, null);
			};

			Action<string, IValueConverter> add = (label,conv) => {
				Label l = new Label() {
					Content = label + ":",
					Width = double.NaN,
					MinWidth = 50,
					Height = double.NaN,
				};

				TextBox v = new TextBox() {
					Width = double.NaN,
					MinWidth = 100,
					Height = double.NaN,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 4, 0),
					BorderBrush = Brushes.Transparent,
					BorderThickness = new Thickness(0),
					Background = Brushes.Transparent,
					IsReadOnly = true,
					Text = "n/a",
				};

				Binding val_binding = new Binding()
				{
					Source = this,
					Path = new PropertyPath("Node"),
					Mode = BindingMode.OneWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
					Converter = conv,
					ConverterParameter = this,
				};
				BindingOperations.SetBinding(v, TextBox.TextProperty, val_binding);

				addRow(l, v);
			};


			add("Boolean", new NodeValuePreview<bool>());

			addSep();

			add("Ascii", new NodeValuePreview<Datatypes.AsciiChar>());
			add("Wide", new NodeValuePreview<char>());

			addSep();

			add("Byte", new NodeValuePreview<byte>());
			add("SByte", new NodeValuePreview<sbyte>());
			add("Int16", new NodeValuePreview<short>());
			add("UInt16", new NodeValuePreview<ushort>());
			add("Int32", new NodeValuePreview<int>());
			add("UInt32", new NodeValuePreview<uint>());
			add("Int64", new NodeValuePreview<long>());
			add("UInt64", new NodeValuePreview<ulong>());
			add("Float", new NodeValuePreview<float>());
			add("Double", new NodeValuePreview<double>());

			addSep();

			add("AsciiString", new NodeValuePreview<Datatypes.AsciiString>());
			add("WideString", new NodeValuePreview<Datatypes.WideString>());
			add("VarString", new NodeValuePreview<Datatypes.VarString>());

			addSep();


			_View.SelectionChanged += _View_SelectionChanged;
			//_View.CurrentNodeChanged += _View_CurrentNodeChanged;
		}

		//private void _View_CurrentNodeChanged(object sender, ProjectNode node)
		//{
		//}

		private void _View_SelectionChanged(object sender, long from, long to)
		{
			if (_Project == null || from < 0)
				return;

			ProjectNode node = _Project.FindNodeByOffset(from);//_View.Selection as ProjectNode;
			if (node == null)
				return;

			from -= node.Offset;
			if (from < 0 || from >= node.Length)
				return;

			Offset = from;

			if (GetValue(NodeProperty) == node)
				ClearValue(NodeProperty);
			SetValue(NodeProperty, node);
		}


		private ProjectView _View;
		private ProjectFile _Project;
		private Grid _Grid;


		private class NodeValuePreview<_Type> : IValueConverter
		{
			public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value == null || parameter == null)
					return "-";

				ProjectNode node = value as ProjectNode;
				ValuePreview preview = parameter as ValuePreview;
				//long offset = preview.Offset;
				//IReadOnlyList<byte> data = node[0, node.Length];

				//if (offset < 0 || offset >= data.Count)
				//	return "-";

				try
				{
					return Datatypes.Helpers.ToString(typeof(_Type), node.Data, (int)preview.Offset);
				}
				catch
				{
					return "-";
				}
			}

			public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
			{
				throw new NotImplementedException();
			}
		}

	}

}
