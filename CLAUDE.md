# CLAUDE.md · chinese-grocery

2D 俯视角像素风模拟经营游戏：华人二代接手父母的杂货店，做成大型华人超市。
需求以 `spec.md` 为准。开始任何任务前，先读 `spec.md` 里相关的章节。

## 你的角色
你是这个项目唯一的程序员。Jerry 是设计者和产品负责人，不手写代码。
- 设计问题（玩法规则、数值、文案）由 Jerry 决定。spec 没写清楚时，不要自己发明规则：在 `spec.md` 第 9 节"决策记录"追加问题和选项，然后停下来问 Jerry。
- 技术问题（代码结构、实现方式）由你决定，按本文件的规则来。

## 技术栈
- Godot 4（.NET 版）+ C#
- 游戏规则写在纯 C# 类库里，用 xUnit 测试
- 数据表是 JSON，翻译是 Godot CSV

## 目录结构
```
/src/Sim            纯 C# 类库：全部规则和状态，不引用 Godot
/tests/Sim.Tests    xUnit 测试
/tools/SimRunner    命令行：无画面跑 N 天模拟并输出报表
/game               Godot 项目：画面、输入、UI
/game/locale        strings.csv（keys,en,zh）
/data               JSON 数据表
spec.md             技术规格（需求来源）
```

## 常用命令
```bash
dotnet build                                   # 构建全部
dotnet test                                    # 跑全部测试
dotnet run --project tools/SimRunner -- --days 30 --seed 1   # 无画面模拟
$GODOT --headless --path game --build-solutions --quit       # 检查 Godot 项目能否构建（$GODOT = Godot .NET 可执行文件路径）
```

## 架构规则（必须遵守）
1. **Sim 不引用 Godot。** `/src/Sim` 里不能出现 `using Godot`。画面层只读 Sim 状态、向 Sim 发命令。
2. **确定性。** 随机数只用注入的带种子 RNG。逻辑里禁止 `DateTime.Now`、`Random.Shared`、`Guid.NewGuid()`。同种子 + 同命令 = 同结果。
3. **固定步长。** Sim 按 tick 推进；暂停、加速、现实时间换算都在画面层处理。
4. **钱用 `long` 美分。** 不用 float / double / decimal 表示金额。
5. **一套任务系统。** 员工和主角执行同一套任务逻辑；主角只是不自动领任务。不要给主角另写一套。
6. **数值不写死。** 平衡数值全部来自 `/data`。设计未定的数值用占位值并加 `"_tbd": true`。
7. **文字不写死。** Sim 和 UI 代码里不出现中文或英文显示文字，只用翻译键。新增键时同时填 `en` 和 `zh` 两列。
8. **不做 spec 范围外的功能。** spec 第 3 节"不包含"里的内容不要实现，也不要预留代码。

## 工作流程
1. 读 spec 相关章节，确认要做什么。
2. Sim 层的功能：先写测试，再写实现。
3. 每次改动后运行 `dotnet build` 和 `dotnet test`；涉及经济或顾客的改动，再跑一次 SimRunner 看报表有没有异常（负数、钱对不上、库存为负）。
4. 改动 Godot 场景后，跑一次 Godot 无界面构建检查。
5. 小步提交，一个提交做一件事，提交信息用英文写清楚做了什么。
6. 完成一个里程碑后，向 Jerry 汇报：做了什么、怎么验证、有哪些待他决定的问题。
7. 改动了画面的任务，必须启动游戏查看并截图（保存到 /docs/screenshots/），才算完成。

## 代码风格
- C#：开启 nullable，file-scoped namespace，数据类型优先用 `record`。
- 代码、注释、提交信息、翻译键用英文；和 Jerry 沟通用中文。
- 翻译键格式：`<类别>.<id>.<字段>`，如 `product.soy_sauce_light.name`、`ui.hud.cash`。
- 类和文件一一对应，按系统分文件夹（Time、Grid、Inventory、Customers、Tasks、Staff、Meat、Economy、Save）。

## Godot 设置
- 像素完美：纹理过滤用 Nearest，整数倍缩放，关闭抗锯齿。
- 格子 32×32 像素。基础分辨率先用 640×360，整数倍放大（以后可调）。
- 场景和资源保持文本格式（.tscn / .tres），方便阅读和修改。
- 美术使用 /game/assets/sprites/ 里的过渡素材；需要新图时在决策记录里提出，不要自己从网上下载美术或音频。

## 不要做的事
- 不要修改 `/data` 里的平衡数值，除非 Jerry 要求。
- 不要在没问 Jerry 的情况下新增 NuGet 包或第三方插件。
- 不要提交 `.godot/`、`bin/`、`obj/` 等生成文件。
- 不要删除或跳过失败的测试来让测试通过。
