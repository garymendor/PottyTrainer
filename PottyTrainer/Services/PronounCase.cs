using System.Runtime.Serialization;

namespace PottyTrainer.Services;

public enum PronounCase
{
    [EnumMember(Value = "s")]
    Subjective,
    [EnumMember(Value = "sc")]
    SubjectiveCapitalized,
    [EnumMember(Value = "o")]
    Objective,
    [EnumMember(Value = "oc")]
    ObjectiveCapitalized,
    [EnumMember(Value = "p")]
    Possessive,
    [EnumMember(Value = "pc")]
    PossessiveCapitalized,
    [EnumMember(Value = "r")]
    Reflexive,
    [EnumMember(Value = "rc")]
    ReflexiveCapitalized,
}