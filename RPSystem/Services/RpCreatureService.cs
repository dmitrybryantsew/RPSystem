using ChemCalculationAndManagementApp.RpSystem;

namespace ChemCalculationAndManagementApp.Services;

public static class RpCreatureService
{
    public static void EnsureCreatureStats(Character character)
    {
        character.Vitals.HealthMax = character.Vitals.HealthMax <= 0 ? 100 : character.Vitals.HealthMax;
        character.Vitals.HealthCurrent = ClampPositive(character.Vitals.HealthCurrent <= 0 ? character.Vitals.HealthMax : character.Vitals.HealthCurrent, character.Vitals.HealthMax);

        if (character.Vitals.StaminaMax <= 0)
        {
            character.Vitals.StaminaMax = character.StaminaMax > 0 ? character.StaminaMax : 100;
        }

        if (character.Vitals.StaminaCurrent <= 0)
        {
            character.Vitals.StaminaCurrent = character.StaminaCurrent > 0 ? character.StaminaCurrent : character.Vitals.StaminaMax;
        }

        if (character.Vitals.FocusMax <= 0)
        {
            character.Vitals.FocusMax = character.RpTags.Contains("sapient", StringComparer.OrdinalIgnoreCase) ||
                character.RpTags.Contains("creature", StringComparer.OrdinalIgnoreCase)
                ? 30
                : 0;
        }

        if (character.Vitals.FocusCurrent <= 0 && character.Vitals.FocusMax > 0)
        {
            character.Vitals.FocusCurrent = character.Vitals.FocusMax;
        }

        if (character.Vitals.FocusRegenPerTick <= 0 && character.Vitals.FocusMax > 0)
        {
            character.Vitals.FocusRegenPerTick = 2;
        }

        if (character.Vitals.StaminaRegenPerTick <= 0 && character.Vitals.StaminaMax > 0)
        {
            character.Vitals.StaminaRegenPerTick = 3;
        }

        character.StaminaMax = character.Vitals.StaminaMax;
        character.StaminaCurrent = character.Vitals.StaminaCurrent;
        character.ActionSpeeds.MoveSpeed = character.ActionSpeeds.MoveSpeed <= 0 ? 1 : character.ActionSpeeds.MoveSpeed;
        character.ActionSpeeds.AttackSpeed = character.ActionSpeeds.AttackSpeed <= 0 ? 1 : character.ActionSpeeds.AttackSpeed;
        character.ActionSpeeds.CastSpeed = character.ActionSpeeds.CastSpeed <= 0 ? 1 : character.ActionSpeeds.CastSpeed;

        if (!character.RpTags.Contains("creature", StringComparer.OrdinalIgnoreCase))
        {
            character.RpTags.Add("creature");
        }
    }

    private static float ClampPositive(float value, float max)
        => max <= 0 ? value : Math.Clamp(value, 0, max);
}
