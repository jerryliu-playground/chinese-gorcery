namespace ChineseGrocery.Sim.Data;

/// <summary>economy.json "shifts": when the morning shift ends and the evening one starts, and what half a day pays.</summary>
public sealed record ShiftSettingsDef(int MorningEndMinuteOfDay, int EveningStartMinuteOfDay, int HalfShiftWagePermille);
