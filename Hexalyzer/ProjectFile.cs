using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


/*
 * TODO:
 * 
 * - New type 'Structure' with just a blob of user-defined length.
 * 
 * 
 */


namespace Hexalyzer
{

	/// <summary>
	/// Class representing a Hexalyzer project
	/// </summary>
	public class ProjectFile
	{

		/// <summary>
		/// Filename for this project (must be an absolute path)
		/// </summary>
		public string Filename { get; private set; }

		/// <summary>
		/// Name of project
		/// </summary>
		public string Project { get; private set; }

		/// <summary>
		/// File being analyzed (returned using absolute path)
		/// </summary>
		public string Source
		{
			get
			{
				if (_Source != null && !Path.IsPathRooted(_Source))
					return Path.Combine(Path.GetDirectoryName(Filename), _Source);
				return _Source;
			}
			private set
			{
				_Source = value;
			}
		}

		/// <summary>
		/// Project nodes
		/// Every project will have at least 1 node, even newly created ones 
		/// (a placeholder with it's length being the source file length). 
		/// So the sum of all nodes matches the file size.
		/// </summary>
		public List<ProjectNode> Nodes { get; private set; }

		/// <summary>
		/// Total size of source file being analyzed
		/// </summary>
		public long Filesize
		{
			get
			{
				if (_DataBuffer == null)
					return -1;
				return _DataBuffer.Size;
			}
		}

		/// <summary>
		/// Indicates whether or not project was modified
		/// </summary>
		public bool IsModified
		{
			get
			{
				return _IsModified;
			}
			set
			{
				_IsModified = value;
				IsModifiedChanged?.Invoke(this, _IsModified);
			}
		}
		public delegate void IsModifiedChangedHandler(object sender, bool state);
		public event IsModifiedChangedHandler IsModifiedChanged;

		/// <summary>
		/// Return some information on the data buffer
		/// (informative nature)
		/// </summary>
		public string[] BufferInfo
		{
			get
			{
				if (_DataBuffer == null)
					return null;
				return _DataBuffer.Info;
			}
		}


		/// <summary>
		/// Create new project
		/// </summary>
		/// <param name="filename">Filename to use for saving project</param>
		/// <param name="project">Project name</param>
		/// <param name="source">File being analyzed</param>
		/// <returns>Newly created project file instance</returns>
		static public ProjectFile Create(string filename, string project, string source)
		{
			return new ProjectFile(filename, project, source);
		}

