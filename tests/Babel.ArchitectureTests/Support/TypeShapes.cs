using System.Reflection;
using System.Runtime.CompilerServices;

namespace Babel.ArchitectureTests.Support;

/// <summary>أدوات فحص الأنواع المشتركة بين القواعد.</summary>
internal static class TypeShapes
{
    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>أعضاء النوع المُعلنة فيه، دون ما يولّده المصرّف.</summary>
    public static IEnumerable<MemberInfo> DeclaredMembers(Type type) =>
        type.GetMembers(AllDeclared).Where(static member => !member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false));

    /// <summary>هل النوع مولَّد من المصرّف؟</summary>
    public static bool IsCompilerGenerated(Type type) =>
        type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
        || (type.FullName?.Contains('<', StringComparison.Ordinal) ?? false);

    /// <summary>هل النوع معلن عاماً على كل مستويات تعشيشه؟</summary>
    public static bool IsVisibleOutsideAssembly(Type type)
    {
        Type? current = type;
        while (current is not null)
        {
            if (current.IsNested)
            {
                if (!current.IsNestedPublic && !current.IsNestedFamily && !current.IsNestedFamORAssem)
                {
                    return false;
                }

                current = current.DeclaringType;
                continue;
            }

            return current.IsPublic;
        }

        return false;
    }

    /// <summary>يفكّ الأنواع المغلِّفة: <c>Nullable</c>، المصفوفات، المراجع، ووسائط الأنواع العامة.</summary>
    public static IEnumerable<Type> Unwrap(Type type)
    {
        Type current = type;
        if (current.IsByRef || current.IsPointer)
        {
            current = current.GetElementType() ?? current;
        }

        if (current.IsArray)
        {
            foreach (Type inner in Unwrap(current.GetElementType()!))
            {
                yield return inner;
            }

            yield break;
        }

        if (current.IsGenericType)
        {
            yield return current.GetGenericTypeDefinition();
            foreach (Type argument in current.GetGenericArguments())
            {
                foreach (Type inner in Unwrap(argument))
                {
                    yield return inner;
                }
            }

            yield break;
        }

        yield return current;
    }

    /// <summary>هل يشتق النوع من <c>DbContext</c>؟ فحص بالاسم لتفادي إدخال EF Core إلى مشروع الاختبار.</summary>
    public static bool IsDbContext(Type type)
    {
        Type? current = type.BaseType;
        while (current is not null)
        {
            if (current.FullName == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    /// <summary>إن كان النوع <c>DbSet&lt;T&gt;</c> أعاد <c>T</c>.</summary>
    public static Type? DbSetElement(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Microsoft.EntityFrameworkCore.DbSet`1"
            ? type.GetGenericArguments()[0]
            : null;

    /// <summary>نوع قيمة العضو: حقل أو خاصية أو ناتج دالة.</summary>
    public static IEnumerable<(string Description, Type Type)> ValueTypesOf(MemberInfo member)
    {
        switch (member)
        {
            case FieldInfo field:
                yield return ($"حقل {field.Name}", field.FieldType);
                break;

            case PropertyInfo property:
                yield return ($"خاصية {property.Name}", property.PropertyType);
                break;

            case MethodInfo method:
                if (method.ReturnType != typeof(void))
                {
                    yield return ($"ناتج {method.Name}", method.ReturnType);
                }

                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return ($"وسيط {method.Name}({parameter.Name})", parameter.ParameterType);
                }

                break;

            case ConstructorInfo constructor:
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    yield return ($"وسيط منشئ ({parameter.Name})", parameter.ParameterType);
                }

                break;

            default:
                break;
        }
    }
}
