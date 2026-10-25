# M15 台词草稿（待 Jerry 审核）

全部台词都是 agent 写的**草稿**，中英文对照，按场景分组。数据里每段对话都带 `"_draft": true`；
翻译在 `game/locale/strings.csv`（键名见每段标题下）。改台词只需改 CSV 里的中英文两列；
改说话人、表情、触发条件改 `data/dialogue/` 下的 JSON。

占位变量：`{protagonist}` 主角名、`{dad}` 父亲、`{mom}` 母亲（名字待定，目前的占位翻译见文末），`{shop}` 玩家起的店名。

## 开场（18.2）

### 开场 1 · 黑屏字幕

`opening_captions` · 黑屏字幕

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | （字幕） | normal | 那年夏天，我大学毕业了。 | The summer I graduated, I had a degree and big plans. |
| 2 | （字幕） | normal | 投了几十份简历，回音寥寥。 | I sent out dozens of applications. Hardly anyone wrote back. |
| 3 | （字幕） | normal | 倒是学生贷款的账单，每个月都准时寄到。 | The student loan statements, though, arrived right on time. |

### 开场 2 · 母亲来电

`opening_mom_call` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {protagonist} | normal | 喂，妈？ | Hello? Mom? |
| 2 | {mom} | worried | {protagonist}，你爸……前两天在店里晕倒了。 | {protagonist}, your dad... he collapsed at the store two days ago. |
| 3 | {protagonist} | worried | 什么？！他现在怎么样？ | What?! Is he okay? |
| 4 | {mom} | worried | 医生说没大事，但必须好好休养，不能再累着了。 | The doctor says he'll be fine, but he has to rest. No more long days. |
| 5 | {mom} | normal | 你回家一趟吧，我们有事想跟你商量。 | Can you come home? There's something we want to talk about. |

### 开场 3 · 父母退休

`opening_retire` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 这家店，我和你妈开了二十年。 | Your mom and I have run this store for twenty years. |
| 2 | {dad} | worried | 可我这身体，是撑不动了。 | But this body of mine can't keep up anymore. |
| 3 | {mom} | normal | 我们商量好了，准备退休。店……想交给你。 | We've decided to retire. And the store... we'd like you to have it. |
| 4 | {protagonist} | worried | 交给我？我连货都没进过…… | Me? I've never even placed an order... |
| 5 | {dad} | happy | 不会就学。你从小在店里长大，怕什么。 | Then you'll learn. You grew up in this store. What's there to be afraid of? |
| 选项 `accept` | 玩家 | | 好，我接！ | Okay. I'll do it. |
| 选项 `for_now` | 玩家 | | 我先帮你们撑一阵子。 | I'll keep it going for a while, for you. |

### 开场 3a · 选择「我接」

`opening_retire_accept` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | happy | 好孩子。 | That's my kid. |
| 2 | {dad} | happy | 有什么不懂的，随时打电话问我。 | If you get stuck, just call me. |

### 开场 3b · 选择「先撑一阵子」

`opening_retire_for_now` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | normal | 一阵子也好。你肯回来，我们就很高兴了。 | Even a while is enough. We're just glad you came home. |
| 2 | {dad} | normal | 遇到不懂的，随时给我打电话。 | Whenever you're stuck, give me a call. |

### 开场 4 · 给店起名

`opening_name_shop` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 对了，店既然交给你了，招牌也该你来定。 | Oh, and since the store is yours now, the sign is yours to choose. |
| 2 | {mom} | happy | 起个名字吧，想好了我们就去订新招牌。 | Give it a name. We'll order a new sign once you've decided. |

### 开场 5 · 第一次走进店里

`opening_walk_in` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {protagonist} | normal | 「{shop}」……从今天起，这就是我的店了。 | "{shop}"... From today, this is my store. |
| 2 | {protagonist} | worried | 货架有点旧，后仓堆得满满的，跟小时候一模一样。 | Old shelves, a backroom packed to the ceiling. Just like when I was a kid. |
| 3 | {protagonist} | happy | 好，先把今天撑过去再说！ | Okay. Let's just get through today first! |

## 新手引导：父亲的电话（18.3）

### 引导 1 · 下订单（第 1 天开店前）

`tutorial_1_order` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 喂，{protagonist}，是我。开门前，先看看后仓和货架。 | Hey {protagonist}, it's me. Before you open, check the backroom and the shelves. |
| 2 | {dad} | normal | 点左边的「进货」，挑要补的货，填个数量就行。 | Click "Order" on the left, pick what's running low and set a quantity. |
| 3 | {dad} | normal | 今天订的货，明天开门前送到后仓。 | Whatever you order today arrives in the backroom before opening tomorrow. |
| 4 | {dad} | happy | 别一次订太多，手上要留点钱周转。 | Don't overdo it. Keep some cash on hand. |

