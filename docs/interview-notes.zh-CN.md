# StudyLens 面试讲解笔记

## 30 秒介绍

中文：

> StudyLens 是一个 multi-course, source-grounded AI tutor。它把课程材料变成带文件名和页码的检索索引，先找证据，再用本地 Qwen 做双语讲解、生成练习和形成性评分。评分记录连同引用证据保存在 MongoDB，学生可以刷新后继续查看。它还包含一个只读取用户明确选中文字的浏览器扩展。公开仓库有可复现实例和固定 retrieval benchmark；我的 EAM 本地部署包含 52 份 PDF、926 页资料。

English:

> StudyLens is a multi-course, source-grounded AI tutor. It retrieves page-level evidence before using a local Qwen model to explain concepts, generate practice, or provide formative feedback. MongoDB persists each attempt together with its nested citation evidence, and an explicit-selection browser extension connects web context to the course tutor without passive browsing-data collection. A public demo corpus and fixed retrieval benchmark make the repository reproducible.

## 为什么这和岗位匹配

岗位要继续开发 AI learning browser extension 和 shared dashboard，并要求 JavaScript/React、ASP.NET Core/C#、MongoDB/NoSQL。StudyLens 对应的是同一条产品链：

- React/TypeScript dashboard：课程检索、练习、反馈、历史；
- ASP.NET Core/C#：API、输入校验、retrieval、AI orchestration、文件安全边界；
- MongoDB：持久化 nested study-attempt documents；
- browser extension：明确选中文本后交接到 dashboard；
- Python：离线资料抽取、chunking 和 retrieval test fixtures；
- AI tools research：可替换 provider，并记录模型、证据和延迟边界。

不要说“我已经做了和你们一模一样的系统”。更可信的表达是：

> I built a small end-to-end prototype around the same technical boundaries so that I could contribute faster and discuss concrete trade-offs rather than only expressing interest.

## 一次评分请求怎么走

1. React 发送 `courseId`、题目和 student answer。
2. Controller 校验课程状态、文本长度和请求结构。
3. Search Service 只在该课程索引中做 BM25-style ranking。
4. 系统选出六段证据并编号 `[1]`–`[6]`。
5. Tutor Service 要求模型按 JSON Schema 返回分数、strengths、missing points 和 improved answer。
6. API 自己附加稳定 attempt ID、model name 和 citation objects。
7. `MongoStudyAttemptRepository` 把整次作答作为一个 document 保存。
8. Feedback 页面重新读取该课程历史，计算 attempt count 和 average percentage。

## 为什么 MongoDB 合适

一次作答不是简单的一行分数，而是一个天然的聚合对象：question、answer、多个 strengths、多个 missing points、improved answer、model 和多条 citation。MongoDB 可以把这组嵌套结构作为一个 document 原样保存，读取 Feedback 时不需要把很多表重新 join。

这不代表 SQL 不行。选择 MongoDB 的原因是数据访问模式以“按课程读取完整 attempt”为主，而且目标岗位本身使用 MongoDB/NoSQL。为了不把业务逻辑绑死，Controller 只依赖 `IStudyAttemptRepository`；JSON adapter 只用于单元测试和明确 fallback。

实现细节：

- 默认连接 `mongodb://127.0.0.1:27017`；
- database `studylens`，collection `study_attempts`；
- compound index：`courseId` ascending + `createdAtUtc` descending；
- `/health` 会真实 ping MongoDB，断开时返回 degraded/503；
- 删除按 course scoped 执行，并由用户在 UI 确认。

## 为什么不把全部 PDF 发给模型

- 926 页会引入大量无关上下文，成本高且难核查；
- retrieval 和 generation 分开后，可以分别测试“找得对不对”和“讲得对不对”；
- 模型只收到当前问题需要的摘录，降低隐私和版权暴露面；
- 文件名和页码来自程序索引，不让模型猜引用。

## 为什么选 Qwen3.5

- 权重采用开放许可证，可在本机运行，不需要 API key 或 token 费用；
- 4B 是当前 16 GB 电脑上可靠的默认档；
- 9B 已保留给 RTX 3060 电脑做质量/延迟比较；
- `ITutorAiProvider` 隔离模型调用，以后接云模型不用改 retrieval、MongoDB 或 React。

不要说“Qwen 是世界上最好的模型”。应该说：

