# GameplayAbilitySystem-in-Unity

Unity Gameplay Ability System (GAS) 实现，基于 Unreal Engine 5 的 GAS 设计理念进行开发。

## 项目概述

这是一个完整的 Unity 游戏能力系统框架，实现了类似于 Unreal Engine 5 中 Gameplay Ability System (GAS) 的核心功能。系统提供了强大的能力(Ability)、效果(Effect)和标签(Tag)管理机制，可用于构建复杂的 RPG、ARPG 或技能系统。

本框架已被应用于同人游戏《少女前线：攻性协议》中。**游戏演示和发布页**: [B站视频](https://www.bilibili.com/video/BV1ZePtzqEtF/)

## 功能特性

### 核心系统

- **AbilitySystemComponent (ASC)**: 核心组件，挂载在游戏对象上管理所有能力和效果
- **GameplayAbility**: 技能/能力基类，支持多种实例化策略和触发方式
- **GameplayEffect**: 效果定义，用于修改属性、添加状态等
- **GameplayTag**: 标签系统，用于条件判断和状态管理

### 能力系统 (GameplayAbility)

- **实例化策略**: 非实例化、每角色实例化
- **触发方式**: 
  - GameplayEvent 事件触发
  - OnTagAdd 标签添加触发
  - OnTagPresent 标签存在触发
- **技能标签**: abilityTags、cancelAbilitiesWithTags、blockAbilitiesWithTags、activationOwnedTags、activationRequiredTags、activationBlockedTags
- **冷却与消耗**: 冷却时间、施放费用支持
- **执行次数限制**: 支持限制技能激活次数

### 效果系统 (GameplayEffect)

- **持续时间**: 即时、限时、永久
- **周期效果**: 支持周期性生效
- **叠加机制**: 
  - 叠加类型: 无限制、按源叠加、按目标叠加
  - 失效策略: 清除全部、单层移除并刷新、仅刷新持续时间
  - （可选）根据叠加层数让属性修饰器效果倍增
- **属性修改**: 支持多种数值修改方式
- **自定义执行**: 支持自定义效果计算逻辑

### 标签系统 (EGF)

- **GameplayTag**: 层级标签，支持类似 `Character.Health` 格式
- **GameplayTagContainer**: 标签容器，支持查询和匹配
- **编辑器工具**: 可视化标签编辑器
- 基于整数哈希和前缀树匹配实现高效查询

### 属性系统

- **GameplayAttributeSet**: 属性集定义
- **AttributeRef**: 属性引用，支持直接修改和捕获
- **数值计算**: 支持基础值修改、百分比修改、浮动修改等

## 项目结构

```
Assets/
├── GAS/
│   ├── EGF_GameplayTag/          # 标签系统
│   │   ├── Scripts/              # 核心标签逻辑
│   │   ├── Editor/               # 标签编辑器
│   │   └── Resources/            # 标签数据资源
│   └── GameplayAbilitySystem/    # 能力系统
│       ├── GameplayAbility/      # 能力相关
│       ├── GameplayAttribute/    # 属性系统
│       ├── GameplayEffect/       # 效果系统
│       ├── GameplayCue/          # 表现系统
│       ├── Example/              # 示例代码
│       └── Editor/               # 编辑器工具
└── Scenes/                       # 场景文件
```

## 快速开始

### 环境要求

- Unity 2022.3+
- .NET Framework 4.7.1
- C# 9.0

### 基本使用

1. **创建 AbilitySystemComponent**
   - 在场景中创建空对象或角色对象
   - 添加 `AbilitySystemComponent` 组件

2. **创建属性集**
   - 继承 `GameplayAttributeSet` 创建自定义属性集
   - 在 ASC 中注册属性集

3. **创建技能**
   - 继承 `GameplayAbility` 创建自定义技能
   - 实现 `ActivateAbility()` 和 `EndAbility()` 方法
   - 创建 ScriptableObject 资源

4. **创建效果**
   - 使用菜单创建 `GameplayEffect`
   - 配置持续时间、属性修改、叠加规则等

5. **激活技能**
   ```csharp
   // 通过 ASC 激活技能
   abilitySystemComponent.TryActivateAbility(abilitySpecHandle);
   ```

## 示例代码

参考 `Assets/GAS/GameplayAbilitySystem/Example/` 目录下的示例代码：

- `GameplayAbilityTestAndExample.cs`: 能力系统使用示例
- `GameplayEffectTestAndExample.cs`: 效果系统使用示例
- `GameplayAttributeSetExample.cs`: 属性集示例

## 技术细节

### 关键类

| 类名 | 说明 |
|------|------|
| AbilitySystemComponent | 能力系统核心组件 |
| GameplayAbility | 能力基类 |
| GameplayEffect | 效果定义 |
| GameplayAttributeSet | 属性集 |
| GameplayTagContainer | 标签容器 |
| GameplayHandle | 对象引用句柄 |

### 事件系统

系统使用事件驱动模式，主要事件包括：

- 标签变更事件 (ONAnyTagAdd, ONAnyTagRemove)
- 技能激活事件 (ONActivateAbility)
- 效果应用事件 (ONApplyGameplayEffectSpec)
- 属性变更时间 (RegisterAttributeValueChangeCallback)

## 已知限制

本框架尚处于开发完善阶段，目前有以下限制：

- **网络同步 (Replication)和预测系统（Prediction）**: 未实现，不适用于需要网络对战的游戏
- **GameplayCue 表现系统**: 未实现
- **SetByCaller**: 未实现
- **数据**: 目前属性数据和属性修饰符能使用预设的固定数值和预设的根据等级动态变化的数据曲线，但不支持从表格读取数据。

### 通过标签系统实现特效触发

虽然未实现专用的Cue系统，但可以通过监听标签变更事件来实现特效触发：

```csharp
// 监听标签添加事件
asc.RegisterOnAnyTagAdd((tag, count) =>
{
    if (tag.Equals(GameplayTagUtils.GetTagHashFromString("Effect.Burn")))
    {
        // 播放燃烧特效
        PlayEffect("VFX_Fire");
    }
});

// 监听标签移除事件
asc.RegisterOnAnyTagRemove((tag, count) =>
{
    if (tag.Equals(GameplayTagUtils.GetTagHashFromString("Effect.Burn")))
    {
        // 停止燃烧特效
        StopEffect("VFX_Fire");
    }
});
```

## 许可证

MIT License