		/// <summary>
		/// Load project from file
		/// </summary>
		/// <param name="filename">Filename to use for loading project</param>
		/// <returns>New project file instance reflexting file contents</returns>
		static public ProjectFile Load(string filename)
		{
			if (!File.Exists(filename))
			{
				Debug.Fail("Tried to open a file which does not exist!\n" + filename);
				return null;
			}
			try
			{
				ProjectFile prj = new ProjectFile();
				prj._Load(filename);
				return prj;
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error loading project file {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Debug.Fail(msg);
				return null;
			}
		}

		/// <summary>
		/// Save project using its current filename
		/// </summary>
		public void Save()
		{
			try
			{
				_Save(Filename);
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error saving project file {0}:\n", Filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Debug.Fail(msg);
			}
		}

		/// <summary>
		/// Save project to a different filename
		/// </summary>
		/// <param name="filename">Filename to use for saving project</param>
		public void SaveAs(string filename)
		{
			try
			{
				_Save(filename);
				Filename = filename;
			}
			catch (Exception exc)
			{
				string msg = string.Format("Error saving project file {0}:\n", filename)
							+ exc.Message.ToString() + "\n"
							+ exc.Source.ToString() + "\n"
							+ exc.StackTrace.ToString() + "\n"
							;
				Debug.Fail(msg);
			}
		}

		/// <summary>
		/// Closes project
		/// </summary>
		public void Close()
		{
			Nodes = null;
			_CloseMem();
		}


		/// <summary>
		/// Tries to find covering node by offset
		/// </summary>
		/// <param name="offset">Offset to use</param>
		/// <returns>ProjectNode instance, or null if none found</returns>
		public ProjectNode FindNodeByOffset(long offset)
		{
			return Nodes.Find(n => (n.Offset <= offset && offset < (n.Offset + n.Length)));
		}

		/// <summary>
		/// Find node before given one
		/// </summary>
		/// <param name="node">Node</param>
		/// <returns>Previous node in chain, or null if this was first node</returns>
		public ProjectNode FindPrevNode(ProjectNode node)
		{
			int index = Nodes.IndexOf(node);
			index--;
			if (index >= 0)
				return Nodes[index];
			return null;
		}

		/// <summary>
		/// Find node following given one
		/// </summary>
		/// <param name="node">Node</param>
		/// <returns>Next node in chain, or null if this was last node</returns>
		public ProjectNode FindNextNode(ProjectNode node)
		{
			int index = Nodes.IndexOf(node);
			index++;
			if (index < Nodes.Count)
				return Nodes[index];
			return null;
		}


		/// <summary>
		/// Insert a new node at given offset
		/// </summary>
		/// <param name="type">Type of node to add</param>
		/// <param name="offset">Absolute offset where new node is to be inserted</param>
		/// <param name="length">Length  of node, if -1 is passed length is taken from type instead</param>
		/// <returns>Nodes added, or null to indicate failure</returns>
		public ProjectNode[] InsertAt(Type type, long offset, long length)
		{
			// Find node which holds this offset
			ProjectNode source = FindNodeByOffset(offset);
			if (source == null)
				return null;

			return InsertAt(source, type, offset - source.Offset, length);
		}

		/// <summary>
		/// Insert a new node at given offset
		/// </summary>
		/// <param name="node">Node to split for this insert operation</param>
		/// <param name="type">Type of node to add</param>
		/// <param name="offset">Absolute offset where new node is to be inserted</param>
		/// <param name="length">Length  of node, if -1 is passed length is taken from type instead</param>
		/// <returns>Nodes added, or null to indicate failure</returns>
		public ProjectNode[] InsertAt(ProjectNode node, Type type, long offset, long length)
		{
			if (node == null /*|| type == null*/ || offset < 0)
				throw new ArgumentException();

			// Only untyped nodes can be split
			if (node.Type != null)
				return null;

			// Replace length with actual length if none was given
			if (length < 0)
			{
				if (type != null)
				{
					//byte[] data = node[0, node.Length];
					IReadOnlyList<byte> data = node[0, node.Length];
					length = Datatypes.Helpers.LengthOf(type, data, offset);
				}
				else
				{
					// Un-typed split
					length = node.Length - offset;
				}
			}

			// Split node data into 1-3 parts based on data avail
			ProjectNode[] added = node.Split(type, offset, length);
			if (added != null)
			{
				int index = Nodes.IndexOf(node);

				// Remove original node
				Nodes.Remove(node);

				// Add any new node
				Nodes.InsertRange(index, added);

				// Mark as modified
				IsModified = true;
			}

			// Return nodes added
			return added;
		}

		/// <summary>
		/// Removes node by re-combining its data into neighbor(s)
		/// </summary>
		/// <param name="node">Node to remove</param>
		/// <param name="removed">List of nodes removed</param>
		/// <returns>ProjectNode instance if successfully, null if error occured</returns>
		public ProjectNode Remove(ProjectNode node, out ProjectNode[] removed)
		{
			removed = null;
			if (node == null)
				return null;

			int index = Nodes.IndexOf(node);
			ProjectNode prev = Nodes.ElementAtOrDefault(index-1);
			ProjectNode next = Nodes.ElementAtOrDefault(index+1);

			List<ProjectNode> remove_list = new List<ProjectNode>();
			int remove_index;
			ProjectNode replacement;

			if (index == 0)
			{
				// Head node, so combine with node following
				if (next == null)
					throw new IndexOutOfRangeException();

				remove_list.Add(node);
				if (next.Type == null)
				{
					// Allowed to combine
					replacement = new ProjectNode(node, next);
					remove_list.Add(next);
					remove_index = index;
				}
				else
				{
					// Next node is typed, DON'T touch
					replacement = new ProjectNode(this, node.Offset, node.Length, null, node.Remark);
					remove_index = index;
				}
			}
			else if (index == Nodes.Count-1)
			{
				// Tail node, combine with previous node
				if (prev == null)
					throw new IndexOutOfRangeException();

				if (prev.Type == null)
				{
					// Allowed to combine
					replacement = new ProjectNode(prev, node);
					remove_list.Add(prev);
					remove_index = index - 1;
				}
				else
				{
					// Prev node is typed, DON'T touch
					replacement = new ProjectNode(this, node.Offset, node.Length, null, node.Remark);
					remove_index = index;
				}
				remove_list.Add(node);
			}
			else
			{
				// In between, so combine with previous + following nodes
				if (prev.Type == null && next.Type == null)
				{
					// Allowed to combine
					replacement = new ProjectNode(prev, node, next);
					remove_list.Add(prev);
					remove_list.Add(node);
					remove_list.Add(next);
					remove_index = index - 1;
				}
				else if (prev.Type == null)
				{
					// Allowed to combine with prev, but NOT next
					replacement = new ProjectNode(prev, node);
					remove_list.Add(prev);
					remove_list.Add(node);
					remove_index = index - 1;
				}
				else if (next.Type == null)
				{
					// Allowed to combine with next, but NOT prev
					replacement = new ProjectNode(node, next);
					remove_list.Add(node);
					remove_list.Add(next);
					remove_index = index;
				}
				else
				{
					// Prev and next node are both typed, DON'T touch
					replacement = new ProjectNode(this, node.Offset, node.Length, null, node.Remark);
					remove_list.Add(node);
					remove_index = index;
				}
			}

			if (remove_list.Count > 0)
			{
				// Remove old nodes
				Nodes.RemoveRange(remove_index, remove_list.Count);

				// Add replacement node
				Nodes.Insert(remove_index, replacement);

				// Mark as modified
				IsModified = true;
			}

			removed = remove_list.ToArray();
			return replacement;
		}


		// Rest of members solely private!
		//

		/// <summary>
		/// Private constructor, one has to use dedicated methods Create() 
		/// or Load() for creating an project file instance
		/// </summary>
		private ProjectFile(string filename = null, string project = null, string source = null)
		{
			Filename = filename;
			Project = project;
			Source = source;
			Nodes = new List<ProjectNode>();

			if (Source != null)
			{
				_OpenMem();

				Nodes.Add(new ProjectNode(this, 0, Filesize));
			}

			// Newly created projects are always modified
			IsModified = true;
		}

		/// <summary>
		/// Destructor
		/// </summary>
		~ProjectFile()
		{
			_CloseMem();
		}

	
		/// <summary>
		/// Private load method
		/// </summary>
		/// <param name="filename">Filename to use for loading project</param>
		private void _Load(string filename)
		{
			_IsModified = false;

			Filename = filename;
			Nodes.Clear();

			XmlDocument xml = new XmlDocument();
			xml.Load(Filename);

			if (xml.ChildNodes.Count < 2)
				throw new Exception("INVALID PROJECT FILE! (too few nodes)");

			XmlNode node = xml.ChildNodes[0];
			if (node.Name.ToLower() != "xml")
				throw new Exception("INVALID PROJECT FILE! (missing '?xml' node)");

			node = xml.ChildNodes.OfType<XmlElement>().FirstOrDefault();
			if (node == null || node.Name != "Hexalyzer-Project")
				throw new Exception("INVALID PROJECT FILE! (missing 'Hexalyzer-Project' node)");
			XmlAttribute attr = node.Attributes["version"];
			if (attr == null || attr.InnerText != "0.1")
				throw new Exception("INVALID PROJECT FILE! (missing/unknown version)");

			foreach (XmlElement child in node.ChildNodes.OfType<XmlElement>())
			{
				switch (child.Name)
				{
					case "Name":
						Project = child.InnerText;
						break;

					case "Source":
						Source = child.InnerText;
						//TODO: Re-link source file if missing
						//if (!File.Exists(Source))
						//{
						//}
						_OpenMem();
						break;

					case "Nodes":
						if (Source == null)
							throw new Exception("INVALID PROJECT FILE! (node order)");
						foreach (XmlElement element in child.ChildNodes.OfType<XmlElement>())
						{
							int index;
							ProjectNode prj_node = ProjectNode.FromXml(this, element, out index);
							if (index != Nodes.Count)
								Debug.WriteLine("ProjectNode: Abnormal index {0} found, expected {1}",
									index, Nodes.Count);
							Nodes.Add(prj_node);
						}
						break;

					case "":
					case null:
						throw new Exception("INVALID PROJECT FILE! (empty child node)");

					default:
						throw new Exception(string.Format(
							"INVALID PROJECT FILE! (unknown child node {0})",
							child.Name));

				}
			}	
		}

		/// <summary>
		/// Private save method
		/// </summary>
		/// <param name="filename">Filename to use for saving project</param>
		private void _Save(string filename)
		{
			XmlDocument xml = new XmlDocument();
			XmlElement element;

			//<?xml version="1.0" encoding="utf-8"?>
			XmlDeclaration decl = xml.CreateXmlDeclaration("1.0", "utf-8", null);
			xml.AppendChild(decl);

			XmlElement root = xml.CreateElement("Hexalyzer-Project");
			root.SetAttribute("version", "0.1");
			xml.AppendChild(root);

			element = xml.CreateElement("Name");
			element.InnerText = Project;
			root.AppendChild(element);

			string source = _Source;
			if (!Path.IsPathRooted(source))
			{
				// Convert to relative path based on project filename
				Uri full = new Uri(Path.GetDirectoryName(Filename));
				Uri src = new Uri(Path.GetDirectoryName(Source));
				source = Path.Combine(src.MakeRelativeUri(full).ToString(), Path.GetFileName(source));
			}
			element = xml.CreateElement("Source");
			element.InnerText = source;
			root.AppendChild(element);

			element = xml.CreateElement("Nodes");
			int index = 0;
			foreach (ProjectNode node in Nodes)
			{
				element.AppendChild(node.ToXml(xml, index));
				++index;
			}
			root.AppendChild(element);

			xml.Save(filename);//+".test");

			// Remove modified state
			IsModified = false;
		}


		/// <summary>
		/// Open memory view
		/// </summary>
		private void _OpenMem()
		{
			_CloseMem();

			_DataBuffer = new FileBuffer<byte>(Source, 64); 
		}

		/// <summary>
		/// Close previously opened memory view
		/// </summary>
		private void _CloseMem()
		{
			if (_DataBuffer != null)
				_DataBuffer.Close();
			_DataBuffer = null;
		}


		private string _Source;
		private bool _IsModified;
		internal FileBuffer<byte> _DataBuffer;

	}


