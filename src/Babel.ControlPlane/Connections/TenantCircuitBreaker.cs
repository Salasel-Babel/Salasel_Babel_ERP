namespace Babel.ControlPlane.Connections;

/// <summary>حالة قاطع الدارة لمستأجر واحد.</summary>
public enum CircuitState
{
    /// <summary>مغلق: المرور مسموح — الوضع الطبيعي.</summary>
    Closed,

    /// <summary>مفتوح: كل طلب يُرفض فوراً بلا استهلاك اتصال ولا انتظار مهلة.</summary>
    Open,

    /// <summary>نصف مفتوح: يُسمح بمحاولة استطلاع واحدة؛ فشلها يُعيد الفتح فوراً.</summary>
    HalfOpen
}

/// <summary>يُرفع حين يُرفض طلب لأن قاطع دارة المستأجر مفتوح.</summary>
/// <param name="tenantCode">رمز المستأجر غير القابل للوصول.</param>
/// <param name="remaining">ما تبقّى من مدّة الفتح.</param>
public sealed class CircuitOpenException(string tenantCode, TimeSpan remaining)
    : Exception($"قاعدة المستأجر «{tenantCode}» غير قابلة للوصول — القاطع مفتوح "
                + $"لمدة {remaining.TotalSeconds:F1} ثانية بعد. الرفض فوري بلا استهلاك اتصال.")
{
    /// <summary>رمز المستأجر الذي رُفض الطلب إليه.</summary>
    public string TenantCode { get; } = tenantCode;
}

/// <summary>
/// قاطع دارة لكل مستأجر.
///
/// <para><b>لماذا هو شرط بقاء لا تحسيناً:</b> بلا قاطع، كل طلب إلى مستأجر
/// معطوب ينتظر مهلة الاتصال كاملةً <b>وهو ممسك بحجز من السقف العام</b>. عشرة
/// طلبات في الثانية إلى مستأجر واحد ميّت بمهلة ثلاث ثوانٍ تحجز ثلاثين حجزاً
/// دائماً — أي أن مستأجراً واحداً يُسقِط المنصّة كلها.</para>
///
/// <para>الرفض هنا يقع <b>قبل</b> طلب الحجز: تكلفته صفر اتصال وصفر انتظار.</para>
/// </summary>
/// <param name="failureThreshold">عدد الإخفاقات المتتالية التي تفتح القاطع.</param>
/// <param name="openDuration">مدّة البقاء مفتوحاً قبل السماح بمحاولة استطلاع.</param>
/// <param name="clock">ساعة قابلة للحقن — للاختبار؛ الافتراضي ساعة النظام.</param>
public sealed class TenantCircuitBreaker(int failureThreshold, TimeSpan openDuration,
    Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Lock _gate = new();

    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private CircuitState _state = CircuitState.Closed;

    /// <summary>عدد الطلبات المرفوضة فوراً بلا استهلاك اتصال — وهو المورد الذي وُجد القاطع ليحميه.</summary>
    public long RejectedFast { get; private set; }

    /// <summary>عدد مرات انتقال القاطع إلى الحالة المفتوحة.</summary>
    public long Trips { get; private set; }

    /// <summary>الحالة الآنية، بعد تطبيق انتهاء مدّة الفتح.</summary>
    public CircuitState State
    {
        get { lock (_gate) { Refresh(); return _state; } }
    }

    private void Refresh()
    {
        if (_state == CircuitState.Open && _now() - _openedAt >= openDuration)
            _state = CircuitState.HalfOpen;
    }

    /// <summary>يرمي فوراً إن كان القاطع مفتوحاً. يُنادى قبل حجز أي مورد.</summary>
    /// <param name="tenantCode">رمز المستأجر — يدخل في رسالة الرفض.</param>
    /// <exception cref="CircuitOpenException">القاطع مفتوح.</exception>
    public void ThrowIfOpen(string tenantCode)
    {
        lock (_gate)
        {
            Refresh();
            if (_state != CircuitState.Open) return;
            RejectedFast++;
            throw new CircuitOpenException(tenantCode, openDuration - (_now() - _openedAt));
        }
    }

    /// <summary>يُسجّل نجاحاً: يُصفّر العدّاد ويُغلق القاطع.</summary>
    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

    /// <summary>
    /// يُسجّل إخفاقاً. في الحالة نصف المفتوحة يكفي إخفاق واحد لإعادة الفتح —
    /// لا تُمنح الفرصة مرتين لمستأجر يتذبذب.
    /// </summary>
    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            // في نصف المفتوح: فشل واحد يكفي لإعادة الفتح — لا نُعطي الفرصة مرتين.
            if (_state == CircuitState.HalfOpen || _consecutiveFailures >= failureThreshold)
            {
                if (_state != CircuitState.Open) Trips++;
                _state = CircuitState.Open;
                _openedAt = _now();
            }
        }
    }
}