### 引导 2 · 上架（第 2 天货到）

`tutorial_2_shelve` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 货到了吧？东西堆在后仓，可卖不出去。 | Delivery's in? Goods sitting in the backroom won't sell themselves. |
| 2 | {dad} | normal | 走到后仓按 E 拿货，再走到货架前按 E 放上去。 | Walk to the backroom and press E to pick up, then press E at a shelf to stock it. |
| 3 | {dad} | normal | 嫌麻烦，就点上面的「一键上货」。 | Short on time? Hit "Fill shelves" at the top. |
| 4 | {dad} | happy | 货架满满的，客人看着就想买。 | Full shelves make people want to buy. |

### 引导 3 · 收银（第一次开门营业）

`tutorial_3_checkout` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 开门了？有客人排队，你就站到收银台后面。 | Open already? When customers line up, go stand behind the register. |
| 2 | {dad} | normal | 人站在收银台后，客人就会过来一个一个结账。 | While you're behind the register, they'll come up and pay one by one. |
| 3 | {dad} | worried | 别让人等太久。等急了，人家放下东西就走。 | Don't keep them waiting. If they get fed up, they'll drop everything and leave. |

### 引导 4 · 看报表（第 1 天打烊）

`tutorial_4_report` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 打烊了？辛苦了。看看今天的报表。 | Closed up? Good work. Now look at today's report. |
| 2 | {dad} | normal | 卖了多少、进货花了多少、赚没赚钱，上面都有。 | What you sold, what you spent on stock, whether you made money: it's all there. |
| 3 | {dad} | happy | 哪样卖得好，明天就多订一点。 | Whatever sold well, order a bit more tomorrow. |

### 引导 5 · 改价格（第 3 天）

`tutorial_5_price` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 这两天生意怎么样？我教你改价钱。 | How's business? Let me show you how to set prices. |
| 2 | {dad} | normal | 点「定价」，每样东西都有个参考价。 | Click "Prices". Every item has a reference price. |
| 3 | {dad} | worried | 定高了，阿姨们一看就皱眉；定低了，白忙一场。 | Too high and the aunties frown. Too low and you're working for nothing. |
| 4 | {dad} | happy | 改一样试试，看看客人什么反应。 | Try changing one and see how customers react. |

### 引导 6 · 升级店面（第一次能升级）

`tutorial_6_upgrade` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | happy | 听你妈说，店里生意不错？ | Your mom says business is good? |
| 2 | {dad} | normal | 钱攒够了，就能把店面扩大。点「升级」看看。 | Once you've saved enough, you can expand the store. Take a look under "Upgrade". |
| 3 | {dad} | worried | 不过别急，想好了再扩。 | But don't rush it. Expand when you're ready. |

## 主线章节（18.4）

### 第一章 · 接手：老顾客认出主角（第 2 天开门）

`chapter1_regular` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 王阿姨 | happy | 哎哟，这不是{protagonist}吗？ | Oh my, isn't that little {protagonist}? |
| 2 | 王阿姨 | normal | 以前天天趴在收银台上写作业，现在都当老板啦！ | You used to do your homework on this counter. Now you're the boss! |
| 3 | {protagonist} | happy | 阿姨……您还记得我啊。 | Auntie... you still remember me. |
| 4 | 王阿姨 | normal | 怎么不记得。你爸身体好点没有？ | Of course I do. How's your dad feeling? |
| 5 | {protagonist} | normal | 在家休养呢。以后店里缺什么，您跟我说。 | Resting at home. If you ever need anything here, just tell me. |
| 6 | 王阿姨 | happy | 好好好，阿姨以后天天来照顾你生意。 | Good, good. Auntie will come by every day to keep you in business. |

### 第二章 · 改变：升级到阶段 2

`chapter2_change` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | worried | 店扩大了？墙打通了，货架也全换了…… | You expanded? Knocked down a wall, swapped out every shelf... |
| 2 | {dad} | worried | 老顾客来了，会不会找不到东西？ | Won't the regulars get lost? |
| 3 | {protagonist} | normal | 爸，我想多卖点肉和菜，街坊就不用跑大超市了。 | Dad, I want to sell meat and produce, so the neighbors don't have to go to the big supermarket. |
| 选项 `reassure` | 玩家 | | 放心，我会照顾好老顾客的。 | Don't worry. I'll take care of the regulars. |
| 选项 `ask` | 玩家 | | 那您教教我，怎么改才不走样？ | Then teach me how to change without losing what we had. |

