# chinese-grocery 过渡美术包 v2

所有图都是 32×32 PNG，统一调色板，由 `make_sprites.py` 生成。改图请改脚本后重新运行：

```bash
python3 make_sprites.py      # 输出到 sprites/，并生成预览图
```

放进项目：`sprites/` → `/game/assets/sprites/`，脚本 → `/tools/art/`。v1 的文件名全部保留，可以直接覆盖。

## 新增内容（v2）

**角色行走帧**：每个角色 6 帧，命名 `char_<名字>_<front|back>_<0|1|2>`
- `front` 正面（朝下走），`back` 背面（朝上走）
- `0` 站立，`1`、`2` 左右脚交替。行走循环：0 → 1 → 0 → 2
- 横向走：暂时用正面帧 + 水平翻转
- 角色：`protagonist` 主角、`butcher` 切肉师傅、`auntie` 阿姨叔叔、`student` 留学生、`family` 华人家庭（拿购物篮）、`dad` 父亲、`mom` 母亲（父母供以后剧情用）
- 旧文件 `char_<名字>.png` 仍保留，等于 `front_0`

**设施**：`produce_stand_empty / full / full_b`（果蔬架）、`freezer`（冷冻柜）

**装饰**：`deco_fu_poster`（福字斗方，挂墙）、`deco_plant`（盆栽）、`deco_basket`（购物篮）、`deco_mat`（门口地垫）

**商品**：
- 干货 / 冷藏冷冻：`sesame_oil` 香油、`oyster_sauce` 蚝油、`vermicelli` 米粉、`dumplings_frozen` 速冻饺子、`tofu` 豆腐、`red_dates` 红枣
- 果蔬：`napa_cabbage` 大白菜、`bok_choy` 小白菜、`scallion` 葱、`ginger` 姜、`orange` 橙子、`lychee` 荔枝、`daikon` 白萝卜

**UI 图标**：`ui_coin` 金币、`ui_clock` 时钟、`ui_star` 星星、`ui_bubble_question` / `frown` / `angry` / `heart`（顾客气泡）

## 注意
- 果蔬、冷冻商品属于后续里程碑的内容，图先备好；在 spec 把它们纳入范围之前，代码里不要使用。
- 华人家庭顾客已有专属形象，可以取代 M7 里"阿姨换色"的临时方案。

## 新增内容（v3 · 海鲜部）

**设施**：`fish_tank`（活鱼池，清水）/ `fish_tank_murky`（水质差，水发绿、有病鱼）/ `fish_tank_empty`、`ice_table` / `ice_table_empty`（冰台）、`seafood_counter`（海鲜柜台，带秤和砧板）

**商品**：`live_fish` 活鱼、`chilled_fish` 冰鲜鱼、`live_shrimp` 活虾、`crab` 螃蟹（绑草绳）、`lobster` 龙虾（钳子套皮筋）、`clams` 蛤蜊

**角色**：`fishmonger` 海鲜师傅（蓝帽、胶皮围裙、水鞋），同样有正面、背面各 3 帧

**UI 图标**：`ui_water_good` / `ui_water_bad`（鱼池水质）

预览：`preview_seafood_4x.png`
