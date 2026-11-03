## 更新
v1.0.3
- 修复开启思考（`includeThoughts`）后翻译结果混入思考过程文本的问题：解析流式响应时遍历所有 `parts`，跳过 `thought:true` 的思考 part，仅拼接最终答案文本

v1.0.2
- 添加 `思考预算` 设置

v1.0.1
- 实现 `Gemini` 翻译插件