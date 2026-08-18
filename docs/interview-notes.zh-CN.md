# StudyLens 面试讲解笔记

## 30 秒项目介绍

中文版本：

> StudyLens 是一个面向学生的隐私优先 AI 学习反思工具。浏览器扩展让用户主动记录一次 AI 学习会话，但只发送平台、学习目标、时长、交互次数、字数和帮助程度，不发送 prompt 或 response 正文。ASP.NET Core API 负责验证并通过 repository 保存数据，React Dashboard 再展示使用趋势。第一版重点不是复杂 AI 功能，而是打通一个可靠、可测试、隐私边界明确的产品流程。

English version:

> StudyLens is a privacy-first reflection tool for AI-assisted learning. A Manifest V3 browser extension collects only opt-in, content-free metadata. An ASP.NET Core API validates and stores the events through a repository abstraction, and a React dashboard visualizes usage patterns and self-reported helpfulness. The MVP focuses on a complete and testable product workflow rather than collecting sensitive conversation content.

## 一次请求是怎么走的

1. 用户打开扩展 popup。
2. 扩展从当前 tab 的 URL 判断 ChatGPT、Claude、Gemini 或 Copilot，但不读取网页内容。
3. 用户选择 learning activity，填写时长、交互次数、字数和 helpfulness rating。
4. 扩展向 `POST /api/events` 发送 JSON。
5. ASP.NET Core model binding 把 JSON 转成 `CreateLearningEventRequest`。
6. Data Annotations 检查范围；JSON 配置拒绝未声明字段。
7. Controller 创建 `LearningEvent`，通过 `ILearningEventRepository` 保存。
8. Dashboard 请求 `/api/insights`，Service 按 provider、activity 和日期聚合。

## 为什么这样选技术

### 为什么 React

- 岗位现有产品使用 React；
- Dashboard 和 extension popup 都是状态驱动的小型交互界面；
- TypeScript 可以在编译期检查 API 数据形状。

### 为什么 ASP.NET Core

- 它提供依赖注入、model binding、validation 和清晰的 controller 结构；
- 我有 Java/OOP 基础，C# 的类型系统和分层设计很容易迁移；
- `WebApplicationFactory` 可以直接测试完整 HTTP pipeline。

### 为什么 MongoDB

- 学习事件天然接近 document/event 数据；
- 未来增加非敏感字段时 schema 演进比较灵活；
- 但当前查询模式固定，如果以后需要复杂关系、事务和研究数据联结，PostgreSQL 可能更合适。不要说 MongoDB 永远更好。

### 为什么 Repository 接口

Controller 不应该知道数据存在内存还是 MongoDB。`ILearningEventRepository` 让测试使用快速、确定性的内存实现，真实环境再换成 MongoDB。代价是多一层抽象；对于只有一个简单 endpoint 的一次性脚本会过度设计，但这个项目需要测试和可替换存储，因此合理。

## 隐私设计怎么讲

核心不是“我们承诺不保存 prompt”，而是三层限制：

1. 扩展表单没有 raw prompt 输入；
2. API request model 没有 raw prompt 字段；
3. JSON deserializer 配置为拒绝 unknown fields，所以带 `rawPrompt` 的请求返回 400。

扩展权限使用 `activeTab`，只在用户主动点击扩展时识别当前平台；没有申请读取全部历史记录的权限。字数应在客户端计算，只上传数字。

## 测试与真实调试故事

最开始业务单元测试全部通过，但扩展真实 POST 时 API 返回 500。原因是 .NET 10 对 record primary constructor 的 validation metadata 有更严格的规则。修复方法是把 request DTO 改成普通 class，让 Data Annotations 明确放在属性上。

这个问题说明：

- compile success 不等于 HTTP pipeline 正常；
- unit test 只能验证 aggregation logic；
- integration test 才覆盖 JSON serialization、model binding、validation 和 controller routing。

修复后加入了 `WebApplicationFactory` 测试，覆盖：

- 合法 metadata 返回 201；
- helpfulness 超出 1-5 返回 400；
- 请求包含 `rawPrompt` 返回 400。

## CORS 和开发代理

Dashboard 最初直接从 5173 端口请求 5080 端口，会形成 cross-origin request。开发环境后来改成请求相对路径 `/api`，由 Vite proxy 转发给 ASP.NET Core。生产环境也可以用 Nginx、Cloudflare 或平台反向代理提供统一 origin。

扩展不经过 Dashboard 的代理，所以 Manifest 用 `host_permissions` 明确允许访问本地 API。

## 现在不能夸大的地方

如果被问到 production readiness，要主动说明：

- 目前没有登录和授权，participant ID 不是安全凭证；
- 默认演示存储是内存，重启会丢失；
- MongoDB adapter 已实现并编译，但还没有连接真实测试数据库做 integration test；
- 扩展现在是用户主动填写 metadata，还没有自动计算会话数据；
- 没有用户研究结果，不能声称提升了学习效果。

这不会让项目显得差。能明确 boundary 和 next steps 通常比假装 production-ready 更专业。

## 如果问“这个项目是不是 AI 帮你写的”

可以诚实回答：

> I used AI as a coding and research assistant to move quickly across an unfamiliar stack. I made the product-scope, data-model and privacy decisions, reviewed the generated code, ran the application end to end, and added tests after finding a real model-binding failure. I can explain each layer and its trade-offs, rather than treating the generated code as a black box.

不要回答“全部都是我手写的”，也不要回答“AI 自动做完了”。重点是你使用 AI 提高速度，但你负责判断、验证和结果。

## 常见追问

### 为什么不直接保存聊天内容，再用 LLM 分析？

原始聊天可能包含个人信息、课程答案和第三方内容。第一版先验证 content-free metadata 是否已经能提供有价值的反思，减少收集范围和合规风险。如果研究确实需要文本，应重新做 informed consent、retention policy、访问控制和 ethics review，而不是悄悄扩大采集。

### 为什么 participant ID 不够安全？

它只是假名化标识，不是 authentication token。知道 ID 的人理论上可以查询对应记录。正式版本必须有登录、ownership check 和授权。

### 为什么不用 Python/FastAPI？

FastAPI 也能完成任务，但这个岗位的现有后端是 ASP.NET Core。选择 C# 可以直接证明我能把 Java/OOP 基础迁移到他们的技术栈，同时学习强类型 DTO、依赖注入和 .NET testing。

### 下一步最重要的是什么？

不是继续堆图表，而是补真实 MongoDB integration test、身份与数据删除能力，然后找 3-5 个学生做 usability test，验证收集字段是否容易理解、Dashboard 是否真的帮助 reflection。

## 90 秒演示顺序

1. 用一句话说明问题和 privacy principle。
2. 打开扩展，指出它不读取或显示 prompt 内容。
3. 选择 provider、activity 和 rating，保存一条记录。
4. 打开 Dashboard，展示 sessions、minutes、helpfulness 和分布变化。
5. 快速展示 API request model、repository interface 和一条 integration test。
6. 最后主动说一个 limitation 和 next step。
