using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Boundary;
using Babel.Ai.Lookup;
using Babel.Ai.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// <b>البوابة — بين نداء النموذج وأي تنفيذ، بترتيبٍ كلّيّ لا التفاف حوله.</b>
/// <list type="number">
///   <item>حلّ الاسم في الكتالوج المغلق — ما ليس فيه لا يُنفَّذ.</item>
///   <item><see cref="VoiceOperationGuard.Refuse"/> <b>حرفياً</b> — فعلٌ ممنوع أو فعلٌ
///         غير مصنَّف يسقطان معاً. فعمليةٌ تُنشر غداً بفعلٍ جديد لا تبلغ الوكيل حتى
///         يصنّفها إنسان.</item>
///   <item>السطح المنشور: العملية موجودة، ومسارها لا ينتهي بمقطعٍ لا يُعكَس — يُقرأ من
///         <b>المسار</b> لا من الاسم، فعمليةٌ تُسمّى غداً بأي اسمٍ ومسارُها «…/posting»
///         تبقى ممنوعة.</item>
///   <item>المِصفاة الخارجة على <b>كل</b> وسيطٍ نصّي — والنموذج قد يعيد إلينا ما شكلُه
///         معرّف فيصير جسمُ المسوّدة طريقاً ثانياً.</item>
///   <item>فكّ كل حقلٍ شكلُه معرّف: غرضاً ومنشأةً وشركةً وجلسة. ومعرّفٌ خام يكتبه
///         النموذج من عنده يُرفض.</item>
///   <item>حالة الدور: سقف البحث، وقاعدة السؤال بعد الغموض، ورفض السبر.</item>
///   <item>الاستحقاق للوحدة المالكة.</item>
/// </list>
/// <para>
/// ⇒ <see cref="AgentDispatch"/> — وهو النوع الوحيد الذي يقبله المنفّذ. <b>فمن نسي
/// البوابة لا يجد ما يمرّره.</b>
/// </para>
/// <para>
/// <b>وطبقةٌ ثانية فوقها:</b> الكتالوج نفسه لا يمكن أن يحمل عمليةً ممنوعة، لأنه يُرشَّح
/// عند التركيب ويُسقط التركيبَ إن بقي فيه ما ترفضه الخطوة الثالثة
/// (<see cref="AgentToolCatalogue.Load"/>). <b>وثالثةٌ قائمة أصلاً:</b> كلّ ما يُنتجه هذا
/// المسار مسوّدة، وكلّ <c>post…</c> يحتاج الشاشة.
/// </para>
/// </summary>
public static class AgentToolGate
{
    /// <summary>
    /// يأذن — أو يرفض ويسمّي. <b>ويُعيد كل أسباب الرفض لا أوّلها</b>: النموذج الذي يقرأ
    /// سبباً واحداً يُصلحه ثم يسقط على الثاني، ودورةً بعد دورة حتى ينفد السقف.
    /// </summary>
    /// <param name="call">النداء كما نطق به النموذج.</param>
    /// <param name="caller">المتكلّم ونطاقه وصلاحياته.</param>
    /// <param name="state">حالة الدور.</param>
    /// <param name="catalogue">الكتالوج المغلق.</param>
    /// <param name="handles">مُصدِر المقابض ومُستردّها.</param>
    public static Result<AgentDispatch> Authorise(
        AgentToolCall call,
        AgentCaller caller,
        AgentTurnState state,
        AgentToolCatalogue catalogue,
        ILookupHandles handles)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(handles);

        // ── ١ · الكتالوج المغلق ───────────────────────────────────────────────
        AgentTool? tool = catalogue.Resolve(call.Name);
        if (tool is null)
        {
            return Result<AgentDispatch>.Failure(AgentErrors.UnknownTool(call.Name));
        }

        // ── ٢ و ٣ · الحارس المنطوق ثم السطح المنشور ──────────────────────────
        List<Error> errors = [];

        if (tool.OperationId is not null)
        {
            string? why = VoiceOperationGuard.Refuse(tool.OperationId);
            if (why is not null)
            {
                errors.Add(AgentErrors.OperationRefused(tool.OperationId, why));
            }

            if (tool.Path is null)
            {
                errors.Add(AgentErrors.OperationNotPublished(tool.OperationId));
            }
            else
            {
                string? irreversible = AgentToolCatalogue.IrreversibleSegments.FirstOrDefault(
                    segment => tool.Path.EndsWith(segment, StringComparison.Ordinal));

                if (irreversible is not null)
                {
                    errors.Add(AgentErrors.OperationIsIrreversible(tool.OperationId, tool.Path));
                }
            }

            // ── ٧ · الاستحقاق ────────────────────────────────────────────────
            // يُفحص هنا لا في بناء الكتالوج: الكتالوج واحدٌ للجميع كي تُقرأ الذاكرة،
            // والتصفية تقع بعد أن ينطق النموذج وقبل أن يُنفَّذ شيء.
            if (!caller.PermittedOperationIds.Contains(tool.OperationId))
            {
                errors.Add(AgentErrors.NotEntitled(tool.OperationId));
            }
        }

