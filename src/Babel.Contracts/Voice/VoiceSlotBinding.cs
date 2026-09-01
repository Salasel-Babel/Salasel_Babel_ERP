namespace Babel.Contracts.Voice;

/// <summary>
/// كيف تمتلئ شريحةٌ في خطوةٍ من خطّة.
/// </summary>
/// <param name="SlotName">
/// اسم الشريحة <b>كما تُعلنها النيّة التي تسمّيها الخطوة</b>. وربطٌ يسمّي شريحةً لا
/// تُعلنها النيّة يُسقط بناء السجلّ — لا نُطقاً واحداً.
/// </param>
/// <param name="Source">مصدر القيمة.</param>
public sealed record VoiceSlotBinding(string SlotName, VoiceSlotSource Source);
