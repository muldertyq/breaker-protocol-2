using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Godot;

namespace BreakerProtocol.Data.Registries
{
	/// <summary>
	/// 泛型线程安全数据注册表
	/// </summary>
	/// <typeparam name="T">注册的数据实体类型</typeparam>
	public class Registry<T> where T : class
	{
		public string RegistryName { get; }
		private readonly ConcurrentDictionary<string, T> _storage = new();

		public event Action<string, T>? OnItemRegistered;

		public Registry(string registryName)
		{
			RegistryName = registryName;
		}

		public bool Register(string id, T item, bool allowOverwrite = true)
		{
			if (string.IsNullOrWhiteSpace(id))
			{
				GD.PrintErr($"[Registry:{RegistryName}] 注册失败：ID 不能为空！");
				return false;
			}

			if (item == null)
			{
				GD.PrintErr($"[Registry:{RegistryName}] 注册失败：ID [{id}] 对应的对象为 null！");
				return false;
			}

			if (_storage.ContainsKey(id))
			{
				if (!allowOverwrite)
				{
					GD.PrintErr($"[Registry:{RegistryName}] 注册冲突：ID [{id}] 已存在且不允许覆盖！");
					return false;
				}
				_storage[id] = item;
				GD.PrintRich($"[color=yellow][Registry:{RegistryName}] 数据覆写：[{id}][/color]");
			}
			else
			{
				_storage.TryAdd(id, item);
				GD.Print($"[Registry:{RegistryName}] 注册成功：[{id}]");
			}

			OnItemRegistered?.Invoke(id, item);
			return true;
		}

		public T Get(string id)
		{
			if (_storage.TryGetValue(id, out var item))
			{
				return item;
			}
			throw new KeyNotFoundException($"[Registry:{RegistryName}] 未找到 ID 为 [{id}] 的数据项！");
		}

		public bool TryGet(string id, out T? item) => _storage.TryGetValue(id, out item);

		public bool Contains(string id) => _storage.ContainsKey(id);

		public IEnumerable<T> GetAll() => _storage.Values;

		public int Count => _storage.Count;

		public void Clear()
		{
			_storage.Clear();
			GD.Print($"[Registry:{RegistryName}] 注册表已清空。");
		}
	}
}