        if (errors.Count > 0)
        {
            return Result<AgentDispatch>.Failure(errors);
        }

        // ── وسائطٌ تُفكّ JSON لا تُقرأ نصّاً ──────────────────────────────────
        JsonNode? body;
        try
        {
            body = JsonNode.Parse(call.ArgumentsJson);
        }
        catch (JsonException)
        {
            return Result<AgentDispatch>.Failure(AgentErrors.ToolArgumentsNotAnObject(tool.Name));
        }

        if (body is not JsonObject arguments)
        {
            return Result<AgentDispatch>.Failure(AgentErrors.ToolArgumentsNotAnObject(tool.Name));
        }

        // ── ٤ · المِصفاة الخارجة على كل وسيطٍ نصّي ────────────────────────────
        // ‏**والاتجاه هنا معكوس ظاهراً وصحيح واقعاً:** هذه قيمٌ *قادمة* من النموذج،
        // لكنها ستُكتب في جسم مسوّدة وتُقرأ في صدى القراءة الذي يعود إليه. فالمسار
        // نفسه، والمِصفاة تعمل على كل نصّ يعبره.
        //
        // ‏**ويُستثنى موضعُ المِقبض وحده، وبفحصٍ أقوى لا بأضعف منه:** ما في ذلك الموضع
        // يجب أن يكون مِقبضاً موقَّعاً بـHMAC لهذه الجلسة بعينها (الخطوة الخامسة)، ورقمُ
        // هويةٍ يُكتب هناك يسقط عند التوقيع لا عند الشكل. ولو فُحص بالشكل أيضاً لصار
        // المِقبض نفسه — وهو ‎142 محرفاً من base64url — يحمل باحتمالٍ ضئيلٍ ثابت سلسلةَ
        // تسع خانات فيُرفض نداءٌ سليم مرّةً في نصف مليون. وعطلٌ نادرٌ عشوائيّ أسوأ من
        // عطلٍ مطّرد: لا يُعاد إنتاجه فلا يُصلَح.
        HashSet<JsonNode> handleSlots = [];
        foreach (string idField in tool.IdFields)
        {
            foreach (JsonNode? slot in Locate(arguments, idField))
            {
                if (slot is not null)
                {
                    handleSlots.Add(slot);
                }
            }
        }

        if (string.Equals(tool.Name, AgentProtocolTools.AskQuestion, StringComparison.Ordinal)
            && arguments["questionId"] is { } questionSlot)
        {
            handleSlots.Add(questionSlot);
        }

