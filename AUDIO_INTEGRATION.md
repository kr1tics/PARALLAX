# PARALLAX — 第一版音频接入

**状态：已生成工作副本并接入 Unity；尚未完成直接试听或游戏内混音验证。** 运行测试、峰值检查和循环边界测量不代表声音已试听通过。拾取片段、环境片段与合成完成提示仍为可替换候选。

## 分类与触发

| 分类 | 项目工作副本 | 触发与处理 |
| --- | --- | --- |
| 菜单 | Ambience/Menu.wav | MainMenu 的低音量循环；511493 原素材 15–45 秒，去均值 DC，1 秒首尾交叉衔接，循环周期 29 秒。 |
| UI | UI/Click.wav | UI Toolkit 鼠标点击/键盘提交，以及 uGUI Intro、完成界面的按钮；843148 裁为约 0.148 秒，峰值限制为 0.18，另乘播放音量。 |
| 2026 环境 | Ambience/Past.wav | 715475 开头 30 秒，经 1 秒循环衔接得到 29 秒工作循环。 |
| 2176 环境 | Ambience/Future.wav | 864658 开头 30 秒，经 1 秒循环衔接得到 29 秒工作循环。 |
| 局部设备 | Ambience/DeviceHum.wav | 571922 的 7 秒单声道循环；两个 relay 附近的 3D AudioSource，仅对应 relay 成功后淡入，9 米外不可闻。 |
| 时间切换 | Systems/TimeSwitch.wav | 872647 经 afconvert 从实际 AIFF 正确转换为 WAV；保留开头 0.32 秒（约 0.2 秒事件及尾音）。实际时代变化时播放，环境底音交叉淡化 1.5 秒。 |
| 重力关闭 | Systems/GravityOff.wav | 169994 转 WAV，保留 4.4 秒下降趋势；仅 station gravity 从 ON 变 OFF 时播放。 |
| 拾取 | Interaction/PickupCandidate.wav | 762911 暂选第一事件周边 0.17–0.64 秒，含前后余量；不是最终试听裁切点。实际 HeldObject 获得物体时播放，不播放整段 19 秒集合。 |
| 重物落地 | Interaction/HeavyDrop.wav | 810170 转 WAV，测量检测主要事件，保留少量起音/尾音余量，降低峰值；松手后在实际碰撞且相对速度至少 0.45 m/s 时播放一次，不在失重松手时直接播放。 |
| Relay 激活 | UI/Click.wav + 局部 DeviceHum | 一个短确认音后，成功的 relay 附近 Hum 淡入；运行时组合，尚未试听混音确认，不存在已验证的合成成品。 |
| 完成 | UI/CompleteCandidate.wav | 843148 独立双音候选：110 ms 间隔，第二音通过重采样上行 2 个半音，总长约 0.39 秒；完成界面按钮淡入结束时播放。它是本次新生成的未试听候选，不是已验证成品。 |

所有工作文件位于 `Assets/Audio/Processed/`；原始素材仍在用户提供的 Downloads / Desktop 路径，未被修改。工作副本使用 44.1 kHz、16-bit PCM WAV；环境导入使用压缩内存模式，短事件为 PCM。处理包括所选片段的 DC 均值移除、事件 5 ms 起止淡化、峰值降低与环境首尾交叉衔接。详细裁切、实际长度、增益、样本边界差和原始/输出 SHA-256 记录于 `AUDIO_PROCESSING_MANIFEST.json`。原始文件哈希已复查一致；副本峰值均低于 0.36，无数字削波。

**Gravity ON、Jetpack Thrust 未新增，也不列为缺少素材。** 未使用 197993；872648 保留为方案中的替换备选，未提供路径、未导入。

## 调整位置

在 Project 面板打开 **Assets/Resources/Audio/ParallaxAudioPalette.asset**。这里集中提供所有 AudioClip 引用、菜单/Past/Future/局部 Hum 音量，以及 UI、系统、交互音量和交叉淡化时长。

初始值：菜单 0.12；Past/Future 各 0.18；局部 Hum 0.10；UI 0.45；系统/交互各 0.40；交叉淡化 1.5 秒。这是保守的接入起点，不是已验证的最终混音。所有声音继续受 Settings 的 Master Volume 控制。Settings 暂停普通声音，UI 确认音仍可响应；未另建 AudioMixer 或改变现有音量系统。

Intro 的 Opening Hum、Mechanical Ambience、Signal Sound 引用了处理副本。最终揭幕的音频交给教程的同一条 Future 环境声，使用一个只读揭幕进度属性，避免 Intro Future 声与游戏 Future 声同时铺底。Intro 的时序、画面、文字、字体与控制恢复逻辑均未改变。

## 结构与范围

- `ParallaxAudioPalette.cs` / Resources 中的 palette：集中可调资源与音量。
- `ParallaxSceneAudio.cs`：MainMenu 和 StartScene 的独立观察组件。读取时间、站点重力、HeldObject、现有 relay 完成标记和完成界面就绪状态；绑定已有 UI 按钮，不改变任何玩法状态。
- `ParallaxDropAudio.cs`：为现有谜题及练习方块添加声音观察组件和 AudioSource；不添加 Collider/Rigidbody，不改变重力、拾取或碰撞机制。碰撞音只在松手后达到速度阈值时播放一次，30 个游戏秒内有效。
- MainMenu.unity、StartScene.unity：仅新增音频组件及引用。
- ContextIntro.cs：仅新增只读 LaboratoryRevealBlend；ContextIntro.prefab：仅指定音频副本。
- 现有 Level1Presentation 的可选事件槽仍为空，避免重复播放。原来的 UI/视觉、玩家、谜题、时间切换与重力脚本未改动。

继续测试时，从 MainMenu 点击 START GAME；在教程检查 Past/Future、关闭重力、拾取、实际落地与 relay 激活。Shift + K 可预览完成音候选，Replay 可重新开始。请用耳机或扬声器在可比音量下确认环境片段、循环接缝、拾取是否具有奖励感、完成双音和实际混音；目前这些主观/听觉检查仍待完成。

落地音本次测量起点约 0.386 秒，副本长度约 0.541 秒；这是技术检测结果，需要试听确认自然尾音。

## 验证

Unity 6000.3.20f1 编译及全部 10 个工作副本导入成功。最终 17/17 针对性 PlayMode 测试通过（音频资源、交叉淡化、Settings 暂停、Intro/Skip、菜单、完成界面/Replay/Continue）。此前完整回归为 51/52 通过，唯一失败是新测试将方块数量写成 4，实际还包含 2 个练习方块；已修正为核对所有 MovableObject 都有落地音观察组件，并在最终测试中通过。报告：Verification/audio-integration-playmode.xml。

验证不包括直接试听、游戏内主观混音、实际扬声器输出或 WebGL 构建。所有这些听觉/目标平台确认仍待完成，不能称为已试听通过。