	/// <summary>
	/// Node class which represents analyzed data
	/// </summary>
	public class ProjectNode
	{
		/// <summary>
		/// Project file this node is contained in
		/// </summary>
		public ProjectFile Parent { get; private set; }

		/// <summary>
		/// Length of data for this node
		/// </summary>
		public long Length { get; private set; }

		/// <summary>
		/// Indexed accessor for data this node represents.
		/// </summary>
		/// <param name="index">Index to access</param>
		/// <returns>Value at given index</returns>
		public byte this[long index]
		{
			get
			{
				//TODO: Serve from short-time buffer if avail
				//      (don't forget that underlying buffer must serve this from multiple pages!)
				return Parent._DataBuffer[index + Offset];
			}
		}

		/// <summary>
		/// Indexed accessor for data this node represents.
		/// For now, only readable
		/// </summary>
		/// <param name="index">Index to access</param>
		/// <param name="count">Number of values to return</param>
		/// <returns>Container with count values</returns>
		public IReadOnlyList<byte> this[long index, long count]
		{
			get
			{
				//TODO: If requested [0,Len) -> Create short-time buffer to serve from
				//      (don't forget that underlying buffer must serve this from multiple pages!)
				if (count > 0x7FFFFFFF)
				{
					Debug.Fail(string.Format("Indexer is NOT capable of returning a {0} bytes!", count));
					return null;
				}
				return Parent._DataBuffer[index + Offset, (int)count];
			}
		}

