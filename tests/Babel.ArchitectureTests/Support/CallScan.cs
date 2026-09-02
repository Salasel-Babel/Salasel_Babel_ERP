using System.Reflection;
using System.Reflection.Emit;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// <b>ماسحُ نداءات: مَن ينادي هذه الدالّة، في هذه التجميعة؟</b>
/// <para>
/// و<see cref="AdmissionScan"/> يجيب عن سؤالٍ آخر — «أيّ نقطة دخولٍ تبلغ حدثاً محروساً
/// بلا قبول؟» — ويبني رسم استدعاءٍ كاملاً لأجله، ويُسقط كلّ نداءٍ يخرج من تجميعته.
/// وهذا الماسح يسأل سؤالاً أصغر ويقرأ ما يُسقطه ذاك: <b>النداءات العابرة للتجميعات</b>،
/// وهي بالضبط ما يُقاس هنا (<c>UserId.SystemActor</c> في <c>Babel.SharedKernel</c>،
/// ومُنشئُ الهوية في <c>Babel.Api</c>).
/// </para>
/// <para>
/// <b>ويقرأ آلات الحالة</b>: جسدُ دالّةٍ غير متزامنة في <c>MoveNext</c> لا في جسدها
/// الظاهر — وماسحٌ ينسى ذلك يمرّ أخضر على كل مسارٍ غير متزامن، وهو أغلب هذا المستودع.
/// وأنواعُ المُصرِّف المُولَّدة تُقرأ لأنّها في <c>GetTypes()</c>، ويُنسب ما فيها إلى
/// <b>نوعها الخارجي</b> فيُقرأ الاسم كما كتبه الإنسان.
/// </para>
/// </summary>
internal static class CallScan
{
    private static readonly OpCode[] SingleByte = new OpCode[0x100];
    private static readonly OpCode[] DoubleByte = new OpCode[0x100];

    static CallScan()
    {
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode code)
            {
                continue;
            }

            if ((code.Value & 0xFF00) == 0xFE00)
            {
                DoubleByte[code.Value & 0xFF] = code;
            }
            else if (code.Value >= 0 && code.Value < 0x100)
            {
                SingleByte[code.Value] = code;
            }
        }
    }

    /// <summary>
    /// أسماءُ الأنواع التي يشير جسدُ إحدى دوالّها إلى دالّةٍ يقبلها <paramref name="wanted"/>،
    /// مرتَّبةً وبلا تكرار.
    /// </summary>
    /// <param name="assembly">التجميعة المفحوصة.</param>
    /// <param name="wanted">شرطُ الدالّة المطلوبة.</param>
    public static IReadOnlyList<string> CallersIn(Assembly assembly, Func<MethodBase, bool> wanted)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(wanted);

        SortedSet<string> callers = new(StringComparer.Ordinal);

        foreach (Type type in Types(assembly))
        {
            foreach (MethodBase method in Methods(type))
            {
                if (References(method, wanted))
                {
                    callers.Add(Outermost(type));
                }
            }
        }

        return [.. callers];
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException fault)
        {
            return fault.Types.Where(static type => type is not null)!;
        }
    }

    private static string Outermost(Type type)
    {
        Type walk = type;
        while (walk.DeclaringType is { } parent)
        {
            walk = parent;
        }

        return walk.FullName ?? walk.Name;
    }

    private static IEnumerable<MethodBase> Methods(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (MethodInfo method in type.GetMethods(All))
        {
            yield return method;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(All))
        {
            yield return constructor;
        }
    }

    private static bool References(MethodBase method, Func<MethodBase, bool> wanted)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (InvalidOperationException)
        {
            il = null;
        }
        catch (NotSupportedException)
        {
            il = null;
        }

        if (il is null)
        {
            return false;
        }

        Type[]? typeArguments = method.DeclaringType?.IsGenericType == true
            ? method.DeclaringType.GetGenericArguments()
            : null;

        Type[]? methodArguments = method.IsGenericMethodDefinition ? method.GetGenericArguments() : null;
        Module module = method.Module;

        int position = 0;
        while (position < il.Length)
        {
            OpCode code;
            if (il[position] == 0xFE && position + 1 < il.Length)
            {
                code = DoubleByte[il[position + 1]];
                position += 2;
            }
            else
            {
                code = SingleByte[il[position]];
                position += 1;
            }

            int operand = position;
            position += OperandSize(code, il, position);

            if (code.OperandType is not (OperandType.InlineMethod or OperandType.InlineTok))
            {
                continue;
            }

            try
            {
                MethodBase? resolved = module.ResolveMethod(
                    BitConverter.ToInt32(il, operand), typeArguments, methodArguments);

                if (resolved is not null && wanted(resolved))
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                // ‏InlineTok قد يشير إلى نوع أو حقل لا إلى دالّة — يُتخطّى وحده.
            }
        }

        return false;
    }

    private static int OperandSize(OpCode code, byte[] il, int position) => code.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
        _ => throw new InvalidOperationException("رمز عملية بمُعامل غير معروف: " + code.Name),
    };
}
