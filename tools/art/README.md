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

## 新增内容（v4 · 熟食部）

**设施**：`steam_table` / `steam_table_empty`（打菜台，带防喷罩和四格热菜）、`roast_station` / `roast_station_empty`（烧腊档，挂烧鸭和叉烧）、`wok_stove`（厨房炉灶，两口锅）

**菜品图标**（`dish_*`）：`braised_pork` 红烧肉、`sweet_sour_ribs` 糖醋排骨、`dumplings` 饺子、`stir_fried_bok_choy` 清炒小白菜、`steamed_fish` 葱姜蒸鱼、`salt_pepper_shrimp` 椒盐虾、`roast_duck` 烧鸭、`char_siu` 叉烧

**原料**：`product_whole_duck` 整鸭、`product_pork_shoulder` 猪梅肉；`product_takeaway_box` 外卖打包盒

**角色**：`chef` 厨师（高帽、白色厨师服、红领巾），正面、背面各 3 帧

预览：`preview_deli_4x.png`

## 新增内容（v5 · 为 M11–M15 准备）

**员工**（M11）：`cashier` 收银员（扎马尾、红围裙）、`stocker` 理货员（红帽、黄色马甲）、`manager` 店长（深色西装、红领带、眼镜）

**顾客**（M13）：`abc_teen` ABC 青少年（粉色卫衣、耳机）、`local_a` / `local_b` 本地非华人顾客（两种外形）、`restaurant_buyer` 餐馆采购（抱纸箱）

**其他角色**（M12、M14）：`driver` 货运司机、`inspector` 卫生检查员（拿记录板）

以上角色都有正面、背面各 3 帧，命名规则同前。

**剧情对话头像**（M15，64×64）：`portrait_<protagonist|dad|mom>_<normal|happy|worried>`，主角、父亲、母亲各三种表情

**干货**：`doubanjiang` 豆瓣酱、`fermented_tofu` 腐乳、`seaweed` 紫菜、`rice_crackers` 仙贝、`dried_shiitake` 干香菇、`instant_noodles_5pack` 五连包泡面、`soy_milk` 豆奶、`herbal_tea` 凉茶、`vinegar` 香醋、`frozen_buns` 速冻包子

**亚洲特色果蔬**：`a_choy` A 菜、`tong_ho` 茼蒿、`bitter_melon` 苦瓜、`winter_melon` 冬瓜、`chinese_chives` 韭菜、`gai_lan` 芥兰、`mango` 芒果、`dragon_fruit` 火龙果、`persimmon` 柿子、`taro` 芋头、`yangmei` 杨梅、`longan` 龙眼

**节日商品**：`mooncake` 月饼、`mooncake_box` 月饼礼盒、`zongzi` 粽子、`glutinous_rice` 糯米、`red_envelope` 红包、`candy_box` 新年糖果盒、`mandarins` 年桔、`nian_gao` 年糕

**节日装饰**：`deco_couplet_left` / `deco_couplet_right`（春联，挂门两侧）、`deco_festival_banner`（彩旗，可横向拼接）、`deco_rabbit_lantern`（中秋兔子灯）

**物流与其他**：`pallet` 进口货托盘、`product_cardboard_box` 纸箱、`dining_table` 餐桌（以后做用餐区用）

**UI 图标**：`ui_calendar` 日历、`ui_thumb_up` / `ui_thumb_down` 好评差评、`ui_rating` 星级、`ui_warning` 警告、`ui_inspection` 检查、`ui_power_out` 停电、`ui_truck` 货车、`ui_ship` 货船、`ui_phone` 来电、`ui_morale_happy` / `ok` / `sad` 员工士气、`ui_level_up` 升级

预览：`preview_festival_4x.png`、`preview_portraits_3x.png`