		/// <summary>
		/// Get all data, shortcut for this[0, Length]
		/// </summary>
		public IReadOnlyList<byte> Data
		{
			get
			{
				if (Length > 0x7FFFFFFF)
				{
					Debug.Fail(string.Format("'Data' member is NOT capable of returning a {0} bytes!", Length));
					return null;
				}
				return this[0, (int)Length];
			}
		}

		/// <summary>
		/// Offset into source data
		/// </summary>
		public long Offset { get; private set; }

		/// <summary>
		/// Type of value this block expresses
		/// </summary>
		public Type Type { get; private set; }

		/// <summary>
		/// Remarks on this node (optional)
		/// </summary>
		public string Remark
		{
			get { return _Remark; }
			set
			{
				if (_Remark != value)
					Parent.IsModified = true;
				_Remark = value;
			}
		}


		public override string ToString()
		{
			string s = string.Format("[{0:X8}-{1:X8}] {2}", Offset, Offset + Length - 1, Type);
			if (!string.IsNullOrEmpty(Remark))
				s += " (" + Remark + ")";
			return s;
		}


		// Following internal only members
		//

		/// <summary>
		/// Constructs a new node, using passed data
		/// </summary>
		/// <param name="parent">Project file this node is contained in</param>
		/// <param name="offset">Offset into source data</param>
		/// <param name="length">Length of data</param>
		/// <param name="type">Type of value this block expresses</param>
		/// <param name="remark">Remarks on this node (optional)</param>
		internal ProjectNode(ProjectFile parent, long offset, long length, Type type = null, string remark = null)
		{
			Parent = parent;
			Offset = offset;
			Length = length;
			Type = type;

			_Remark = remark;
		}

