using Babel.SharedKernel;

namespace Babel.Core;

/// <summary>بطاقة الوحدة. Rule09 يتحقق من وجود بطاقة لكل عضو في <see cref="BabelModule"/>.</summary>
public static class CoreModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Core;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("النواة", "Core");
}
