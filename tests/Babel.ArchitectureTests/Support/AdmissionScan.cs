using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;

namespace Babel.ArchitectureTests.Support;

/// <summary>
/// <b>ماسح لغة وسيطة</b> يبني رسم الاستدعاء داخل تجميعة وحدة، ويسأل سؤالاً واحداً:
/// أي نقطة دخول عامة تبلغ رمز حدث <b>تفتحه قدرة</b>، ولا تبلغ بوابة القبول؟
/// <para>
/// <b>ولماذا لا يعدّ استدعاءات مكتوبة بيد:</b> فحصٌ يؤدّيه مستدعٍ واحد يُنسى في المستدعي
/// الثاني، وهذا بالضبط صنف العطل الذي يتكرّر هنا: «الموضع الذي يجيب عن السؤال ليس الموضع
/// الذي أُصلح». فالماسح يقرأ اللغة الوسيطة:
/// <list type="bullet">
///   <item>الأحداث المحروسة تُقرأ من <c>CapabilityCatalogue</c> نفسه — لا قائمة تنحرف عنه.</item>
///   <item>أي دالّة في التجميعة تحمل رمز حدث محروس نصّاً (‏<c>ldstr</c>) تُعدّ «حاملة».</item>
///   <item>كل نقطة دخول عامة على خدمة تطبيق تُتبَّع عبر رسم الاستدعاء (بما فيه آلات الحالة
///         غير المتزامنة والدوالّ المحلّية)، فإن بلغت حاملةً وجب أن تبلغ بوابة القبول.</item>
/// </list>
/// </para>
/// <para>
/// <b>ومُعمَّم على الوحدات:</b> كان يقرأ <c>Babel.Sales</c> وحدها، فكان «لا باب غير محروس»
/// جملةً عن وحدة واحدة. وحارسٌ يخصّ وحدةً بعينها لا يمتدّ إلى الثانية عند ربطها — والوحدة
/// الثانية هي التي يقع فيها العطل عادةً، لأن الأولى هي التي كُتب الحارس وهو ينظر إليها.
/// </para>
/// </summary>
internal sealed class AdmissionScan
{
    private static readonly OpCode[] SingleByte = new OpCode[0x100];
    private static readonly OpCode[] DoubleByte = new OpCode[0x100];

    static AdmissionScan()
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

    private AdmissionScan(
        IReadOnlyList<string> gatedEvents,
        IReadOnlyList<string> bearers,
        IReadOnlyList<string> guarded,
        IReadOnlyList<string> violations,
        int entryPointCount)
    {
        GatedEvents = gatedEvents;
        Bearers = bearers;
        Guarded = guarded;
        Violations = violations;
        EntryPointCount = entryPointCount;
    }

    public IReadOnlyList<string> GatedEvents { get; }

    public IReadOnlyList<string> Bearers { get; }

    public IReadOnlyList<string> Guarded { get; }

    public IReadOnlyList<string> Violations { get; }

    public int EntryPointCount { get; }

    /// <summary>يمسح تجميعة وحدة مقابل بوابتها.</summary>
    /// <param name="assembly">تجميعة الوحدة المفحوصة.</param>
    /// <param name="module">الوحدة في الكتالوج — منها تُقرأ الأحداث المحروسة.</param>
    /// <param name="admission">دالّة القبول التي يجب أن تبلغها كل نقطة دخول حاملة.</param>
    public static AdmissionScan Run(Assembly assembly, BabelModule module, MethodBase admission)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(admission);

        Assembly sales = assembly;

