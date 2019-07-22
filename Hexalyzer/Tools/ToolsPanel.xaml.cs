using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;


namespace Hexalyzer.Tools
{
	/// <summary>
	/// Interaction logic for ToolsPanel.xaml
	/// </summary>
	public partial class ToolsPanel : UserControl
	{

		public ToolsPanel()
		{
			InitializeComponent();
		}


		public void Add(UIElement tool)
		{

			if (panel.Children.Contains(tool))
			{
				tool.Visibility = Visibility.Visible;
				return;
			}

			foreach (UIElement element in panel.Children)
			{
				if (element.GetType() == tool.GetType())
				{
					element.Visibility = Visibility.Visible;
					return;
				}
			}

			panel.Children.Add(tool);
			tool.Visibility = Visibility.Visible;

			Visibility = Visibility.Visible;

			InvalidateVisual();
		}

		public void Remove(UIElement tool)
		{
			if (panel.Children.Contains(tool))
			{
				panel.Children.Remove(tool);
			}
			else
			{
				foreach (UIElement element in panel.Children)
				{
					if (element.GetType() == tool.GetType())
					{
						panel.Children.Remove(element);
						break;
					}
				}
			}

			if (panel.Children.Count == 0)
				Visibility = Visibility.Collapsed;

			InvalidateVisual();
		}

	}

}