		/// <summary>
		/// Combines 2 nodes into one
		/// </summary>
		/// <param name="left">Left node</param>
		/// <param name="right">Right node</param>
		internal ProjectNode(ProjectNode left, ProjectNode right)
		{
			if (left == null || right == null)
				throw new ArgumentNullException();
			if (left.Parent != right.Parent)
				throw new ArgumentException("Parents do not match!");
			if (left.Offset + left.Length != right.Offset)
				throw new ArgumentException("Nodes aren't continuous!");

			Parent = left.Parent;
			Offset = left.Offset;
			Length = left.Length + right.Length;
			Type = null;
			//Remark = left.Remark;
			//if (Remark == null)
			//	Remark = right.Remark;
			_Remark = string.Join("|", new string[]{ left.Remark, right.Remark });

			//// 0              L.Len     L.Len+R.Len
			//// |------Left------|------Right------|
			//Data = new byte[left.Data.Length + right.Data.Length];
			//Array.Copy(left.Data, 0, Data, 0, left.Data.Length);
			//Array.Copy(right.Data, 0, Data, left.Data.Length, right.Data.Length);
		}

		/// <summary>
		/// Combines 3 nodes into one
		/// </summary>
		/// <param name="left">Left node</param>
		/// <param name="middle">Middle node</param>
		/// <param name="right">Right node</param>
		internal ProjectNode(ProjectNode left, ProjectNode middle, ProjectNode right)
		{
			if (left == null || middle == null || right == null)
				throw new ArgumentNullException();
			if (left.Parent != middle.Parent || middle.Parent != right.Parent)
				throw new ArgumentException("Parents do not match!");
			if (left.Offset + left.Length != middle.Offset || 
				middle.Offset + middle.Length != right.Offset)
				throw new ArgumentException("Nodes aren't continuous!");

			Parent = left.Parent;
			Offset = left.Offset;
			Length = left.Length + middle.Length + right.Length;
			Type = null;
			//Remark = left.Remark;
			//if (Remark == null)
			//	Remark = middle.Remark;
			//if (Remark == null)
			//	Remark = right.Remark;
			_Remark = string.Join("|", new string[]{ left.Remark, middle.Remark, right.Remark });

			//// 0              L.Len           L.Len+M.Len        Sum()
			//// |------Left------|------Middle------|------Right------|
			//Data = new byte[left.Data.Length + middle.Data.Length + right.Data.Length];
			//int ofs = 0;
			//Array.Copy(left.Data, 0, Data, ofs, left.Data.Length);
			//ofs += left.Data.Length;
			//Array.Copy(middle.Data, 0, Data, ofs, middle.Data.Length);
			//ofs += middle.Data.Length;
			//Array.Copy(right.Data, 0, Data, ofs, right.Data.Length);
		}