### 第二章 a · 选择「放心」

`chapter2_reassured` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | ……嗯。你决定了，我就不多说了。 | ...Alright. If you've made up your mind, I won't say more. |
| 2 | {dad} | happy | 切肉台记得天天擦干净。 | Just keep that cutting table clean every day. |

### 第二章 b · 选择「教教我」

`chapter2_advice` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | happy | 哈，还知道问我。 | Ha, so you still ask your old man. |
| 2 | {dad} | normal | 老顾客常买的那几样，永远别断货。别的，你放手去做。 | Never run out of what the regulars buy. Everything else is your call. |

### 第三章 · 对手：升级到阶段 3

`chapter3_rival` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | worried | {protagonist}，你看到没有？街口要开一家大型亚洲连锁超市。 | {protagonist}, did you see? A big Asian supermarket chain is opening down the street. |
| 2 | {protagonist} | worried | 看到了……听说开业那周全场打折。 | I saw... They say everything's on sale opening week. |
| 3 | {mom} | worried | 你爸听说了，一晚上没睡好。 | Your dad heard and didn't sleep all night. |
| 4 | {protagonist} | normal | 妈，价钱我们拼不过，可我们有新鲜，有人情。 | Mom, we can't beat them on price. But we can beat them on freshness, and on knowing our customers. |
| 5 | {mom} | happy | 说得好。口碑做好了，客人会回来的。 | Well said. Earn a good name and people will come back. |

### 第四章 · 自己的店：升级到阶段 4

`chapter4_own_store` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | happy | 哇，这还是我们那家小店吗？ | Wow, is this really our little store? |
| 2 | {dad} | normal | 熟食柜、鱼缸……比我当年想的还大。 | A deli counter, fish tanks... bigger than I ever dreamed. |
| 3 | {protagonist} | normal | 其实一开始，我只是想替你们撑住这家店。 | Honestly, at first I just wanted to keep it going for you. |
| 4 | {protagonist} | happy | 可现在，每天开门，我都很期待。 | But now, every morning when I open up, I can't wait. |
| 5 | {dad} | happy | 那就好。这家店，现在真是你的了。 | That's all I wanted. This store is truly yours now. |

### 尾声（第四章结束后）

`chapter4_epilogue` · 黑屏字幕

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | （字幕） | normal | 后来，「{shop}」成了街坊们最常去的地方。 | In time, "{shop}" became the place the whole neighborhood went to. |
| 2 | （字幕） | normal | 爸妈还是会时不时来店里转转，假装只是路过。 | Mom and Dad still drop by now and then, pretending they're just passing. |
| 3 | （字幕） | normal | 而我的故事，还在继续。 | And my story keeps going. |

## 新部门讲解（18.3）

### 新部门 · 肉类（阶段 2，父亲来电）

`dept_meat` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 你进整块猪肉了？那得会拆。 | You're buying whole pork primals? Then you need to know how to break them down. |
| 2 | {dad} | normal | 放上切肉台，五花、排骨、里脊分开，边角料做肉馅。 | Put it on the cutting table. Belly, ribs and loin go separate; the scraps become ground pork. |
| 3 | {dad} | worried | 刀工不好，损耗就大。忙不过来，就请个师傅。 | Poor knife work means more waste. If you're swamped, hire a butcher. |

### 新部门 · 果蔬（阶段 2，母亲来访）

`dept_produce` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | happy | 开始卖菜啦？妈教你怎么挑。 | Selling produce now? Let me teach you how to choose. |
| 2 | {mom} | normal | 叶子菜放不久，卖多少进多少，宁可少进。 | Leafy greens don't keep. Buy what you can sell; less is better than too much. |
| 3 | {mom} | normal | 不新鲜了就打折，摊子乱了就理一理，客人才愿意挑。 | Mark it down when it wilts and tidy the stand when it's messy, so people want to browse. |

### 新部门 · 海鲜（阶段 3，父亲来电）

`dept_seafood` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | happy | 听说你装了鱼缸？我年轻时就想卖活鱼。 | I hear you put in fish tanks? I always wanted to sell live fish when I was young. |
| 2 | {dad} | normal | 水要勤换，水脏了鱼死得快。 | Change the water often. Dirty water kills fish fast. |
| 3 | {dad} | normal | 客人挑了活鱼，现杀现卖，这才叫新鲜。 | When a customer picks a fish, clean it on the spot. That's what fresh means. |

