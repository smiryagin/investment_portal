namespace WiseLine.Portal.Domain.Email;

public enum EmailDeliveryStatus
{
    Pending,
    Processing,
    Sent,
    Delivered,
    Delayed,
    Bounced,
    Complained,
    Suppressed,
    Failed
}
