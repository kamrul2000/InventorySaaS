namespace InventorySaaS.Domain.Common.Enums;

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Overdue = 4,
    Cancelled = 5
}

public enum PaymentMethod
{
    Cash = 0,
    BankTransfer = 1,
    Card = 2,
    Cheque = 3,
    MobileBanking = 4,
    Other = 5
}