        foreach (string text in Strings(arguments, handleSlots))
        {
            AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(text);
            if (verdict.IsRefused)
            {
                foreach (Error error in verdict.Errors)
                {
                    if (!errors.Exists(seen => string.Equals(seen.Code, error.Code, StringComparison.Ordinal)))
                    {
                        errors.Add(error);
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            return Result<AgentDispatch>.Failure(errors);
        }

        // ── ٦ · حالة الدور (أدوات البروتوكول) ────────────────────────────────
        if (string.Equals(tool.Name, AgentProtocolTools.LookupEntity, StringComparison.Ordinal))
        {
            Error? refusal = RefuseLookupCall(arguments, catalogue, state);
            if (refusal is not null)
            {
                return Result<AgentDispatch>.Failure(refusal);
            }

            return Result<AgentDispatch>.Success(new AgentDispatch(
                tool, call.Id, arguments.ToJsonString(), [], caller));
        }

        // ── ٥ · فكّ المقابض ──────────────────────────────────────────────────
        List<AgentRedeemedField> redeemed = [];

        if (string.Equals(tool.Name, AgentProtocolTools.AskQuestion, StringComparison.Ordinal))
        {
            Result<AgentRedeemedField> question = RedeemOne(
                arguments, "questionId", LookupHandlePurpose.Question, caller, handles);

            if (question.IsFailure)
            {
                return Result<AgentDispatch>.Failure(question.Errors);
            }

            redeemed.Add(question.Value);

            return Result<AgentDispatch>.Success(new AgentDispatch(
                tool, call.Id, arguments.ToJsonString(), redeemed, caller));
        }

        foreach (string field in tool.IdFields)
        {
            foreach (JsonNode? slot in Locate(arguments, field))
            {
                if (slot is null)
                {
                    continue;
                }

                Result<Guid> subject = Redeem(slot, field, LookupHandlePurpose.Entity, caller, handles);
                if (subject.IsFailure)
                {
                    errors.AddRange(subject.Errors);
                    continue;
                }

                redeemed.Add(new AgentRedeemedField(field, subject.Value));
                Replace(slot, subject.Value);
            }
        }

        return errors.Count > 0
            ? Result<AgentDispatch>.Failure(errors)
            : Result<AgentDispatch>.Success(new AgentDispatch(
                tool, call.Id, arguments.ToJsonString(), redeemed, caller));
    }

    /// <summary>قواعد الدور الثلاث على نداء بحث.</summary>
    private static Error? RefuseLookupCall(
        JsonObject arguments,
        AgentToolCatalogue catalogue,
        AgentTurnState state)
    {
        string? kind = arguments["kind"]?.GetValue<string>();
        string? text = arguments["text"]?.GetValue<string>();

        if (kind is null || !catalogue.RegisterKeys.Contains(kind, StringComparer.Ordinal))
        {
            return LookupErrors.NoRegisterSource(kind ?? string.Empty);
        }

        return text is null ? LookupErrors.EmptyText : state.RefuseLookup(kind, text);
    }

    private static Result<AgentRedeemedField> RedeemOne(
        JsonObject arguments,
        string field,
        LookupHandlePurpose purpose,
        AgentCaller caller,
        ILookupHandles handles)
    {
        JsonNode? slot = arguments[field];
        if (slot is null)
        {
            return Result<AgentRedeemedField>.Failure(AgentErrors.RawIdentifierInsteadOfHandle(field));
        }

        Result<Guid> subject = Redeem(slot, field, purpose, caller, handles);
        return subject.IsFailure
            ? Result<AgentRedeemedField>.Failure(subject.Errors)
            : Result<AgentRedeemedField>.Success(new AgentRedeemedField(field, subject.Value));
    }

    private static Result<Guid> Redeem(
        JsonNode slot,
        string field,
        LookupHandlePurpose purpose,
        AgentCaller caller,
        ILookupHandles handles)
    {
        if (slot.GetValueKind() != JsonValueKind.String)
        {
            return Result<Guid>.Failure(AgentErrors.RawIdentifierInsteadOfHandle(field));
        }

        string token = slot.GetValue<string>();

        // ‏**معرّفٌ خام يكتبه النموذج من عنده يُرفض قبل أن يُقارَن بشيء.** ولا يُسأل عنه
        // السجلّ «فلعلّه موجود»: سؤالٌ كهذا هو بعينه تسريبُ وجودٍ من عدمه.
        if (Guid.TryParse(token, out _))
        {
            return Result<Guid>.Failure(AgentErrors.RawIdentifierInsteadOfHandle(field));
        }

        Result<RedeemedLookupHandle> handle = handles.Redeem(
            token, purpose, caller.Tenant, caller.CompanyId, caller.SessionId);

        return handle.IsFailure
            ? Result<Guid>.Failure(handle.Errors)
            : Result<Guid>.Success(handle.Value.Subject);
    }

    /// <summary>يجد مواضع حقلٍ بمساره — و«‏[]» يعني كلّ عنصرٍ في المصفوفة.</summary>
    private static List<JsonNode?> Locate(JsonNode root, string path)
    {
        List<JsonNode?> current = [root];

        foreach (string step in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            List<JsonNode?> next = [];

            foreach (JsonNode? node in current)
            {
                if (node is null)
                {
                    continue;
                }

                if (string.Equals(step, "[]", StringComparison.Ordinal))
                {
                    if (node is JsonArray array)
                    {
                        next.AddRange(array);
                    }
                }
                else if (node is JsonObject entry && entry.TryGetPropertyValue(step, out JsonNode? child))
                {
                    next.Add(child);
                }
            }

            current = next;
        }

        return current;
    }

    /// <summary>يستبدل موضع المِقبض بما دلّ عليه، في مكانه من الشجرة.</summary>
    private static void Replace(JsonNode slot, Guid subject)
    {
        JsonNode value = JsonValue.Create(subject.ToString());

        switch (slot.Parent)
        {
            case JsonObject parent:
                parent[slot.GetPropertyName()] = value;
                break;
            case JsonArray array:
                array[slot.GetElementIndex()] = value;
                break;
            default:
                throw new InvalidOperationException("موضعُ مِقبضٍ بلا أب — وهو ما لا يقع في جسمٍ مفكوك.");
        }
    }

    /// <summary>كل قيمةٍ نصّية في الشجرة بأي عمق، عدا مواضع المقابض.</summary>
    private static IEnumerable<string> Strings(JsonNode? node, HashSet<JsonNode> exempt)
    {
        if (node is not null && exempt.Contains(node))
        {
            yield break;
        }

        switch (node)
        {
            case JsonObject entry:
                foreach (KeyValuePair<string, JsonNode?> property in entry)
                {
                    foreach (string text in Strings(property.Value, exempt))
                    {
                        yield return text;
                    }
                }

                break;

            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    foreach (string text in Strings(item, exempt))
                    {
                        yield return text;
                    }
                }

                break;

            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                yield return value.GetValue<string>();
                break;

            default:
                break;
        }
    }
}
