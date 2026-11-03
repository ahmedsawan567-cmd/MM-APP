namespace MMApplication.Domain.Enums;

public enum SettlementStatus
{
    OPEN,
    PENDING_APPROVAL,
    SETTLED,
    CANCELED,               // ملغى ومؤرشف محاسبياً كوثيقة قانونية دون حذف فيزيائي
    Pending = PENDING_APPROVAL,
    Settled = SETTLED,
    Canceled = CANCELED
}
