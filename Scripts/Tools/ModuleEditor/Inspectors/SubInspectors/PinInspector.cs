using System;
using System.Collections.Generic;
using Godot;
using BreakerProtocol.Data.Models;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class PinInspector : VBoxContainer
	{
		private ItemList _pinList = null!;
		private VBoxContainer _pinDetailBox = null!;
		private Label _pinIdLabel = null!;
		private OptionButton _categorySelect = null!;
		private OptionButton _typeSelect = null!;
		private Label _locationLabel = null!;
		private Button _deletePinBtn = null!;

		public event Action? OnValuesChanged;
		public event Action<int>? OnPinSelectedInInspector;

		private ModuleDataDefinition? _boundData;
		private int _selectedPinIndex = -1;
		private bool _isUpdating = false;

		private static readonly string[] Categories = {
			"Universal",    // 🌐 通用管线 (直桥/跨线芯片自适应接入)
			"PulsePower",   // ⚡ 高能电脉冲
			"HeavyPulse",   // 🔮 重压强电
			"Thermal",      // 🔥 热力排热
			"Logic"         // 💡 逻辑信号
		};

		private static readonly string[] CategoryDisplayNames = {
			"🌐 通用自适应 (Universal)",
			"⚡ 高能电 (PulsePower)",
			"🔮 重脉冲 (HeavyPulse)",
			"🔥 热力排热 (Thermal)",
			"💡 逻辑信号 (Logic)"
		};

		private static readonly string[] Types = { "IN", "OUT" };
		private static readonly string[] TypeDisplayNames = { "📥 IN (输入端)", "📤 OUT (输出端)" };

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			// 统一卡片化容器
			var (card, content) = CreateCard("⚡ 引脚与端口拓扑 (Pins)", new Color(0.95f, 0.85f, 0.35f));
			AddChild(card);

			// 引脚概览列表（优化暗色微透背景与圆角内边距）
			_pinList = new ItemList
			{
				CustomMinimumSize = new Vector2(0, 100),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				SelectMode = ItemList.SelectModeEnum.Single
			};
			var listStyle = new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.09f, 0.12f, 0.85f),
				BorderColor = new Color(0.20f, 0.24f, 0.32f, 0.7f),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 4,
				CornerRadiusBottomRight = 4,
				CornerRadiusTopLeft = 4,
				CornerRadiusTopRight = 4,
				ContentMarginBottom = 4,
				ContentMarginLeft = 6,
				ContentMarginRight = 6,
				ContentMarginTop = 4
			};
			_pinList.AddThemeStyleboxOverride("panel", listStyle);
			_pinList.ItemSelected += OnPinListSelected;
			content.AddChild(_pinList);

			// 详情配置面板
			_pinDetailBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_pinDetailBox.AddThemeConstantOverride("separation", 6);
			content.AddChild(_pinDetailBox);

			// 只读端口 ID
			var idHBox = new HBoxContainer();
			idHBox.AddChild(new Label { Text = "端口 ID:", CustomMinimumSize = new Vector2(100, 0) });
			_pinIdLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_pinIdLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.75f));
			idHBox.AddChild(_pinIdLabel);
			_pinDetailBox.AddChild(idHBox);

			// 通道类别与流向
			_categorySelect = CreateOptionRowToParent(_pinDetailBox, "通道类别:", CategoryDisplayNames);
			_typeSelect = CreateOptionRowToParent(_pinDetailBox, "端口流向:", TypeDisplayNames);

			// 只读物理位置
			var locHBox = new HBoxContainer();
			locHBox.AddChild(new Label { Text = "物理位置:", CustomMinimumSize = new Vector2(100, 0) });
			_locationLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			_locationLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1.0f));
			locHBox.AddChild(_locationLabel);
			_pinDetailBox.AddChild(locHBox);

			// 删除按钮
			_deletePinBtn = new Button
			{
				Text = "🗑️ 删除当前引脚 (或画布右键)",
				CustomMinimumSize = new Vector2(0, 28)
			};
			_deletePinBtn.Pressed += DeleteCurrentPin;
			_pinDetailBox.AddChild(_deletePinBtn);

			_pinDetailBox.Visible = false;
		}

		public void BindData(ModuleDataDefinition data, int selectIndex = -1)
		{
			_boundData = data;
			_isUpdating = true;

			RefreshPinList();

			if (data.Pins != null && data.Pins.Length > 0)
			{
				if (selectIndex >= 0 && selectIndex < data.Pins.Length)
				{
					_selectedPinIndex = selectIndex;
				}
				else if (_selectedPinIndex < 0 || _selectedPinIndex >= data.Pins.Length)
				{
					_selectedPinIndex = 0;
				}

				if (_selectedPinIndex < _pinList.ItemCount)
				{
					_pinList.Select(_selectedPinIndex);
				}
				BindSelectedPinDetail();
				_pinDetailBox.Visible = true;
			}
			else
			{
				_selectedPinIndex = -1;
				_pinDetailBox.Visible = false;
			}

			_isUpdating = false;
		}

		public void SelectPinExternal(int index)
		{
			if (_boundData?.Pins == null || index < 0 || index >= _boundData.Pins.Length)
			{
				_selectedPinIndex = -1;
				_pinDetailBox.Visible = false;
				_pinList.DeselectAll();
				return;
			}

			_isUpdating = true;
			_selectedPinIndex = index;
			RefreshPinList();
			if (_selectedPinIndex < _pinList.ItemCount)
			{
				_pinList.Select(_selectedPinIndex);
				_pinList.EnsureCurrentIsVisible();
			}
			BindSelectedPinDetail();
			_pinDetailBox.Visible = true;
			_isUpdating = false;
		}

		private void RefreshPinList()
		{
			_pinList.Clear();
			if (_boundData?.Pins == null) return;

			for (int i = 0; i < _boundData.Pins.Length; i++)
			{
				var p = _boundData.Pins[i];
				string tag = p.Category switch
				{
					"Universal" => "🌐 通用",
					"Thermal" => "🔥 热力",
					"HeavyPulse" => "🔮 重压",
					"Logic" => "💡 逻辑",
					_ => "⚡ 脉冲"
				};
				string flowSymbol = p.Type == "IN" ? "📥" : "📤";
				_pinList.AddItem($"{flowSymbol} [{tag}] {p.PinId} ({p.Type} @ {p.LocalGridX},{p.LocalGridY}:{p.Edge})");
			}
		}

		private void BindSelectedPinDetail()
		{
			if (_boundData?.Pins == null || _selectedPinIndex < 0 || _selectedPinIndex >= _boundData.Pins.Length) return;
			var pin = _boundData.Pins[_selectedPinIndex];

			_pinIdLabel.Text = pin.PinId;
			SelectOptionByText(_categorySelect, pin.Category, Categories);
			SelectOptionByText(_typeSelect, pin.Type, Types);

			string edgeName = pin.Edge switch
			{
				"Top" => "上边缘 (Top)",
				"Bottom" => "下边缘 (Bottom)",
				"Left" => "左边缘 (Left)",
				_ => "右边缘 (Right)"
			};
			_locationLabel.Text = $"单元格 ({pin.LocalGridX}, {pin.LocalGridY}) · {edgeName}";
		}

		private void OnPinListSelected(long index)
		{
			_selectedPinIndex = (int)index;
			BindSelectedPinDetail();
			_pinDetailBox.Visible = true;
			OnPinSelectedInInspector?.Invoke(_selectedPinIndex);
		}

		private void EmitChange()
		{
			if (_isUpdating || _boundData == null || _selectedPinIndex < 0 || _selectedPinIndex >= _boundData.Pins.Length) return;

			var pin = _boundData.Pins[_selectedPinIndex];
			pin.Category = Categories[_categorySelect.Selected];
			pin.Type = Types[_typeSelect.Selected];

			pin.PinId = $"{pin.Category.ToLower()}_{pin.Type.ToLower()}_{pin.LocalGridX}_{pin.LocalGridY}_{pin.Edge.ToLower()}";
			_pinIdLabel.Text = pin.PinId;

			RefreshPinList();
			if (_selectedPinIndex < _pinList.ItemCount)
			{
				_pinList.Select(_selectedPinIndex);
			}
			OnValuesChanged?.Invoke();
		}

		private void DeleteCurrentPin()
		{
			if (_boundData?.Pins == null || _selectedPinIndex < 0 || _selectedPinIndex >= _boundData.Pins.Length) return;

			var list = new List<PinDefinition>(_boundData.Pins);
			list.RemoveAt(_selectedPinIndex);
			_boundData.Pins = list.ToArray();

			_selectedPinIndex = list.Count > 0 ? Mathf.Clamp(_selectedPinIndex - 1, 0, list.Count - 1) : -1;
			BindData(_boundData, _selectedPinIndex);
			OnValuesChanged?.Invoke();
		}

		private (PanelContainer card, VBoxContainer content) CreateCard(string title, Color accentColor)
		{
			var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.11f, 0.13f, 0.17f, 0.95f),
				BorderColor = new Color(0.22f, 0.26f, 0.35f, 0.8f),
				BorderWidthBottom = 1,
				BorderWidthLeft = 1,
				BorderWidthRight = 1,
				BorderWidthTop = 1,
				CornerRadiusBottomLeft = 6,
				CornerRadiusBottomRight = 6,
				CornerRadiusTopLeft = 6,
				CornerRadiusTopRight = 6,
				ContentMarginBottom = 8,
				ContentMarginLeft = 10,
				ContentMarginRight = 10,
				ContentMarginTop = 8
			};
			panel.AddThemeStyleboxOverride("panel", style);

			var vbox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			vbox.AddThemeConstantOverride("separation", 6);

			var header = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Left };
			header.AddThemeColorOverride("font_color", accentColor);
			header.AddThemeFontSizeOverride("font_size", 13);
			vbox.AddChild(header);
			vbox.AddChild(new HSeparator());

			panel.AddChild(vbox);
			return (panel, vbox);
		}

		private OptionButton CreateOptionRowToParent(Control parent, string labelText, string[] displayItems)
		{
			var hbox = new HBoxContainer();
			hbox.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(100, 0) });
			var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			for (int i = 0; i < displayItems.Length; i++) opt.AddItem(displayItems[i], i);
			opt.ItemSelected += _ => EmitChange();
			hbox.AddChild(opt);
			parent.AddChild(hbox);
			return opt;
		}

		private void SelectOptionByText(OptionButton opt, string val, string[] keys)
		{
			for (int i = 0; i < keys.Length; i++)
			{
				if (keys[i].Equals(val, StringComparison.OrdinalIgnoreCase))
				{
					opt.Select(i);
					return;
				}
			}
			opt.Select(0);
		}
	}
}