### 新部门 · 熟食（阶段 4，母亲来访）

`dept_deli` · 当面

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | happy | 要做熟食了？这个妈拿手。 | Starting a deli? Now that's my specialty. |
| 2 | {mom} | normal | 快到期的肉和菜别扔，做成菜卖，一点不浪费。 | Don't toss meat and vegetables near their date. Cook them and sell them: no waste. |
| 3 | {mom} | normal | 烧腊要香，炒菜要快，蒸的要热乎。 | Roasts should smell amazing, stir-fries should be quick, steamed dishes piping hot. |

## 学生贷款（18.6）

### 学生贷款 · 还清（主角给父母打电话报喜）

`loan_paid_off` · 电话

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {protagonist} | happy | 妈！我的学生贷款……今天全还清了！ | Mom! My student loan... I paid it all off today! |
| 2 | {mom} | happy | 真的？老头子，快来听！ | Really? Dear, come listen to this! |
| 3 | {dad} | happy | 好小子，比我当年还能干。 | Atta kid. You're doing better than I ever did. |
| 4 | {protagonist} | happy | 都是店里赚的。谢谢你们把店交给我。 | It all came from the store. Thank you for trusting me with it. |

### 学生贷款 · 本周没还够（心情台词，可关闭，14 天冷却）

`loan_short` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {protagonist} | worried | 这周的贷款没还够…… | Couldn't make the full loan payment this week... |
| 2 | {protagonist} | worried | 账单上的欠款又多了一行。 | One more line of arrears on the statement. |
| 3 | {protagonist} | normal | 没事，下周多卖一点，补上就好。 | It's fine. Sell a bit more next week and catch up. |

## 支线小故事（18.5，可在设置里关闭）

### 支线 · 阿姨回忆主角小时候写作业

`side_auntie_homework` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 王阿姨 | normal | {protagonist}，阿姨跟你说，你小时候可乖了。 | {protagonist}, you know, you were such a good kid. |
| 2 | 王阿姨 | happy | 放学就坐在方便面箱子上写作业，写完还帮你妈数零钱。 | After school you'd do homework on a box of instant noodles, then help your mom count change. |
| 3 | {protagonist} | happy | 我都不记得了…… | I don't even remember that... |
| 4 | 王阿姨 | happy | 我记得。那时候你说，长大要开全城最大的超市。 | I do. You said you'd open the biggest supermarket in town one day. |
| 5 | {protagonist} | happy | ……那我得加油了。 | ...Guess I'd better get to work, then. |

### 支线 · 父母来店里，父亲忍不住帮忙

`side_parents_visit` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {dad} | normal | 我们就是路过，看一眼。 | We were just passing by. Only a quick look. |
| 2 | {dad} | normal | 这箱酱油放歪了，我来—— | This case of soy sauce is crooked, let me— |
| 3 | {mom} | worried | 你给我坐下！医生怎么说的？ | Sit down! What did the doctor tell you? |
| 4 | {dad} | worried | 搬一箱又累不着…… | One box won't hurt... |
| 5 | {protagonist} | happy | 爸，交给我吧。您坐着指挥就行。 | Dad, leave it to me. You can supervise from the chair. |
| 6 | {mom} | happy | 看见没，孩子出息了。 | See? Our kid's all grown up. |

### 支线 · 后仓里找到父亲的旧账本

`side_old_ledger` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {protagonist} | normal | 后仓角落里有个旧本子……是爸的账本。 | There's an old notebook in the corner of the backroom... Dad's ledger. |
| 2 | {protagonist} | normal | 每天进了什么、卖了多少，都用钢笔写得整整齐齐。 | Every delivery, every sale, written neatly in fountain pen. |
| 3 | {protagonist} | worried | 有一页写着：「今天下雨，没人来。明天会好的。」 | One page says: "Rain today, no customers. Tomorrow will be better." |
| 4 | {protagonist} | happy | ……明天会好的。 | ...Tomorrow will be better. |

### 支线 · 父母打电话问店里忙不忙（节日系统接入后可改为节日触发）

`side_parents_call_busy` · 电话 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | {mom} | normal | {protagonist}，最近店里忙不忙？ | {protagonist}, has the store been busy lately? |
| 2 | {protagonist} | happy | 挺忙的，周末客人特别多。 | Pretty busy. Weekends are packed. |
| 3 | {mom} | happy | 那就好。再忙也要记得吃饭。 | Good. Busy or not, remember to eat. |
| 4 | {dad} | normal | （在旁边）问问冷柜温度调好没有！ | (in the background) Ask if the cooler temperature is set right! |
| 5 | {protagonist} | happy | 调好了调好了，爸您放心。 | It's set, Dad. Don't worry. |

