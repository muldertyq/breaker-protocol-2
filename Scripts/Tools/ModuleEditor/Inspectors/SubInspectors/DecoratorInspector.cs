#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using BreakerProtocol.Data.Models;
using BreakerProtocol.Data.Models.Properties;

namespace BreakerProtocol.Tools.ModuleEditor.Inspectors.SubInspectors
{
	public partial class DecoratorInspector : VBoxContainer
	{
		private MultiSelectMenu _weaponTagsField = null!;
		private MultiSelectMenu _deliveryTypesField = null!;
		private MultiSelectMenu _targetTagsField = null!;

		private ItemList _effectList = null!;
		private PanelContainer _effectDetailCard = null!;
		private LineEdit _effectIdInput = null!;
		private LineEdit _effectNameInput = null!;
		private OptionButton _triggerSelect = null!;
		private OptionButton _stackModeSelect = null!;

		private ItemList _modifierList = null!;
		private PanelContainer _modifierDetailCard = null!;
		private OptionButton _attributeSelect = null!;
		private OptionButton _operationSelect = null!;
		private OptionButton _valueTypeSelect = null!;
		private SpinBox _valueInput = null!;

		public event Action? OnValuesChanged;

		private ModuleDataDefinition? _boundData;
		private bool _isUpdating;
		private int _selectedEffectIndex = -1;
		private int _selectedModifierIndex = -1;

		private static readonly string[] TriggerNames =
		{
			"持续生效 (Passive)",
			"开火时 (OnFire)",
			"命中时 (OnHit)",
			"周期触发 (Interval)"
		};

		private static readonly string[] StackModeNames =
		{
			"数值相加 (Additive)",
			"仅取最高 (Highest)",
			"独立判定 (Independent)"
		};

		private static readonly string[] AttributeNames =
		{
			"武器伤害 (Damage)",
			"武器射速 (FireRate)",
			"单发热量 (HeatPerShot)",
			"武器能耗 (EnergyCost)",
			"弹丸数量 (ProjectileCount)",
			"散射角度 (ScatterAngle)",
			"瘫痪时长 (StunDuration)",
			"移动速度 (MoveSpeed)",
			"状态持续时间 (StatusDuration)",
			"穿透层数 (Pierce)",
			"弹药容量 (AmmoCapacity)",
			"触发间隔 (TriggerInterval)",
			"爆炸概率 (ExplosionChance)",
			"爆炸半径 (ExplosionRadius)",
			"制导转向速度 (GuidanceTurnRate)"
		};

		private static readonly string[] OperationNames =
		{
			"增加 (Increase)",
			"减少 (Decrease)",
			"设定为 (Set)"
		};

		private static readonly string[] ValueTypeNames =
		{
			"固定数值 (Flat)",
			"百分比 (Percent)"
		};

		public override void _Ready()
		{
			BuildUI();
		}

