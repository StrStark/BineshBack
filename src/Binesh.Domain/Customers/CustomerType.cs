using System.Text.Json.Serialization;

namespace Binesh.Domain.Customers;

/// <summary>
/// Iranian accounting classification of a customer. Names are kept as the
/// original Persian transliteration because they're the accepted vocabulary
/// in the field — translating loses meaning.
///
/// Serialized as the name string in JSON regardless of the caller's
/// JsonSerializerOptions (the attribute below pins it). On reads, both names
/// and integer values are accepted for backward compat.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CustomerType>))]
public enum CustomerType
{
    /// <summary>No classification yet.</summary>
    None = 0,

    /// <summary>بدهکار — accounts payable side / debtors.</summary>
    Bedehkaran = 1,

    /// <summary>بستانکار — accounts receivable side / creditors.</summary>
    Bestankar = 2,

    /// <summary>پرسنل — employees.</summary>
    Personnel = 3,

    /// <summary>راننده — drivers (logistics).</summary>
    Ranandeh = 4,

    /// <summary>بازاریاب — sales agents on commission.</summary>
    Bazaryab = 5,

    /// <summary>شرکا — partners (shareholders / co-owners).</summary>
    Sherka = 6,

    /// <summary>مشتریان خانگی — home / retail customers.</summary>
    MoshtarianKhanegi = 7,

    /// <summary>جاری شرکت‌ها و اشخاص — corporate / individual current accounts.</summary>
    JariSherkathaVaAshkhas = 8,

    /// <summary>طراح و ادیتور — designers / editors (vendors of creative work).</summary>
    TarahVaEditor = 9,
}
