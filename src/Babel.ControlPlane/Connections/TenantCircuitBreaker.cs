namespace Babel.ControlPlane.Connections;

public enum CircuitState { Closed, Open, HalfOpen }

public sealed class CircuitOpenException(string tenantCode, TimeSpan remaining)
    : Exception($"قاعدة المستأجر «{tenantCode}» غير قابلة للوصول — القاطع مفتوح "
                + $"لمدة {remaining.TotalSeconds:F1} ثانية بعد. الرفض فوري بلا استهلاك اتصال.")
{
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
public sealed class TenantCircuitBreaker(int failureThreshold, TimeSpan openDuration,
    Func<DateTimeOffset>? clock = null)
{
    private readonly Func<DateTimeOffset> _now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Lock _gate = new();

    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private CircuitState _state = CircuitState.Closed;

    public long RejectedFast { get; private set; }
    public long Trips { get; private set; }

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

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
            _state = CircuitState.Closed;
        }
    }

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