		/// <summary>
		/// Split node at offset given, returning as list of newly created nodes
		/// </summary>
		/// <param name="type">Type of new node</param>
		/// <param name="offset">Offset on where to split node</param>
		/// <param name="length">Length of new node</param>
		/// <returns>List of newly created node, or null if error occured</returns>
		internal ProjectNode[] Split(Type type, long offset, long length)
		{
			if ((offset + length) > Length)
				return null;

			List<ProjectNode> added = new List<ProjectNode>();
			ProjectNode node;
			//byte[] data;

			if (offset == 0)
			{
				// Split at beginning, so a 1-2 nodes to produce
				// 0         len         Len
				// |----len---|----Tail----|

				// Create new node with spec passed in
				//data = new byte[length];
				//Array.Copy(Data, 0, data, 0, length);
				node = new ProjectNode(Parent, Offset, length, type);
				added.Add(node);

				long remain = Length - length;
				if (remain > 0)
				{
					// Still some data remaining, create tail node with same specs as this one
					//data = new byte[remain];
					//Array.Copy(Data, length, data, 0, remain);
					node = new ProjectNode(Parent, Offset + length, remain, Type, Remark);
					added.Add(node);
				}
			}
			else
			{
				// Split in between, so a 2-3 nodes to produce
				// 0          ofs       ofs+len       Len
				// |----Lead---|----len----|----Tail----|

				// Create leading node with same specs as this one
				//data = new byte[offset];
				//Array.Copy(Data, 0, data, 0, offset);
				node = new ProjectNode(Parent, Offset, offset, Type, !string.IsNullOrEmpty(Remark) ? "(1) "+Remark : null);
				added.Add(node);

				// Create new node with spec passed in
				//data = new byte[length];
				//Array.Copy(Data, offset, data, 0, length);
				node = new ProjectNode(Parent, Offset + offset, length, type);
				added.Add(node);

				long remain = Length - offset - length;
				if (remain > 0)
				{
					// Still some data remaining, create tail node with same specs as this one
					//data = new byte[remain];
					//Array.Copy(Data, offset + length, data, 0, remain);
					node = new ProjectNode(Parent, Offset + offset + length, remain, Type, !string.IsNullOrEmpty(Remark) ? "(2) "+Remark : null);
					added.Add(node);
				}
			}

			return added.ToArray();
		}


		/// <summary>
		/// Create new ProjectNode from xml element passed in
		/// </summary>
		/// <param name="parent">Project file this node is contained in</param>
		/// <param name="element">Xml element to use</param>
		/// <returns>Newly created instance</returns>
		internal static ProjectNode FromXml(ProjectFile parent, XmlElement element, out int index)
		{
			index = -1;
			long offset = -1;
			long length = -1;
			string type_s = null;

			foreach (XmlAttribute attr in element.Attributes)
			{
				switch (attr.Name)
				{
					case "index":  index  = int.Parse(attr.InnerText); break;
					case "offset": offset = long.Parse(attr.InnerText); break;
					case "length": length = long.Parse(attr.InnerText); break;
					case "type":   type_s = attr.InnerText; break;

					case "":
					case null:
						throw new Exception(string.Format(
							"INVALID PROJECT NODE! (empty attribute {0})", 
							attr.Name));

					default:
						throw new Exception(string.Format(
							"INVALID PROJECT NODE! (unknown attribute {0})", 
							attr.Name));
				}
			}

			if (index < 0)
				throw new Exception("INVALID PROJECT NODE! (missing index)");
			if (offset < 0)
				throw new Exception("INVALID PROJECT NODE! (missing offset)");
			if (length < 0)
				throw new Exception("INVALID PROJECT NODE! (missing length)");

			Type type = null;
			if (!string.IsNullOrEmpty(type_s))
			{
				if (type == null)
					type = Type.GetType(type_s);
				if (type == null)
					type = Type.GetType("System." + type_s);
				if (type == null)
					type = Type.GetType("Hexalyzer.Datatypes." + type_s);
			}

			return new ProjectNode(parent, offset, length, type, element.InnerText);
		}

		/// <summary>
		/// Create new XmlElement from project node contents
		/// </summary>
		/// <param name="xmldoc">XML doc to use for creating element</param>
		/// <param name="index">Sequential index to use</param>
		/// <returns>Newly created XmlElement</returns>
		internal XmlElement ToXml(XmlDocument xmldoc, int index)
		{
			XmlElement element = xmldoc.CreateElement("Node");

			element.SetAttribute("index", index.ToString());

			element.SetAttribute("offset", Offset.ToString());

			element.SetAttribute("length", Length.ToString());

			if (Type != null)
				element.SetAttribute("type", Type.Name);

			if (!string.IsNullOrEmpty(Remark))
				element.InnerText = Remark;

			return element;
		}


		// Rest of members solely private!
		//
	
		private string _Remark;

	}

}
