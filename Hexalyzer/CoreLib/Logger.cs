﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Hexalyzer//CoreLib
{
	//REFACTOR: Add async log writing?

	public class Logger
    {
		public static Logger LOG = null;

		public Logger(string filepath, string appname, Level min_level = Level.Info)
        {
			if (!Directory.Exists(filepath))
				Directory.CreateDirectory(filepath);

			string filename = Path.Combine(filepath, appname + ".log");

			// Backup existing log
			if (File.Exists(filename))
			{
				string backupfile = Path.Combine(filepath, appname + "-" + DateTime.Now.ToString("yyyyMMdd-hhmmss") + ".log");
				try { File.Move(filename, backupfile); }
				catch { /*Just eat this*/ }
			}

			_file = File.CreateText(filename);

			_min_level = min_level;

			Info("Starting up log facility ...");

			// Replace original listeners, replacing with writing into our own log
			Trace.Listeners.RemoveAt(0);
			_trace = new _Trace(this);
			Trace.Listeners.Add(_trace);

			Info("... log facility up and running");
			_file.Write("\n");

			SetLog(this);
		}

		~Logger()
		{
			LOG = null;

			if (_file != null)
				Shutdown();
		}


		public void Shutdown()
		{
			_file.Write("\n");
			Info("Shutting down log facility ...");

			Trace.Listeners.RemoveAt(0);

			if (_trace != null)
				_trace.Close();
			_trace = null;

			Info("... log facility is down now!");

			_file.Close();
			_file = null;
		}


		// Override this if you subclass from Logger, or call it at least once
		virtual public void SetLog(Logger log)
		{
			LOG = log;
		}


		public void Log(string msg, Level level = Level.Info, bool add_ts = true, bool add_nl = true)
		{
			if (level > _min_level)
				return;

			string s = "";
			if (add_ts)
				s += DateTime.Now.ToString("[yyyy/MM/dd hh:mm:ss.fff]") + LEVEL[(int)level];
			s += msg;
			if (add_nl)
				s += "\n";

			lock (_file)
			{
				_file.Write(s);
				_file.Flush();
			}
		}


		public void Debug(string msg, params object[] args)
        {
			if (Level.Debug > _min_level)
				return;
			Log(string.Format(msg, args), Level.Debug);
		}

        public void Info(string msg, params object[] args)
        {
			if (Level.Info > _min_level)
				return;
			Log(string.Format(msg, args), Level.Info);
		}

        public void Warning(string msg, params object[] args)
        {
			if (Level.Warning > _min_level)
				return;
			Log(string.Format(msg, args), Level.Warning);
		}

		public void Error(string msg, params object[] args)
		{
			if (Level.Error > _min_level)
				return;
			Log(string.Format(msg, args), Level.Error);
		}

		public void Error(string msg, Exception exc)
		{
			if (Level.Error > _min_level)
				return;
			string s = msg + ":\n"
				+ exc.Message.ToString() + "\n"
				+ exc.Source.ToString() + "\n"
				+ exc.StackTrace.ToString() + "\n"
				;
			Log(msg, Level.Error);
		}


		protected Level _min_level;
		protected TextWriter _file;
		protected _Trace _trace;

		// Formatted as needed to speed up message generation with just appending it
		public enum Level { Off, Error, Warning, Info, Debug };
		protected static string[] LEVEL = { "[OFF]   ", "[ERROR] ", "[WARN]  ", "[INFO]  ", "[DEBUG] " };


		protected class _Trace : TraceListener
		{
			public _Trace(Logger logger)
				: base("TraceLogger")
			{
				_logger = logger;
				IndentLevel = 0;
				IndentSize = 4;
				_logger.Info("! Warming up " + Name);
			}

			public override void Close()
			{
				_logger.Info("! Shutting down " + Name);
				base.Close();
			}
			//public abstract void Write(string message);
			//public abstract void WriteLine(string message);

			public override void Fail(string message)
			{
				_logger.Error(message);
			}

			public override void Fail(string message, string detailMessage)
			{
				_logger.Error(message);
				_logger.Error(detailMessage);
			}
	
			public override void Write(string message)
			{
				_logger.Info(message);
			}

			public override void WriteLine(string message)
			{
				_logger.Info(message + "\n");
			}

			protected Logger _logger;
		}

	}

}


// To allow for easy access
public static class Log
{
	public static void Debug(string msg, params object[] args) 
	{
		L.Debug(msg, args);
	}

	public static void Info(string msg, params object[] args)
	{
		L.Info(msg, args);
	}

	public static void Warning(string msg, params object[] args)
	{
		L.Warning(msg, args);
	}

	public static void Error(string msg, params object[] args)
	{
		L.Error(msg, args);
	}

	public static void Error(string msg, Exception exc)
	{
		L.Error(msg, exc);
	}

	public static void _(string msg, Hexalyzer.Logger.Level level = Hexalyzer.Logger.Level.Info, bool add_ts = true, bool add_nl = true)
	{
		L.Log(msg, level, add_ts, add_nl);
	}


	private static Hexalyzer.Logger L
	{
		get
		{
			if (Hexalyzer.Logger.LOG == null)
				throw new ArgumentNullException("No logger available");
			return Hexalyzer.Logger.LOG;
		}
	}
}