		private void BuildUI()
		{
			AddThemeConstantOverride("separation", 10);

			var (conditionCard, conditionContent) = CreateCard("适用条件", new Color(0.45f, 0.80f, 1.0f));
			AddChild(conditionCard);
			_weaponTagsField = new MultiSelectMenu(conditionContent, "武器标签:", "武器必须同时具备所选标签；不选择表示不限", EmitConditionChange);
			_deliveryTypesField = new MultiSelectMenu(conditionContent, "载荷类型:", "匹配任意一个所选载荷类型；不选择表示不限", EmitConditionChange);
			_targetTagsField = new MultiSelectMenu(conditionContent, "命中目标标签:", "命中目标必须同时具备所选标签；仅对命中类效果有意义", EmitConditionChange);

			var (effectListCard, effectListContent) = CreateCard("效果列表", new Color(0.95f, 0.72f, 0.28f));
			AddChild(effectListCard);
			_effectList = new ItemList
			{
				CustomMinimumSize = new Vector2(0, 96),
				SelectMode = ItemList.SelectModeEnum.Single,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_effectList.ItemSelected += index => SelectEffect((int)index);
			effectListContent.AddChild(_effectList);
			effectListContent.AddChild(CreateAddRemoveRow("添加效果", AddEffect, "删除效果", RemoveEffect));

			var (effectDetailCard, effectDetailContent) = CreateCard("当前效果", new Color(0.95f, 0.55f, 0.35f));
			_effectDetailCard = effectDetailCard;
			AddChild(_effectDetailCard);
			_effectIdInput = CreateTextRow(effectDetailContent, "效果 ID:", "稳定的内部标识", UpdateCurrentEffect);
			_effectNameInput = CreateTextRow(effectDetailContent, "效果名称:", "编辑器显示名称", UpdateCurrentEffect);
			_triggerSelect = CreateOptionRow(effectDetailContent, "触发时机:", TriggerNames, UpdateCurrentEffect);
			_stackModeSelect = CreateOptionRow(effectDetailContent, "叠加规则:", StackModeNames, UpdateCurrentEffect);

			var (modifierListCard, modifierListContent) = CreateCard("属性修改项", new Color(0.48f, 0.92f, 0.62f));
			AddChild(modifierListCard);
			_modifierList = new ItemList
			{
				CustomMinimumSize = new Vector2(0, 104),
				SelectMode = ItemList.SelectModeEnum.Single,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			_modifierList.ItemSelected += index => SelectModifier((int)index);
			modifierListContent.AddChild(_modifierList);
			modifierListContent.AddChild(CreateAddRemoveRow("添加属性", AddModifier, "删除属性", RemoveModifier));

			var (modifierDetailCard, modifierDetailContent) = CreateCard("当前属性", new Color(0.70f, 0.82f, 1.0f));
			_modifierDetailCard = modifierDetailCard;
			AddChild(_modifierDetailCard);
			_attributeSelect = CreateOptionRow(modifierDetailContent, "目标属性:", AttributeNames, UpdateCurrentModifier);
			_operationSelect = CreateOptionRow(modifierDetailContent, "运算方式:", OperationNames, UpdateCurrentModifier);
			_valueTypeSelect = CreateOptionRow(modifierDetailContent, "数值类型:", ValueTypeNames, UpdateCurrentModifier);
			_valueInput = CreateNumberRow(modifierDetailContent, "数值:", 0, 100000, 0.1, UpdateCurrentModifier);

			_effectDetailCard.Visible = false;
			_modifierDetailCard.Visible = false;
		}

		public void SetFilterOptions(IEnumerable<string> weaponTags, IEnumerable<string> deliveryTypes, IEnumerable<string> targetTags)
		{
			var properties = GetProperties();
			_weaponTagsField.SetOptions(weaponTags, properties.ApplicableWeaponTags);
			_deliveryTypesField.SetOptions(deliveryTypes, properties.ApplicableDeliveryTypes);
			_targetTagsField.SetOptions(targetTags, properties.RequiredTargetTags);
		}

		public void BindData(ModuleDataDefinition data)
		{
			_boundData = data;
			_isUpdating = true;
			var properties = GetProperties();
			_weaponTagsField.SetSelected(properties.ApplicableWeaponTags);
			_deliveryTypesField.SetSelected(properties.ApplicableDeliveryTypes);
			_targetTagsField.SetSelected(properties.RequiredTargetTags);
			PopulateEffectList(properties);
			_isUpdating = false;
		}

		private DecoratorProperties GetProperties()
		{
			var properties = _boundData?.GetProperties<DecoratorProperties>() ?? new DecoratorProperties();
			properties.ApplicableWeaponTags ??= Array.Empty<string>();
			properties.ApplicableDeliveryTypes ??= Array.Empty<string>();
			properties.RequiredTargetTags ??= Array.Empty<string>();
			properties.Effects ??= Array.Empty<DecoratorEffectDefinition>();
			foreach (var effect in properties.Effects)
			{
				effect.Modifiers ??= Array.Empty<DecoratorModifierDefinition>();
			}
			return properties;
		}

		private void SaveProperties(DecoratorProperties properties)
		{
			if (_boundData == null) return;
			_boundData.Properties = JsonSerializer.SerializeToElement(properties);
			OnValuesChanged?.Invoke();
		}

		private void EmitConditionChange()
		{
			if (_isUpdating || _boundData == null) return;
			var properties = GetProperties();
			properties.ApplicableWeaponTags = _weaponTagsField.GetSelected();
			properties.ApplicableDeliveryTypes = _deliveryTypesField.GetSelected();
			properties.RequiredTargetTags = _targetTagsField.GetSelected();
			SaveProperties(properties);
		}

		private void PopulateEffectList(DecoratorProperties properties, int preferredIndex = -1)
		{
			_effectList.Clear();
			foreach (var effect in properties.Effects)
			{
				_effectList.AddItem($"{effect.Name}  [{effect.Trigger}]");
			}

			if (properties.Effects.Length == 0)
			{
				_selectedEffectIndex = -1;
				_selectedModifierIndex = -1;
				_effectDetailCard.Visible = false;
				_modifierList.Clear();
				_modifierDetailCard.Visible = false;
				return;
			}

			int target = Mathf.Clamp(preferredIndex >= 0 ? preferredIndex : _selectedEffectIndex, 0, properties.Effects.Length - 1);
			_effectList.Select(target);
			SelectEffect(target);
		}

		private void SelectEffect(int index)
		{
			var properties = GetProperties();
			if (index < 0 || index >= properties.Effects.Length)
			{
				_effectDetailCard.Visible = false;
				return;
			}

			_selectedEffectIndex = index;
			var effect = properties.Effects[index];
			_isUpdating = true;
			_effectIdInput.Text = effect.EffectId;
			_effectNameInput.Text = effect.Name;
			SelectOption(_triggerSelect, DecoratorTriggers.All, effect.Trigger);
			SelectOption(_stackModeSelect, DecoratorStackModes.All, effect.StackMode);
			_effectDetailCard.Visible = true;
			PopulateModifierList(effect);
			_isUpdating = false;
		}

		private void UpdateCurrentEffect()
		{
			if (_isUpdating || _boundData == null || _selectedEffectIndex < 0) return;
			var properties = GetProperties();
			if (_selectedEffectIndex >= properties.Effects.Length) return;

			var effect = properties.Effects[_selectedEffectIndex];
			effect.EffectId = _effectIdInput.Text.Trim();
			effect.Name = _effectNameInput.Text.Trim();
			effect.Trigger = GetSelectedValue(_triggerSelect, DecoratorTriggers.All);
			effect.StackMode = GetSelectedValue(_stackModeSelect, DecoratorStackModes.All);
			_effectList.SetItemText(_selectedEffectIndex, $"{effect.Name}  [{effect.Trigger}]");
			SaveProperties(properties);
		}

		private void AddEffect()
		{
			if (_boundData == null) return;
			var properties = GetProperties();
			var effects = new List<DecoratorEffectDefinition>(properties.Effects);
			string effectId = CreateUniqueEffectId(effects);
			effects.Add(new DecoratorEffectDefinition
			{
				EffectId = effectId,
				Name = "新效果",
				Modifiers = new[] { new DecoratorModifierDefinition { Value = 1 } }
			});
			properties.Effects = effects.ToArray();
			SaveProperties(properties);
			PopulateEffectList(properties, effects.Count - 1);
		}

		private void RemoveEffect()
		{
			if (_boundData == null || _selectedEffectIndex < 0) return;
			var properties = GetProperties();
			var effects = new List<DecoratorEffectDefinition>(properties.Effects);
			if (_selectedEffectIndex >= effects.Count) return;
			effects.RemoveAt(_selectedEffectIndex);
			properties.Effects = effects.ToArray();
			SaveProperties(properties);
			PopulateEffectList(properties, Math.Min(_selectedEffectIndex, effects.Count - 1));
		}

		private void PopulateModifierList(DecoratorEffectDefinition effect, int preferredIndex = -1)
		{
			_modifierList.Clear();
			foreach (var modifier in effect.Modifiers)
			{
				_modifierList.AddItem(FormatModifier(modifier));
			}

			if (effect.Modifiers.Length == 0)
			{
				_selectedModifierIndex = -1;
				_modifierDetailCard.Visible = false;
				return;
			}

			int target = Mathf.Clamp(preferredIndex >= 0 ? preferredIndex : _selectedModifierIndex, 0, effect.Modifiers.Length - 1);
			_modifierList.Select(target);
			SelectModifier(target);
		}

		private void SelectModifier(int index)
		{
			var effect = GetSelectedEffect();
			if (effect == null || index < 0 || index >= effect.Modifiers.Length)
			{
				_modifierDetailCard.Visible = false;
				return;
			}

			_selectedModifierIndex = index;
			var modifier = effect.Modifiers[index];
			_isUpdating = true;
			SelectOption(_attributeSelect, DecoratorAttributes.All, modifier.Attribute);
			SelectOption(_operationSelect, DecoratorModifierOperations.All, modifier.Operation);
			SelectOption(_valueTypeSelect, DecoratorValueTypes.All, modifier.ValueType);
			_valueInput.Value = modifier.Value;
			_modifierDetailCard.Visible = true;
			_isUpdating = false;
		}

		private void UpdateCurrentModifier()
		{
			if (_isUpdating || _boundData == null || _selectedEffectIndex < 0 || _selectedModifierIndex < 0) return;
			var properties = GetProperties();
			if (_selectedEffectIndex >= properties.Effects.Length) return;
			var effect = properties.Effects[_selectedEffectIndex];
			if (_selectedModifierIndex >= effect.Modifiers.Length) return;

			var modifier = effect.Modifiers[_selectedModifierIndex];
			modifier.Attribute = GetSelectedValue(_attributeSelect, DecoratorAttributes.All);
			modifier.Operation = GetSelectedValue(_operationSelect, DecoratorModifierOperations.All);
			modifier.ValueType = GetSelectedValue(_valueTypeSelect, DecoratorValueTypes.All);
			modifier.Value = (float)_valueInput.Value;
			_modifierList.SetItemText(_selectedModifierIndex, FormatModifier(modifier));
			SaveProperties(properties);
		}

		private void AddModifier()
		{
			if (_boundData == null || _selectedEffectIndex < 0) return;
			var properties = GetProperties();
			if (_selectedEffectIndex >= properties.Effects.Length) return;
			var effect = properties.Effects[_selectedEffectIndex];
			var modifiers = new List<DecoratorModifierDefinition>(effect.Modifiers)
			{
				new() { Value = 1 }
			};
			effect.Modifiers = modifiers.ToArray();
			SaveProperties(properties);
			PopulateModifierList(effect, modifiers.Count - 1);
		}

		private void RemoveModifier()
		{
			if (_boundData == null || _selectedEffectIndex < 0 || _selectedModifierIndex < 0) return;
			var properties = GetProperties();
			if (_selectedEffectIndex >= properties.Effects.Length) return;
			var effect = properties.Effects[_selectedEffectIndex];
			var modifiers = new List<DecoratorModifierDefinition>(effect.Modifiers);
			if (_selectedModifierIndex >= modifiers.Count) return;
			modifiers.RemoveAt(_selectedModifierIndex);
			effect.Modifiers = modifiers.ToArray();
			SaveProperties(properties);
			PopulateModifierList(effect, Math.Min(_selectedModifierIndex, modifiers.Count - 1));
		}

		private DecoratorEffectDefinition? GetSelectedEffect()
		{
			var properties = GetProperties();
			return _selectedEffectIndex >= 0 && _selectedEffectIndex < properties.Effects.Length
				? properties.Effects[_selectedEffectIndex]
				: null;
		}

		private static string CreateUniqueEffectId(IEnumerable<DecoratorEffectDefinition> effects)
		{
			var existingIds = effects.Select(effect => effect.EffectId).ToHashSet(StringComparer.OrdinalIgnoreCase);
			int index = existingIds.Count;
			while (existingIds.Contains($"effect_{index}")) index++;
			return $"effect_{index}";
		}

		private static string FormatModifier(DecoratorModifierDefinition modifier)
		{
			string sign = modifier.Operation switch
			{
				DecoratorModifierOperations.Increase => "+",
				DecoratorModifierOperations.Decrease => "-",
				_ => "="
			};
			string suffix = modifier.ValueType == DecoratorValueTypes.Percent ? "%" : string.Empty;
			return $"{modifier.Attribute}  {sign}{modifier.Value:G}{suffix}";
		}

		private static string GetSelectedValue(OptionButton option, string[] values)
		{
			return values[Mathf.Clamp(option.Selected, 0, values.Length - 1)];
		}

		private static void SelectOption(OptionButton option, string[] values, string target)
		{
			int index = Array.FindIndex(values, value => value.Equals(target, StringComparison.OrdinalIgnoreCase));
			option.Select(index >= 0 ? index : 0);
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
			var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			content.AddThemeConstantOverride("separation", 6);
			var header = new Label { Text = title };
			header.AddThemeColorOverride("font_color", accentColor);
			header.AddThemeFontSizeOverride("font_size", 13);
			content.AddChild(header);
			content.AddChild(new HSeparator());
			panel.AddChild(content);
			return (panel, content);
		}

		private static HBoxContainer CreateAddRemoveRow(string addTooltip, Action addAction, string removeTooltip, Action removeAction)
		{
			var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
			var addButton = new Button { Text = "+", TooltipText = addTooltip, CustomMinimumSize = new Vector2(36, 30) };
			var removeButton = new Button { Text = "−", TooltipText = removeTooltip, CustomMinimumSize = new Vector2(36, 30) };
			addButton.Pressed += addAction;
			removeButton.Pressed += removeAction;
			row.AddChild(addButton);
			row.AddChild(removeButton);
			return row;
		}

		private static LineEdit CreateTextRow(Control parent, string labelText, string placeholder, Action changed)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var input = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			input.TextChanged += _ => changed();
			row.AddChild(input);
			parent.AddChild(row);
			return input;
		}

		private static OptionButton CreateOptionRow(Control parent, string labelText, string[] items, Action changed)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			for (int i = 0; i < items.Length; i++) option.AddItem(items[i], i);
			option.ItemSelected += _ => changed();
			row.AddChild(option);
			parent.AddChild(row);
			return option;
		}

		private static SpinBox CreateNumberRow(Control parent, string labelText, double min, double max, double step, Action changed)
		{
			var row = new HBoxContainer();
			row.AddChild(new Label { Text = labelText, CustomMinimumSize = new Vector2(110, 0) });
			var input = new SpinBox
			{
				MinValue = min,
				MaxValue = max,
				Step = step,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			input.ValueChanged += _ => changed();
			row.AddChild(input);
			parent.AddChild(row);
			return input;
		}

	}
}
