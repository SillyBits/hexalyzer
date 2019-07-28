using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;


namespace Hexalyzer.Plugin
{

	/// <summary>
	/// Mandatory interface to be implemented by each plugin
	/// </summary>
	public interface IPlugin
	{
		string Name { get; }
		ImageSource Icon { get; }

		Type[] GetSupportedDatatypes();
		Type[] GetSupportedPanels();
		Type[] GetSupportedTools();
		Type[] GetSupportedAnalyzers();

	}


	/// <summary>
	/// Interface used to specify new data types
	/// </summary>
	public interface IDatatype
	{
		string Name { get; }
		ImageSource Icon { get; }

		// Sadfully, no static methods allowed, so one has to use a fake instance to access those :(
		IDatatype Empty { get; }
		bool IsValid(IAccessor<byte> data, long offset);
		long LengthOf(IAccessor<byte> data, long offset);
		IDatatype FromData(IAccessor<byte> data, long offset);

	}


	/// <summary>
	/// Interfaces used to specify new panels
	/// </summary>
	public interface IPanel
	{
		string Name { get; }
		ImageSource Icon { get; }

	}


	/// <summary>
	/// Interface used to specify new tools
	/// </summary>
	public interface ITool
	{
		string Name { get; }
		ImageSource Icon { get; }

	}


	/// <summary>
	/// Interface used to specify new analyzers
	/// </summary>
	public interface IAnalyzer
	{
		string Name { get; }
		//ImageSource Icon { get; }

	}

}
