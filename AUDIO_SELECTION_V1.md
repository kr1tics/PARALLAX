# PARALLAX — 第一版音效选型与处理清单

风格：Environmental Mystery Sci-Fi。

- **2026 / PAST：**正常运行的实验室。
- **2176 / FUTURE：**废弃但仍有少数设备运行的实验室。

**状态：选型与处理计划，尚未完成直接试听或游戏内混音验证。** 以下技术分析与测量定位来自用户提供的方案；初始文档登记时未重新分析音频。后续已经生成工作副本并接入 Unity，详情见 [音频接入记录](AUDIO_INTEGRATION.md)；仍不得将候选描述为已试听通过或已混音通过。

## 当前音效方案

| 功能 | 选用素材 | 处理要求 |
| --- | --- | --- |
| Main Menu Ambience | **511493 — Ambient Space Noise** | 去直流偏置，选择合适片段并处理循环衔接；低音量铺底。 |
| UI Button Click | **843148 — Monitor Blip** | 约 0.148 秒；明显降低原始电平，使用短促单音。 |
| 2026 Laboratory Ambience | **715475 — Server Room Noise** | 作为主要环境底音，检查循环接缝。 |
| 2176 Laboratory Ambience | **864658 — Dark Sci-Fi Laboratory** | 作为主要环境底音，检查首尾电平差与循环衔接。 |
| 局部仍运行设备 | **571922 — Electric Hum** | 可选辅助层，仅在少量设备附近播放，避免全场叠加低频。 |
| Time Switching | **872647 — TV Switch Off** | 提取开头约 0.2 秒的事件，保留必要尾音；配合两个时代环境音交叉淡化。 |
| Gravity OFF | **169994 — Power Down** | 按关闭动作时长调整约 4.4 秒的原素材，保留下降趋势。 |
| Object Pickup | **762911 — Game Pick Up** | 原文件约 19 秒、包含 9 个短事件；挑一个独立事件，不能整段播放。 |
| Object Drop | **810170 — Heavy Object Drop** | 用于重物／方块落地；精确裁掉主要事件前约 0.4 秒的空白，降低电平。 |
| Puzzle / Relay Activation | **843148 + 571922** | **待制作：**短提示音确认激活，再让设备附近的 Hum 淡入，表达开始运行。 |
| Objective / Tutorial Complete | **843148 的独立双音变体** | **待制作：**以节奏或轻微上行音程区别于 UI 单音；需要试听确认。 |

**Gravity ON 与 Jetpack Thrust 当前不需要，不列为待补素材。**

## Object Pickup 的具体选择

`762911` 已保留为第一版拾取音候选。优先试听：

- **0.19–0.58 秒**
- **2.06–2.44 秒**

以上是测量定位范围，不是最终裁切点。选一个短促、克制、适合物体拾取的事件，保留起音和自然尾音；避免明显的金币奖励感。**最终片段尚未选定。**

## 实施前需要知道

- 根据所提供的技术分析，`872647` 虽然扩展名为 `.wav`，实际容器是 **AIFF**。需要正确转换生成工作副本，不能只改扩展名。
- `843148` 原始峰值接近满刻度，必须调整电平。
- 环境音应在可比音量下试听；原始响度不同，不代表较响的更适合。
- Time Switching 第一版只用一个 TV 瞬态，不默认加入高频较多的 `197993 — Static Fast Hum`。
- `872648 — TV Switch Off 2` 留作替换备选。
- Relay Activation 和 Objective Complete 是**待制作方案**，目前不存在已验证的合成成品。
- 保留原始素材，剪辑、格式转换、去直流等操作均生成独立副本。

## 原始素材位置

以下路径属于本机素材位置，不是仓库内的资源引用。

下载目录：`/Users/soyuuuuu/Downloads/`

```text
511493__the_toothpaste_vampires__ambient-space-noise-1-30bpm-6m-24s.wav
715475__kvv_audio__ambtech_server-room-noise-01_kvv_free.wav
864658__tommasomotteran__dark-sci-fi-laboratory-atmosphere-spaceship-interior-ambience-loop.wav
571922__alex36917__electric_hum_ambiance.wav
872647__signaturesoundsorg__tv_switch_off.wav
169994__peepholecircus__power-down.mp3
762911__ienba__game-pick-up.wav
810170__mokasza__heavy-object-drop.mp3
```

UI 提示音：

```text
/Users/soyuuuuu/Desktop/audio/843148__sadiquecat__795hz-monitor-blip.wav
```

## 初始文档登记时的访问检查与工作边界

2026-10-09（Australia/Melbourne）：上述 **9 个文件均可读取**；不可访问的已列出文件：**无**。检查仅打开文件并读取一个字节，以确认路径访问，不构成试听、技术分析或声音内容判断。未仅凭文件名推断声音内容。

`197993` 与 `872648` 未提供本地路径，本次没有检查其文件可访问性；它们仍分别是“不默认使用”与“替换备选”。

初始登记只纳入项目文档并添加 README 导航。未复制或处理原始音频，未生成剪辑或合成成品，未修改 AudioClip 引用、音量、脚本或 Unity 场景。后续工作包括直接试听、选择与处理独立副本、循环接缝检查，以及游戏内混音验证；完成前不得更改上述待验证状态。

## 后续实施状态

已按分类生成独立副本并接入当前 Unity 项目；原音频未修改。新增的完成双音和运行时 relay 确认/设备 Hum 仍未试听验证，拾取片段仍为候选。见 [AUDIO_INTEGRATION.md](AUDIO_INTEGRATION.md) 与 [处理记录](AUDIO_PROCESSING_MANIFEST.json)。

后续用户调整：Gravity OFF 改用 187326 — powerdown2；169994 移为 Gravity ON，覆盖初始方案中不需要 Gravity ON 的决定。Main Menu 已接入 511493；暂停菜单下背景环境音继续播放。详见音频接入记录。

用户随后纠正，最终映射为：Gravity OFF = 169994，Gravity ON = 187326，覆盖上一条相反的对应。
