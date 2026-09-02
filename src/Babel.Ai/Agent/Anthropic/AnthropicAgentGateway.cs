using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace Babel.Ai.Agent.Anthropic;

/// <summary>
/// <b>المحوّل إلى المزوّد — وهو <u>كل</u> ما يعرف حزمته في هذا المستودع.</b>
/// <para>
/// الحلقة والبوّابة والمِصفاة وحالة الدور كلّها مكتوبة على أنواعٍ محايدة، وهذا الملفّ
/// وحده يترجم بينها وبين <c>Anthropic.Models.Messages</c>. وثمنُ ذلك سطور ترجمة، وثمنُ
/// غيابه أن تُجَرّ حزمة المزوّد إلى كل اختبارٍ فتنفق كل مجموعةِ اختباراتٍ مالاً على كل
/// تشغيل — ومجموعةٌ كهذه تُطفأ خلال شهر.
/// </para>
/// <para>
/// <b>والمفتاح يُقرأ من البيئة عند النداء باسمٍ يأتي في الطلب</b> — لا من كائن إعدادات،
/// ولا من حقلٍ في هذا الصنف. وغيابُه عطلٌ يُعلَن باسم المتغيّر ولا يُذكر فيه شيء من قيمته.
/// </para>
/// <para>
/// <b>وترتيب العرض:</b> أدوات ← نظام ← رسائل. والأدوات تُبنى من الكتالوج المضمَّن مرّةً
/// واحدة لعمر هذا الكائن ولا تتغيّر بالمستخدم؛ ونقطة الذاكرة <b>واحدة</b> وعلى كتلة
/// النظام — وعلامةٌ على آخر كتلة نظام تُذاكر الأدوات والنظام معاً.
/// </para>
/// </summary>
public sealed class AnthropicAgentGateway : IAgentModelGateway
{
    private readonly ConcurrentDictionary<string, AnthropicClient> _clients = new(StringComparer.Ordinal);
    private readonly TimeSpan _timeout;
    private readonly Lazy<List<ToolUnion>> _tools;

    /// <summary>يركّب المحوّل.</summary>
    /// <param name="catalogue">الكتالوج المغلق — يُترجم مرّةً واحدة.</param>
    /// <param name="options">الإعدادات (المهلة).</param>
    public AnthropicAgentGateway(AgentToolCatalogue catalogue, AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(options);

        _timeout = options.Timeout;
        _tools = new Lazy<List<ToolUnion>>(() => Translate(catalogue));
    }

    /// <summary>
    /// يترجم الكتالوج إلى أدوات المزوّد. <b>مرّةً واحدة، وبلا أي أثرٍ للمتصل</b> —
    /// فـ<c>tools = build(user)</c> يمنح كل مستخدمٍ فضاء ذاكرةٍ خاصّاً فلا يُقرأ شيء أبداً.
    /// </summary>
    /// <param name="catalogue">الكتالوج.</param>
    public static List<ToolUnion> Translate(AgentToolCatalogue catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        List<ToolUnion> tools = new(catalogue.Tools.Count);

        foreach (AgentTool tool in catalogue.Tools)
        {
            tools.Add(new Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = JsonSerializer.Deserialize<InputSchema>(tool.InputSchemaJson)
                    ?? throw new InvalidOperationException("مخطّط أداةٍ لا يُفكّ: " + tool.Name),

                // ‏**strict** كي تكون وسائط النداء مطابقةً للمخطّط لا مقاربةً له —
                // والمخطّط نفسه فيه additionalProperties:false من العقد المنشور.
                Strict = true,
            });
        }

