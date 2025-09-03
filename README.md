# SqlSchemaGenerator
SqlSchemaGenerator – Documentation
📖 نظرة عامة
SqlSchemaGenerator مكتبة .NET متقدمة لإدارة وتوليد وتنفيذ تغييرات قواعد البيانات (Database Schema Migrations) مع دعم:
• 	توليد السكريبتات (CREATE / DROP / ALTER) تلقائيًا من تعريفات الـ Entities.
• 	تحليل الأثر (Impact Analysis) قبل التنفيذ.
• 	تحليل الأمان (Safety Analysis) لتجنب الأوامر الخطيرة.
• 	التنفيذ التفاعلي أو التلقائي مع دعم الـ Rollback.
• 	تتبع الميجريشن عبر  مع حفظ Snapshots.
• 	فلترة الـ Types من Assemblies بناءً على Base Class أو Interface.

🏗️ المكونات الأساسية


⚙️ الإعدادات



• 	SingleBatch → إصدار واحد لكل Session.
• 	PerEntity → إصدار لكل Entity.
• 	PerLogicalBatch → إصدار لكل مجموعة منطقية.

🚀 أمثلة الاستخدام
1. تشغيل ميجريشن على Types محددة

2. تشغيل ميجريشن على Assembly كامل

3. تشغيل ميجريشن على Assemblies متعددة

4. استخدام النسخة Generic


🧠 الفلترة الذكية للـ Types
• 	تدعم فلترة الـ Types من Assembly أو أكتر.
• 	الفلترة تعمل بـ OR Logic: أي Type يطابق أي شرط من الشروط (Base Class أو Interface) يتم اختياره.
• 	يمكن تمرير أكثر من شرط.

📊 أوضاع التنفيذ
• 	Dry Run: توليد السكريبتات فقط بدون تنفيذ.
• 	Interactive Mode: تنفيذ تفاعلي أمر بأمر.
• 	Batch Mode: تنفيذ كل الأوامر دفعة واحدة.
• 	Auto Merge: محاولة دمج التغييرات تلقائيًا.
• 	Rollback: تنفيذ أو معاينة سكريبتات التراجع عند الفشل.

🛡️ ميزات الأمان
- Safety Analysis: يمنع تنفيذ أوامر خطيرة (مثل Drop PK) إلا بموافقة.
- Impact Analysis: يوضح تأثير التغييرات على الجداول والبيانات.
- Versioning: تتبع كل ميجريشن برقم إصدار فريد.


flowchart TD
    A[اختيار الـ Types] -->|يدوي: List<Type>| B[RunMigrationSession]
    A2[اختيار الـ Types من Assembly/Assemblies] -->|فلترة بـ Base Class أو Interface| B

    B --> C[EntityDefinitionBuilder.BuildAllWithRelationships]
    C --> D[LoadEntityFromDatabase]
    D --> E[BuildMigrationScript]
    E --> F[AnalyzeImpact & SafetyAnalysis]
    F -->|عرض التقرير| G[ShowPreMigrationReport]

    F -->|تنفيذ| H{Interactive Mode?}
    H -->|Yes| I[ExecuteInteractiveAdvanced / Async]
    H -->|No| J[Execute / Async]

    I --> K[SqlScriptRunner.ExecuteScriptAsync]
    J --> K

    K --> L[MigrationHistoryStore.EnsureTableAndInsertPending]
    L --> M[تنفيذ SQL على قاعدة البيانات]
    M --> N[MigrationHistoryStore.MarkApplied / MarkFailed]
    N --> O[تحديث Snapshot Store]
    O --> P[طباعة Summary]
	
	شرح الرسم
- A / A2: نقطة البداية، إما تمرير قائمة Types يدويًا أو اختيارها من Assemblies مع فلترة.
- B: استدعاء RunMigrationSession أو أحد الأوفرلودز.
- C: بناء تعريفات الـ Entities وتحليل العلاقات.
- D: تحميل التعريف الحالي من قاعدة البيانات.
- E: توليد سكريبت الميجريشن.
- F: تحليل الأثر والأمان.
- G: عرض التقرير قبل التنفيذ (اختياري).
- H: تحديد وضع التنفيذ (تفاعلي أو تلقائي).
- I / J: تنفيذ السكريبتات.
- K: تنفيذ فعلي للأوامر عبر SqlScriptRunner.
- L: تسجيل الميجريشن في الـ History كـ Pending.
- M: تنفيذ الأوامر على قاعدة البيانات.
- N: تحديث حالة الميجريشن (Applied أو Failed).
- O: حفظ Snapshot جديد.
- P: عرض ملخص التنفيذ.


