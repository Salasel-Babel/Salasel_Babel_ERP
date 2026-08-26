using System.Reflection;
using Babel.Contracts.Posting;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Contracts.Tests;

/// <summary>
/// <b>«سطرٌ بلا مركز تكلفة» شكلٌ غير قابل للتمثيل — لا شكلٌ يُفحَص.</b>
/// <para>
/// ‏ADR-0026 يقرّر أن لكل منشأة مركز تكلفة واحداً على الأقل وأن <c>CostCenterId</c> لا
/// يكون فارغاً في أي موضع. وكان النوع يقول <c>string?</c> ويحمل <c>PostingScope.None</c>:
/// أي أن <b>الموضع الذي يُجيب عن السؤال غير الموضع الذي يُعلنه</b>. وهذه المجموعة تفحص
/// أن الإعلان صار في النوع نفسه — فلا يحتاج أحدٌ أن يتذكّره.
/// </para>
/// </summary>
public sealed class CostCenterIsNotRepresentableAsAbsentTests
{
    [Fact]
    public void مركز_التكلفة_غير_قابل_لأن_يكون_فارغاً_في_النوع_نفسه()
    {
        PropertyInfo centre = typeof(PostingScope).GetProperty(nameof(PostingScope.CostCenterId))!;

        // النوع غير قابل لأن يكون null: NullabilityInfoContext يقرأ ما يقرؤه المترجم.
        NullabilityInfo nullability = new NullabilityInfoContext().Create(centre);

        Assert.Equal(NullabilityState.NotNull, nullability.ReadState);
        Assert.Equal(typeof(string), centre.PropertyType);

        // والفرع والمشروع يبقيان اختياريين — فالتضييق مقصود لا شامل.
        foreach (string optional in new[] { nameof(PostingScope.BranchId), nameof(PostingScope.ProjectId) })
        {
            NullabilityInfo info = new NullabilityInfoContext()
                .Create(typeof(PostingScope).GetProperty(optional)!);

            Assert.Equal(NullabilityState.Nullable, info.ReadState);
        }
    }

    /// <summary>
    /// <b>ولا <c>default</c> يلتفّ حول المُنشئ</b> — وهذا سبب أن النوع سجلٌّ مرجعي لا بنية.
    /// <para>
    /// بنيةٌ لها <c>default</c> دائماً ولا مُنشئ يمنعه، فـ<c>default(PostingScope)</c> كان
    /// يُعيد تركيب «نطاقٌ بلا مركز» مهما شُدِّد المُنشئ — أي أن الثابتة تبقى <b>وعداً</b>.
    /// </para>
    /// </summary>
    [Fact]
    public void النوع_سجلٌّ_مرجعي_فلا_قيمة_افتراضية_تلتفّ_حول_مُنشئه()
    {
        Assert.False(typeof(PostingScope).IsValueType, "PostingScope بنية — وللبنية default بلا مُنشئ.");

        // ولا عضو ساكن اسمه None: «لا نطاق» لم يكن حالة مشروعة في المجال أصلاً.
        Assert.Null(typeof(PostingScope).GetProperty("None", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(PostingScope).GetField("None", BindingFlags.Public | BindingFlags.Static));

        // وما حلّ محلّه: أفقر نطاق مشروع — المركز وحده.
        PostingScope poorest = PostingScope.On("cc.001");
        Assert.Equal("cc.001", poorest.CostCenterId);
        Assert.Null(poorest.BranchId);
        Assert.Null(poorest.ProjectId);
    }

    /// <summary>
    /// <b>شاهدٌ موجب: المُنشئ يرفض فعلاً كل صور الغياب.</b> ونوعٌ «غير قابل لأن يكون
    /// <c>null</c>» في التعليق وحده لا يمنع شيئاً وقت التشغيل — فالفحص هنا على السلوك.
    /// </summary>
    [Fact]
    public void المُنشئ_يرفض_كل_صور_الغياب_لا_الفراغ_وحده()
    {
        foreach (string? absent in new string?[] { null, "", " ", "\t", " " })
        {
            Assert.ThrowsAny<ArgumentException>(() => new PostingScope(absent!));
            Assert.ThrowsAny<ArgumentException>(() => PostingScope.On(absent!));
        }

        // ولا يرفض ما ليس غياباً.
        Assert.Equal("cc.001", new PostingScope(" cc.001 ").CostCenterId);
    }

    /// <summary>
    /// <b>والسطر لا يُبنى بلا نطاق إطلاقاً</b> — <c>required</c> على الخاصية، مقروءةً من
    /// البيانات الوصفية لا من قراءة الملف.
    /// </summary>
    [Fact]
    public void سطر_الترحيل_لا_يُبنى_بلا_نطاق()
    {
        PropertyInfo scope = typeof(PostingLine).GetProperty(nameof(PostingLine.Scope))!;

        Assert.Contains(
            scope.GetCustomAttributes(inherit: false),
            attribute => attribute.GetType().Name == "RequiredMemberAttribute");

        // وقيمةٌ سليمة تعبر كما هي: تضييقٌ لا يمنع الاستعمال المشروع.
        PostingLine line = new()
        {
            Role = PostingRole.NetAmount,
            Side = PostingSide.Credit,
            Amount = Money.Of(10m, CurrencyCode.Sar),
            Scope = new PostingScope("cc.007", branchId: "BR-01"),
        };

        Assert.Equal("cc.007", line.Scope.CostCenterId);
        Assert.Equal("BR-01", line.Scope.BranchId);
    }
}
