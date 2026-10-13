namespace Gameya.Domain.Enums;

public enum AssociationStatus { Draft = 1, Active = 2, Paused = 3, Completed = 4, Cancelled = 5 }
public enum MembershipStatus { Active = 1, Exited = 2, Replaced = 3 }
public enum MembershipKind { Paying = 1, Advance = 2 }
public enum PeriodStatus { Upcoming = 1, Due = 2, Closed = 3 }
public enum PayoutRecipientType { Membership = 1, ReserveSlot = 2 }
public enum PayoutStatus { Scheduled = 1, Completed = 2 }
public enum PaymentMethod { Cash = 1, InstaPay = 2, VodafoneCash = 3 }
public enum PaymentEntryType { Payment = 1, Adjustment = 2 }
public enum SettlementStatus { Pending = 1, Completed = 2 }