        return tools;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AgentModelEvent> StreamAsync(
        AgentModelRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AnthropicClient client = _clients.GetOrAdd(request.ApiKeyVariable, Build);

        // ‏**حالةُ تجميعٍ لكل نداء لا حقلٌ على الكائن.** المحوّل مفردٌ في الحاوية، وحقلٌ
        // مشترك يخلط كتل محادثتين متزامنتين — وهو عطلٌ لا يظهر إلا تحت حمل.
        StreamAssembly assembly = new();

        MessageCreateParams parameters = new()
        {
            Model = request.ModelId,

            // سقفٌ مرتفع، والمسار متدفّق — فلا مهلة HTTP على الطريق.
            MaxTokens = request.MaxOutputTokens,

            // ‏**العرض «مُلخَّص» صراحةً**: الافتراضي على هذا النموذج «محذوف»، وهو في لوحةٍ
            // يُفترض أن تُري تقدّماً يُقرأ صمتاً طويلاً لا تفكيراً.
            Thinking = new ThinkingConfigAdaptive { Display = Display.Summarized },

            // مثبَّتٌ لا مُمرَّر: تغيير الجهد في وسط محادثةٍ يُبطل ذاكرة الرسائل.
            OutputConfig = new OutputConfig { Effort = Effort.High },

            // كتلةٌ واحدة، وعليها نقطة الذاكرة الوحيدة — تُذاكر الأدوات والنظام معاً.
            System = new List<TextBlockParam>
            {
                new() { Text = request.SystemPrompt, CacheControl = new CacheControlEphemeral() },
            },

            Tools = _tools.Value,
            Messages = Compose(request),
        };

        await foreach (RawMessageStreamEvent streamEvent in client.Messages
            .CreateStreaming(parameters, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (AgentModelEvent translated in assembly.Absorb(streamEvent))
            {
                yield return translated;
            }
        }
    }

    private AnthropicClient Build(string variable)
    {
        string? key = Environment.GetEnvironmentVariable(variable);

        // ‏**غيابُ المفتاح عطلٌ يُعلَن، لا مفتاحٌ يُخترَع ولا ارتدادٌ صامت.** ولا يُكتب
        // في الرسالة شيءٌ من القيمة — الاسم وحده.
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(AgentErrors.ApiKeyMissing(variable).MessageAr);
        }

        return new AnthropicClient { ApiKey = key, Timeout = _timeout };
    }

    /// <summary>
    /// يجمع الكتل المتتالية ذات الدور الواحد في رسالةٍ واحدة. <b>والنصّ يُقرأ من الظرف
    /// وحده</b> — لا من نسخةٍ ثانية في الكتلة.
    /// </summary>
    private static List<MessageParam> Compose(AgentModelRequest request)
    {
        List<MessageParam> messages = [];
        List<ContentBlockParam> pending = [];
        AgentWireRole? role = null;

        void Flush()
        {
            if (role is null || pending.Count == 0)
            {
                return;
            }

            messages.Add(new MessageParam
            {
                Role = role switch
                {
                    AgentWireRole.User => Role.User,
                    AgentWireRole.Assistant => Role.Assistant,
                    _ => Role.System,
                },
                Content = new List<ContentBlockParam>(pending),
            });

            pending.Clear();
        }

        foreach (AgentWireBlock block in request.Blocks)
        {
            if (role != block.Role)
            {
                Flush();
                role = block.Role;
            }

            string text = request.TextOf(block);

            pending.Add(block.Kind switch
            {
                AgentWireBlockKind.Thinking => new ThinkingBlockParam
                {
                    Thinking = text,

                    // ‏**التوقيع كما ورد حرفاً بحرف** — وتعديلُه يُبطل الكتلة عند المزوّد.
                    Signature = block.Signature ?? string.Empty,
                },

                AgentWireBlockKind.ToolUse => new ToolUseBlockParam
                {
                    ID = block.ToolUseId ?? string.Empty,
                    Name = block.ToolName ?? string.Empty,
                    Input = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text)
                        ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                },

                AgentWireBlockKind.ToolResult => new ToolResultBlockParam
                {
                    ToolUseID = block.ToolUseId ?? string.Empty,
                    Content = text,
                    IsError = block.IsError,
                },

                _ => new TextBlockParam { Text = text },
            });
        }

