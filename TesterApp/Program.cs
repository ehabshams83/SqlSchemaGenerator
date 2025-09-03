using Syn.Core.SqlSchemaGenerator;

using TesterApp;
using TesterApp.Models;
using TesterApp.Models.MTM;
using TesterApp.Models.OTO;

class Program
{
    static void Main()
    {

        // الاتصال بقاعدة البيانات
        var connectionString = DbConnectionString.connectionString;

        // إنشاء الـ Runner
        var runner = new MigrationRunner(connectionString);

        // تحديد الكيانات
        var entityTypes = new List<Type>
{
    typeof(Customer),
    typeof(Order)
};

        // تشغيل المايجريشن مع الـ Preview + Impact Analysis
        runner.Initiate(
            entityTypes,
            execute: true,          // هنشغّل التنفيذ بعد الموافقة
            dryRun: false,          // مش عرض بس، هننفذ لو وافقنا
            interactive: false,     // مش محتاجين موافقة لكل أمر على حدة
            previewOnly: true,      // عرض التقرير قبل التنفيذ
            autoMerge: false,       // مش تلقائي بالكامل
            showReport: true,       // عرض التقرير التفصيلي
            impactAnalysis: true    // تحليل المخاطر
        );

        //var connectionString = "YourConnectionString";
        //var runner = new MigrationRunner(connectionString);

        //// مثال على تجميع كل الـ Entities
        //var entityTypes = new List<Type>
        //{
        //    typeof(User),
        //    typeof(Product),
        //    typeof(Order),
        //    typeof(Tag)
        //};

        //Console.WriteLine("=== Database Migration Console UI ===");
        //Console.WriteLine("1) Normal Execution");
        //Console.WriteLine("2) Dry Run");
        //Console.WriteLine("3) Interactive Mode");
        //Console.WriteLine("4) Preview Mode");
        //Console.WriteLine("5) Auto Merge Mode");
        //Console.WriteLine("6) Pre-Migration Report");
        //Console.WriteLine("7) Impact Analysis");

        //Console.Write("اختر الوضع: ");
        //var choice = Console.ReadLine();

        //bool execute = true, dryRun = false, interactive = false, preview = false, autoMerge = false, showReport = false, impactAnalysis = false;

        //switch (choice)
        //{
        //    case "1": break;
        //    case "2": dryRun = true; break;
        //    case "3": interactive = true; break;
        //    case "4": preview = true; break;
        //    case "5": autoMerge = true; break;
        //    case "6": showReport = true; break;
        //    case "7": impactAnalysis = true; showReport = true; break;
        //    default: Console.WriteLine("❌ اختيار غير صحيح."); return;
        //}

        //runner.RunMigrationSession(
        //    entityTypes,
        //    execute,
        //    dryRun,
        //    interactive,
        //    preview,
        //    autoMerge,
        //    showReport,
        //    impactAnalysis
        //);
    }
}