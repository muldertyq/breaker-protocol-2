using System;
using System.IO;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Registries;

namespace BreakerProtocol.Data
{
	/// <summary>
	/// 独立模块化数据服务（无需 Autoload，按需实例化或挂载）
	/// </summary>
	public partial class ModuleDataService : Node, IDisposable
	{
		public ModLoader Loader { get; } = new();
		public DataHotReloader HotReloader { get; } = new();

		public Registry<ModuleDataDefinition> Modules => Loader.ModuleRegistry;

		[Signal] public delegate void DataReloadedEventHandler();

		public override void _EnterTree()
		{
			InitializeService();
		}

		public void InitializeService()
		{
			// 1. 全量扫描加载
			Loader.LoadAllMods();

			// 2. 绑定热更回调
			HotReloader.OnFileReloadRequested -= OnFileChanged;
			HotReloader.OnFileReloadRequested += OnFileChanged;

			// 3. 启动物理目录监听
			string rootPath = OS.HasFeature("editor") 
				? ProjectSettings.GlobalizePath("res://") 
				: OS.GetExecutablePath().GetBaseDir();

			HotReloader.StartWatching(
				Path.Combine(rootPath, "core_data"),
				Path.Combine(rootPath, "mods")
			);
		}

		public override void _Process(double delta)
		{
			// 轮询防抖队列
			HotReloader.Poll();
		}

		private void OnFileChanged(string fullPath)
		{
			bool success = Loader.ReloadSingleFile(fullPath);
			if (success)
			{
				EmitSignal(SignalName.DataReloaded);
			}
		}

		public override void _ExitTree()
		{
			Dispose();
		}

		public new void Dispose()
		{
			HotReloader.StopWatching();
			HotReloader.Dispose();
		}
	}
}
