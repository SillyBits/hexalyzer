using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hexalyzer
{

	/// <summary>
	/// A dynamic buffer which uses a paged approach internally
	/// For now, only readable implementations exist!
	/// </summary>
	/// <typeparam name="_Type">Type of object buffered</typeparam>
	/// <typeparam name="_Container">Type of container used internally (based on IList&lt;_Type&gt;)</typeparam>
	public abstract class DataBuffer<_Type, _Container>
		//where _Type : struct
		where _Container : IList<_Type>
	{

		/// <summary>
		/// Size of a single page
		/// (published to allow clients to adjust their query strategies)
		/// </summary>
		public int PageSize { get; protected set; }

		/// <summary>
		/// Total size available with this buffer
		/// As of now, not growable
		/// </summary>
		public long Size { get; protected set; }

		/// <summary>
		/// Type of data stored
		/// </summary>
		public Type Datatype { get { return typeof(_Type); } }

		/// <summary>
		/// Return some information on this data buffer
		/// (informative nature)
		/// </summary>
		public abstract string[] Info { get; }
	

		public DataBuffer(int pagesize = 4096)
		{
			PageSize = pagesize;
			Size = 0;
		}


		/// <summary>
		/// Closes buffer
		/// Accessing buffer afterwards will throw exceptions
		/// </summary>
		public virtual void Close()
		{
			Size = 0;
		}


		/// <summary>
		/// Clear buffer, destroying any cached page
		/// </summary>
		public abstract void Clear();


		/// <summary>
		/// Indexed accessor, serving a single object
		/// </summary>
		/// <param name="index">0-based index of object to access</param>
		/// <returns>Object instance</returns>
		public virtual _Type this[long index]
		{
			get
			{
				if (index < 0 || index >= Size)
					throw new IndexOutOfRangeException();

				int page_no = (int)(index / PageSize);
				_Container page = _GetPage(page_no);
				if (page == null)
					throw new Exception("Page missing!");

				return page[(int)(index % PageSize)];
			}
		}


		/// <summary>
		/// Indexed accessor, serving mutiple objects
		/// </summary>
		/// <param name="index">0-based index of object to access</param>
		/// <param name="count">Number of objects to return</param>
		/// <returns>Container with count object instances</returns>
		//public abstract _Container this[long index, int count] { get; }
		public virtual IReadOnlyList<_Type> this[long index, long count]
		{
			get
			{
				if (count > 0x7FFFFFFF)
				{
					Debug.WriteLine(string.Format("Indexer is NOT capable of returning a {0} bytes!\nAdvised to cast to 'Accessor' explicitely.", count));
					return null;
				}
				return new Accessor(this, index, count);
			}
		}


		// Non-public implementation following
		//


		public class Accessor : IReadOnlyList<_Type>
		{
			public _Type this[long index] { get { return Parent[index + Offset]; } }

			public long LongCount { get { return Length; } }


			// IReadOnlyList
			//

			public _Type this[int index] { get { return Parent[index + Offset]; } }

			public int Count { get { return (int)Length; } }

			public IEnumerator<_Type> GetEnumerator()
			{
				return new Enumerator(this);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				IEnumerator<_Type> e = GetEnumerator();
				return e as IEnumerator;
			}


			internal Accessor(DataBuffer<_Type, _Container> parent, long offset, long length)
			{
				Parent = parent;
				Offset = offset;
				Length = length;
			}

			protected DataBuffer<_Type, _Container> Parent;
			protected long Offset;
			protected long Length;


			protected class Enumerator : IEnumerator<_Type>
			{
				public _Type Current { get { return Parent[Index]; } }

				object IEnumerator.Current { get { return Parent[Index]; } }

				public bool MoveNext()
				{
					Index++;
					return (Index < Parent.Length);
				}

				public void Reset()
				{
					Index = -1;
				}


				public void Dispose()
				{ }


				internal Enumerator(Accessor parent)
				{
					Parent = parent;
					Index = -1;
				}

				private Accessor Parent;
				private long Index;

			}

		}


		/// <summary>
		/// Internal request for a page
		/// </summary>
		/// <param name="page">Page to return</param>
		/// <returns>Page instance</returns>
		protected abstract _Container _GetPage(long page);

	}


	/// <summary>
	/// Dynamic buffer based on a file
	/// </summary>
	/// <typeparam name="_Type"></typeparam>
	public class FileBuffer<_Type> : DataBuffer<_Type, _Type[]>
		where _Type : struct
	{
		/// <summary>
		/// Return some information on this data buffer
		/// (informative nature)
		/// </summary>
		public override string[] Info
		{
			get
			{
				//TODO:
				return new string[] {
					string.Format("Filename: {0}", Path.GetFileName(Filename)),
					string.Format("File size: {0} bytes", Size),
					string.Format("Page size: {0} bytes", PageSize),
					string.Format("Datatype: {0}", Datatype.Name),
					string.Format("Max. pages: {0}", _MaxPages),
					string.Format("Page Cache: {0} pages", _PageCache.Count),
				};
			}
		}


		/// <summary>
		/// File which serves the data
		/// </summary>
		public string Filename;


		/// <summary>
		/// Creates a new file buffer
		/// Note that there is no pagesize parameter, this will be taken from 
		/// memory-mapped view once created.
		/// </summary>
		/// <param name="filepath">Path to file</param>
		/// <param name="max_pages">No. of pages to be cached</param>
		public FileBuffer(string filepath, int max_pages = 3)
			: base(0)
		{
			Filename = filepath;
			_MaxPages = max_pages;

			_OpenMemView();
		}


		/// <summary>
		/// Closes buffer
		/// Accessing buffer afterwards will throw exceptions
		/// </summary>
		public override void Close()
		{
			base.Close();

			_CloseMemView();
		}


		/// <summary>
		/// Clear buffer, destroying any cached page
		/// </summary>
		public override void Clear()
		{
			//base.Clear();

			_PageCache.Clear();
		}


		// Non-public implementation following
		//


		/// <summary>
		/// Internal request for a page
		/// </summary>
		/// <param name="page">Page to return</param>
		/// <returns>Page instance</returns>
		protected override _Type[] _GetPage(long page)
		{
			if (!_PageCache.ContainsKey(page))
				return _LoadPage(page);

			PageCache cache = _PageCache[page];
#if DEBUG
			if (cache.Index != page || cache.Offset != page * PageSize)
				throw new Exception("Invalid page cache detected!");
#endif
			return cache.Data;
		}


		~FileBuffer()
		{
			Close();
		}


		/// <summary>
		/// Open memory view
		/// </summary>
		protected void _OpenMemView()
		{
			_CloseMemView();

			FileInfo fileinfo = new FileInfo(Filename);
			if (!fileinfo.Exists)
				throw new FileNotFoundException(Filename);
			Size = fileinfo.Length;

			_MemFile = MemoryMappedFile.CreateFromFile(Filename, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
			if (typeof(_Type) == typeof(byte))
			{
				_MemStream = _MemFile.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
				PageSize = (int)_MemStream.Capacity;
			}
			else
			{
				_MemView = _MemFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
				PageSize = (int)_MemView.Capacity;
			}
		}

		/// <summary>
		/// Close previously opened memory view
		/// </summary>
		protected void _CloseMemView()
		{
			if (_MemView != null)
				_MemView.Dispose();
			_MemView = null;

			if (_MemStream != null)
				_MemStream.Dispose();
			_MemStream = null;

			if (_MemFile != null)
				_MemFile.Dispose();
			_MemFile = null;
		}

		/// <summary>
		/// Load given page into cache, removed oldest page if max. no of pages reached
		/// </summary>
		/// <param name="page">Page to read</param>
		/// <returns>Page instance read</returns>
		protected _Type[] _LoadPage(long page)
		{
			if (_PageCache.Count >= _MaxPages)
			{
				KeyValuePair<long, PageCache> entry = _PageCache.OrderBy(pair => pair.Value.Created).First();
				_PageCache.Remove(entry.Key);
			}

			long offset = page * PageSize;

			PageCache cache = new PageCache();
#if DEBUG
			cache.Index = page;
			cache.Offset = offset;
#endif
			cache.Data = new _Type[PageSize];
			cache.Created = DateTime.Now;

			int read = -1;
			if (typeof(_Type) == typeof(byte))
			{
				if (_MemStream.Seek(offset, SeekOrigin.Begin) != offset)
					throw new Exception("Read error! (seek failure)");
				read = _MemStream.Read(cache.Data as byte[], 0, PageSize);
			}
			else
			{
				read = _MemView.ReadArray(offset, cache.Data, 0, PageSize);
			}
			if ((long)read != PageSize)
				throw new Exception("Read error! (length mismatch)");

			_PageCache.Add(page, cache);

			return cache.Data;
		}


		protected int _MaxPages;
		protected MemoryMappedFile _MemFile;
		protected MemoryMappedViewAccessor _MemView;
		protected MemoryMappedViewStream _MemStream;
		protected Dictionary<long, PageCache> _PageCache = new Dictionary<long, PageCache>();


		protected struct PageCache
		{
#if DEBUG
			/// <summary>
			/// Debug only!
			/// To ensure page requested and page served are the same index.
			/// </summary>
			internal long Index;

			/// <summary>
			/// Debug only!
			/// To ensure page requested and page served referencing same offset.
			/// </summary>
			internal long Offset;
#endif

			/// <summary>
			/// Data blob this page represents
			/// </summary>
			internal _Type[] Data;

			/// <summary>
			/// Creation timestamp, used to remove oldest page if max. no of pages reached
			/// </summary>
			internal DateTime Created;
		}


	}


}