> It was the best practical quality-and-portability trade-off for the hardware available to the project. I kept the provider configurable so model quality can be evaluated rather than assumed.

## 浏览器扩展的隐私设计

第一版就限制为 user-initiated explicit selection：

- 推荐入口是选中文字后的右键菜单；浏览器把明确选择的 `selectionText` 交给扩展，因此不会因为点击工具栏导致页面失焦而丢失选区；
- 工具栏入口仍只在用户点击后调用 `window.getSelection()`，作为兼容性 fallback；
- 右键入口只用 `storage.session` 暂存选区，弹窗读取后立即删除；
- 不申请 cookies 或 history 权限；
- 用户能在发送前编辑或取消；
- 数据进入本机 URL fragment，不成为 HTTP request path；
- Dashboard 消费后立刻清除 fragment。

端到端交接不是“扩展只生成一个链接”就结束。Dashboard 现在会解析并校验 handoff，按可用课程切换上下文，把选中文字显示在独立卡片中，然后复用同一个 course-grounded explain API。无法识别的 course ID 会被丢弃，问题长度也在扩展和 Dashboard 两端限制为 300 个字符。

如果以后做真实用户研究，还需要 consent notice、retention policy、pseudonymous participant ID 和可撤回机制；当前原型不假装已经满足完整研究治理。

## 测试和评估怎么讲

- 5 个 Python 测试验证 PDF/Markdown/text indexing、page preview 和 stable metadata；
- 34 个 C# 测试覆盖 retrieval、完整 evidence detail、course isolation、API validation、Chrome extension CORS、Tutor structured output、repository 和安全文件路径；
- 6 个 TypeScript 测试锁住 extension URL 编码、文本长度限制、右键暂存选区的 freshness、Dashboard handoff 解析和无效 course fallback；
- 4 个固定 retrieval cases 要求期望文档 rank first；
- Dashboard 和 extension 都做 lint、test 和 production build；
- 真实本机 smoke test 已验证 MongoDB health、AI grade write、history read、handoff fragment 消费和浏览器显示。

Chrome/Edge 的扩展管理安全页面不能由自动化工具代替用户操作，因此在亲自在 `chrome://extensions` 或 `edge://extensions` 完成 **Load unpacked** 并跑完 README 的五步 checklist 前，不说“已经完成真实扩展端到端验证”。目前可以准确说：扩展和 Dashboard 两端的 selection/handoff contract 有自动化测试，Dashboard 接收页有真实浏览器验证，最终安装检查需要一次人工操作。

固定 benchmark 的价值是：以后换 embedding、hybrid retrieval 或模型时，能够比较结果，而不是凭感觉说“好像更聪明”。

## 真实调试故事

1. Python dataclass 最初输出 `relative_path`，C# 期待 `relativePath`。文本仍能显示，但来源路径为空。修复为显式 JSON mapping，并加测试锁住 filename/page contract。
2. 9B 模型下载成功，但在当前 16 GB 集显电脑 inference startup OOM。于是 4B 成为 portable default，9B 保留给 RTX 3060；这证明 provider/model configuration 不是过度设计。
3. MongoDB Driver 代码编译通过并不等于数据库真的可用，所以给 `/health` 增加 live ping，并用真实 AI 评分完成 write → reload → UI display。面试时强调 **runtime evidence, not README claims**。
4. 浏览器实测发现历史条目触发横向滚动，原因是 CSS Grid 子项默认 `min-width:auto`。给文本容器加 `min-width:0` 后重新 build 和视觉验证。

## 不能夸大的部分

- 当前是 lexical retrieval，同义词覆盖有限；
- 扫描图片型 PDF 仍需要 OCR；
- 4B 本地模型质量不等于云端旗舰模型；
- 引用证明来源，但生成解释仍需要学生核对；
- 尚未完成真实用户学习效果研究；
- 浏览器扩展是可加载 prototype，不是商店发布产品。

## 下一步按价值排序

1. 用真实用户任务定义 retrieval recall、citation correctness、grading consistency 和 task completion 指标。
2. 对 lexical 与 hybrid semantic retrieval 做同一 benchmark 的比较。
3. 加认证/participant pseudonymization，把个人本地历史升级为真正 shared dashboard。
4. 对比 4B、9B 和云端模型的质量、延迟、成本与隐私。
5. 为扫描资料添加选择性 OCR，而不是对所有 PDF 盲目 OCR。