        // الأحداث المحروسة من الكتالوج نفسه — لا قائمة مكتوبة هنا تنحرف عنه.
        ImmutableArray<string> gated =
        [
            .. CapabilityCatalogue.DocumentTypes
                .Where(definition => definition.Module == module)
                .SelectMany(static definition => definition.Capabilities)
                .SelectMany(static capability => capability.RequiredEvents)
                .Select(static code => code.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        HashSet<string> gatedSet = new(gated, StringComparer.Ordinal);

        Dictionary<MethodBase, Body> bodies = [];
        foreach (Type type in sales.GetTypes())
        {
            foreach (MethodBase method in Methods(type))
            {
                bodies.TryAdd(method, Body.Of(method, sales));
            }
        }

        List<string> bearers = [];
        foreach ((MethodBase method, Body body) in bodies)
        {
            if (body.Literals.Any(gatedSet.Contains))
            {
                bearers.Add(Name(method));
            }
        }

        bearers.Sort(StringComparer.Ordinal);

        List<string> guarded = [];
        List<string> violations = [];
        int entryPoints = 0;

        foreach (Type service in sales.GetTypes()
                     .Where(static type => type is { IsClass: true, IsAbstract: false, IsPublic: true })
                     .Where(static type => typeof(IApplicationService).IsAssignableFrom(type))
                     .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            foreach (MethodInfo entry in service
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(static method => !method.IsSpecialName)
                         .OrderBy(static method => method.Name, StringComparer.Ordinal))
            {
                entryPoints++;

                HashSet<MethodBase> reached = Reach(entry, bodies);
                bool touchesGatedEvent = reached.Any(method =>
                    bodies.TryGetValue(method, out Body? body) && body.Literals.Any(gatedSet.Contains));

                if (!touchesGatedEvent)
                {
                    continue;
                }

                if (reached.Contains(admission))
                {
                    guarded.Add(Name(entry));
                }
                else
                {
                    violations.Add(Name(entry) + " يبلغ حدثاً تفتحه قدرة ولا يبلغ القبول");
                }
            }
        }

        return new AdmissionScan(gated, bearers, guarded, violations, entryPoints);
    }

    /// <summary>
    /// يجلب دالّة القبول بالانعكاس. والبوابة <c>internal</c> عمداً (القاعدة 5)، فلا سبيل
    /// إلى ذكرها بالاسم من هنا — والانعكاس يبلغها كما يبلغها الإنتاج.
    /// </summary>
    /// <param name="assembly">تجميعة الوحدة.</param>
    /// <param name="typeName">اسم نوع البوابة كاملاً.</param>
    /// <param name="methodName">اسم دالّة القبول.</param>
    public static MethodBase AdmissionOf(Assembly assembly, string typeName, string methodName)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Type gate = assembly.GetType(typeName, throwOnError: false)
            ?? throw new InvalidOperationException("لم يُعثر على نوع بوابة القبول: " + typeName);

        return gate.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("لم يُعثر على دالّة القبول: " + typeName + "." + methodName);
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

    /// <summary>يجمع كل ما تبلغه دالّة عبر رسم الاستدعاء داخل التجميعة نفسها.</summary>
    private static HashSet<MethodBase> Reach(MethodBase root, Dictionary<MethodBase, Body> bodies)
    {
        HashSet<MethodBase> seen = [];
        Queue<MethodBase> queue = new();
        queue.Enqueue(root);
        seen.Add(root);

        while (queue.Count > 0)
        {
            MethodBase current = queue.Dequeue();

            if (!bodies.TryGetValue(current, out Body? body))
            {
                continue;
            }

            foreach (MethodBase called in body.Calls)
            {
                if (seen.Add(called))
                {
                    queue.Enqueue(called);
                }
            }
        }

        return seen;
    }

    private static string Name(MethodBase method)
        => (method.DeclaringType?.Name ?? "?") + "." + method.Name;

    /// <summary>جسد دالّة مقروءاً من اللغة الوسيطة: نصوصها الحرفية وما تستدعيه.</summary>
    private sealed class Body
    {
        private Body(ImmutableArray<string> literals, ImmutableArray<MethodBase> calls)
        {
            Literals = literals;
            Calls = calls;
        }

        public ImmutableArray<string> Literals { get; }

        public ImmutableArray<MethodBase> Calls { get; }

        public static Body Of(MethodBase method, Assembly owner)
        {
            List<string> literals = [];
            List<MethodBase> calls = [];

            // الدالّة غير المتزامنة جسدها في آلة حالتها، لا في جسدها الظاهر.
            foreach (Type machine in StateMachines(method))
            {
                MethodInfo? move = machine.GetMethod(
                    "MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (move is not null)
                {
                    calls.Add(move);
                }
            }

            byte[]? il = null;
            try
            {
                il = method.GetMethodBody()?.GetILAsByteArray();
            }
            catch (InvalidOperationException)
            {
                il = null;
            }

            if (il is null)
            {
                return new Body([.. literals], [.. calls]);
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

                if (code.OperandType == OperandType.InlineString)
                {
                    try
                    {
                        literals.Add(module.ResolveString(BitConverter.ToInt32(il, operand)));
                    }
                    catch (ArgumentException)
                    {
                        // رمز لا يُحلّ: لا يُخمَّن ولا يُبتلع أثره — يُتخطّى وحده.
                    }
                }
                else if (code.OperandType is OperandType.InlineMethod or OperandType.InlineTok)
                {
                    try
                    {
                        MethodBase? resolved = module.ResolveMethod(
                            BitConverter.ToInt32(il, operand), typeArguments, methodArguments);

                        if (resolved is not null && resolved.Module.Assembly == owner)
                        {
                            calls.Add(resolved);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // ‏InlineTok قد يشير إلى نوع أو حقل لا إلى دالّة.
                    }
                }
            }

            return new Body([.. literals], [.. calls]);
        }

        private static IEnumerable<Type> StateMachines(MethodBase method)
        {
            if (method.GetCustomAttribute<AsyncStateMachineAttribute>() is { StateMachineType: { } asyncMachine })
            {
                yield return asyncMachine;
            }

            if (method.GetCustomAttribute<IteratorStateMachineAttribute>() is { StateMachineType: { } iterator })
            {
                yield return iterator;
            }
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
}
