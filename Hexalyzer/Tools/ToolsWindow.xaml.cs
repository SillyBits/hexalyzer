using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace Hexalyzer.Tools
{
	/// <summary>
	/// Interaction logic for ToolsWindow.xaml
	/// </summary>
	public partial class ToolsWindow : UserControl
	{

		public static readonly DependencyProperty IconProperty =
			DependencyProperty.Register("Icon", typeof(ImageSource), typeof(ToolsWindow));
		public ImageSource Icon
		{
			get { return GetValue(IconProperty) as ImageSource; }
			set { SetValue(IconProperty, value); }
		}

		public static readonly DependencyProperty TitleProperty =
			DependencyProperty.Register("Title", typeof(string), typeof(ToolsWindow));
		public string Title
		{
			get { return GetValue(TitleProperty) as string; }
			set { SetValue(TitleProperty, value); }
		}

		public UIElement Child
		{
			set
			{
				Grid grid = Content as Grid;
				ContentControl ctrl = grid.Children[1] as ContentControl;
				ctrl.Content = value;
			}
		}

		public ToolsWindow()
		{
			InitializeComponent();
		}

	}
}