### 支线 · 留学生想买家乡零食

`side_student_snack` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 留学生小李 | worried | 老板，请问有没有……那种辣条？我家乡的那种。 | Excuse me, do you have... those spicy gluten strips? The kind from back home? |
| 2 | {protagonist} | normal | 辣条啊，现在还没进。你想要哪个牌子？ | Spicy strips? Not yet. Which brand do you want? |
| 3 | 留学生小李 | happy | 哪个都行！来这边以后好久没吃到了。 | Any brand! I haven't had them since I moved here. |
| 4 | {protagonist} | happy | 我记下了，下次进货给你留一包。 | I'll make a note. I'll save you a pack from the next delivery. |

### 支线 · 切肉师傅的刀

`side_butcher_knife` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 陈师傅 | normal | 老板，这把刀是我师父传下来的。 | Boss, this knife was handed down from my master. |
| 2 | 陈师傅 | normal | 他说，切肉跟做人一样，要顺着纹理来。 | He used to say cutting meat is like living: go with the grain. |
| 3 | {protagonist} | normal | 那您跟师父学了多久？ | How long did you train with him? |
| 4 | 陈师傅 | happy | 十年。头三年，只让我磨刀。 | Ten years. The first three, he only let me sharpen knives. |
| 5 | {protagonist} | happy | 那我这点刀工，还差得远呢。 | Then my knife skills have a long way to go. |

### 支线 · 鱼档师傅的清晨

`side_fishmonger_dawn` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 阿海 | happy | 老板早！我四点就去码头了。 | Morning, boss! I was at the docks by four. |
| 2 | 阿海 | normal | 挑鱼要看眼睛，眼睛亮的才新鲜。 | Check the eyes. Bright eyes mean a fresh fish. |
| 3 | {protagonist} | happy | 难怪最近活鱼卖得这么好。 | No wonder the live fish have been selling so well. |
| 4 | 阿海 | happy | 嘿嘿，渔村长大的，就这点本事。 | Heh, I grew up in a fishing village. It's the one thing I know. |

### 支线 · 厨师奶奶的红烧肉

`side_chef_recipe` · 当面 · 可关闭

| # | 说话人 | 表情 | 中文 | English |
| --- | --- | --- | --- | --- |
| 1 | 周师傅 | normal | 老板，今天的红烧肉，是我奶奶的方子。 | Boss, today's braised pork is my grandmother's recipe. |
| 2 | 周师傅 | happy | 冰糖要炒到琥珀色，一秒都不能多。 | You cook the rock sugar to amber. Not one second longer. |
| 3 | {protagonist} | happy | 怪不得有客人说，吃出了家里的味道。 | No wonder a customer said it tasted like home. |
| 4 | 周师傅 | happy | 那就值了。 | Then it was worth it. |

## 其他文字

| 键 | 中文 | English |
| --- | --- | --- |
| `character.protagonist.name` | 阿明 | Ming |
| `character.dad.name` | 爸爸 | Dad |
| `character.mom.name` | 妈妈 | Mom |
| `character.auntie.name` | 王阿姨 | Auntie Wang |
| `character.student.name` | 留学生小李 | Xiao Li |
| `character.butcher.name` | 陈师傅 | Master Chen |
| `character.fishmonger.name` | 阿海 | Ah Hai |
| `character.chef.name` | 周师傅 | Chef Zhou |
| `story.shop.default_name` | 福记杂货 | Lucky Grocery |
| `tutorial.step1.objective` | 爸爸的提示：下一张订单（进货） | Dad's tip: place an order (Order) |
| `tutorial.step2.objective` | 爸爸的提示：把后仓的货摆上货架 | Dad's tip: put goods from the backroom on a shelf |
| `tutorial.step3.objective` | 爸爸的提示：站到收银台后，完成一笔收银 | Dad's tip: stand behind the register and ring up a customer |
| `tutorial.step4.objective` | 爸爸的提示：看一看今天的报表（报表） | Dad's tip: look over today's report (Report) |
| `tutorial.step5.objective` | 爸爸的提示：改一次价格（定价） | Dad's tip: change one price (Prices) |
| `tutorial.step6.objective` | 爸爸的提示：店面可以升级了（升级） | Dad's tip: the store can be upgraded (Upgrade) |
