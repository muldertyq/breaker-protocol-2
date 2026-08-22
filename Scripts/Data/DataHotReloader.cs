using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Godot;

namespace BreakerProtocol.Data
{
	/// <summary>
	/// 数据运行时热重载监听器：负责文件监控、多写防抖、进程锁避让与主线程安全分发
	/// </summary>
	public class DataHotReloader : IDisposable
	{
		private readonly List<FileSystemWatcher> _watchers = new();
		private readonly ConcurrentDictionary<string, double> _pendingChanges = new();
		private const double DebounceDelaySeconds = 0.2;

		public event Action<string>? OnFileReloadRequested;

		public void StartWatching(params string[] directoryPaths)
		{
			StopWatching();

			foreach (var dir in directoryPaths)
			{
				if (!Directory.Exists(dir)) continue;

				try
				{
					var watcher = new FileSystemWatcher(dir)
					{
						Filter = "*.json",
						IncludeSubdirectories = true,
						NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
					};

					watcher.Changed += OnFileSystemEvent;
					watcher.Created += OnFileSystemEvent;
					watcher.EnableRaisingEvents = true;

					_watchers.Add(watcher);
					GD.Print($"[DataHotReloader] 已启动热重载监听: {dir}");
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataHotReloader] 监听目录 [{dir}] 失败: {ex.Message}");
				}
			}
		}

		private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType is WatcherChangeTypes.Changed or WatcherChangeTypes.Created)
			{
				double now = Time.GetTicksMsec() / 1000.0;
				_pendingChanges[e.FullPath] = now;
			}
		}

		public void Poll()
		{
			if (_pendingChanges.IsEmpty) return;

			double now = Time.GetTicksMsec() / 1000.0;

			foreach (var kvp in _pendingChanges)
			{
				string filePath = kvp.Key;
				double triggerTime = kvp.Value;

				if (now - triggerTime >= DebounceDelaySeconds)
				{
					_pendingChanges.TryRemove(filePath, out _);
					ExecuteSafeReload(filePath);
				}
			}
		}

		private void ExecuteSafeReload(string filePath)
		{
			if (!File.Exists(filePath)) return;

			// 避开编辑器保存瞬间的文件独占锁 (尝试 3 次)
			for (int i = 0; i < 3; i++)
			{
				try
				{
					using var stream = File.Open(
						filePath, 
						System.IO.FileMode.Open, 
						System.IO.FileAccess.Read, 
						System.IO.FileShare.ReadWrite
					);

					GD.PrintRich($"[color=yellow][DataHotReloader] 检测到数据变更: {Path.GetFileName(filePath)}，触发热更新...[/color]");
					OnFileReloadRequested?.Invoke(filePath);
					return;
				}
				catch (IOException)
				{
					Thread.Sleep(50);
				}
				catch (Exception ex)
				{
					GD.PrintErr($"[DataHotReloader] 读取文件 [{filePath}] 异常: {ex.Message}");
					return;
				}
			}

			GD.PrintErr($"[DataHotReloader] 文件 [{filePath}] 被独占锁定，跳过本次热更。");
		}

		public void StopWatching()
		{
			foreach (var w in _watchers)
			{
				w.EnableRaisingEvents = false;
				w.Dispose();
			}
			_watchers.Clear();
			_pendingChanges.Clear();
		}

		public void Dispose()
		{
			StopWatching();
		}
	}
}