        Flush();
        return messages;
    }

    /// <summary>
    /// يجمع أحداث التدفّق في كتلٍ كاملة. <b>ويبثّ الأجزاء أوّلاً بأوّل</b> — فاللوحة
    /// تُري تقدّماً، والنسخة تُبنى من الكتل المكتملة لتُعاد في النداء التالي.
    /// </summary>
    private sealed class StreamAssembly
    {
        private readonly Dictionary<long, Block> _open = [];
        private long _inputTokens;
        private long _cacheRead;
        private long _cacheCreated;

        public IEnumerable<AgentModelEvent> Absorb(RawMessageStreamEvent streamEvent)
        {
            if (streamEvent.TryPickStart(out RawMessageStartEvent? start))
            {
                _inputTokens = start.Message.Usage.InputTokens;
                _cacheRead = start.Message.Usage.CacheReadInputTokens ?? 0;
                _cacheCreated = start.Message.Usage.CacheCreationInputTokens ?? 0;
                _open.Clear();
                yield break;
            }

            if (streamEvent.TryPickContentBlockStart(out RawContentBlockStartEvent? opened))
            {
                Block block = new();

                if (opened.ContentBlock.TryPickToolUse(out ToolUseBlock? toolUse))
                {
                    block.ToolUseId = toolUse.ID;
                    block.ToolName = toolUse.Name;
                }
                else if (opened.ContentBlock.TryPickThinking(out ThinkingBlock? thinking))
                {
                    block.IsThinking = true;
                    block.Text.Append(thinking.Thinking);
                    block.Signature = thinking.Signature;
                }
                else if (opened.ContentBlock.TryPickText(out TextBlock? text))
                {
                    block.Text.Append(text.Text);
                }

                _open[opened.Index] = block;
                yield break;
            }

            if (streamEvent.TryPickContentBlockDelta(out RawContentBlockDeltaEvent? delta))
            {
                if (!_open.TryGetValue(delta.Index, out Block? block))
                {
                    yield break;
                }

                if (delta.Delta.TryPickText(out TextDelta? text))
                {
                    block.Text.Append(text.Text);
                    yield return AgentModelEvent.TextDelta(text.Text);
                }
                else if (delta.Delta.TryPickThinking(out ThinkingDelta? thinking))
                {
                    block.Text.Append(thinking.Thinking);
                    yield return AgentModelEvent.ThinkingDelta(thinking.Thinking);
                }
                else if (delta.Delta.TryPickSignature(out SignatureDelta? signature))
                {
                    block.Signature = signature.Signature;
                }
                else if (delta.Delta.TryPickInputJson(out InputJsonDelta? json))
                {
                    block.Arguments.Append(json.PartialJson);
                }

                yield break;
            }

            if (streamEvent.TryPickContentBlockStop(out RawContentBlockStopEvent? closed))
            {
                if (!_open.Remove(closed.Index, out Block? block))
                {
                    yield break;
                }

                if (block.ToolUseId is not null)
                {
                    string arguments = block.Arguments.Length == 0 ? "{}" : block.Arguments.ToString();
                    yield return AgentModelEvent.ToolCall(
                        new AgentToolCall(block.ToolUseId, block.ToolName ?? string.Empty, arguments));
                }
                else if (block.IsThinking)
                {
                    yield return AgentModelEvent.ThinkingBlock(block.Text.ToString(), block.Signature ?? string.Empty);
                }
                else
                {
                    yield return AgentModelEvent.TextBlock(block.Text.ToString());
                }

                yield break;
            }

            if (streamEvent.TryPickDelta(out RawMessageDeltaEvent? message))
            {
                yield return AgentModelEvent.Completed(
                    message.Delta.StopReason?.ToString() ?? "end_turn",
                    new AgentModelUsage(_inputTokens, message.Usage.OutputTokens, _cacheRead, _cacheCreated));
            }
        }

        private sealed class Block
        {
            public StringBuilder Text { get; } = new();

            public StringBuilder Arguments { get; } = new();

            public string? ToolUseId { get; set; }

            public string? ToolName { get; set; }

            public string? Signature { get; set; }

            public bool IsThinking { get; set; }
        }
    }
}
