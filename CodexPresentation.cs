using System;

namespace CreatureCodex
{
    /// <summary>Pure layout and visibility rules, kept separate so screen safety is testable without Unity.</summary>
    internal static class CodexPresentation
    {
        internal static bool ShowSpecies(bool showUnknown, CodexKnowledge knowledge)
        {
            return showUnknown || knowledge != CodexKnowledge.Unknown;
        }

        internal static float EffectiveScale(float parentWidth, float parentHeight, float designWidth,
            float designHeight, float userScale)
        {
            if (parentWidth <= 0f || parentHeight <= 0f || designWidth <= 0f || designHeight <= 0f)
            {
                return 1f;
            }

            var preference = Clamp(userScale, 0.75f, 1.25f);
            var automatic = Math.Min(parentHeight * 0.8f / designHeight, parentWidth * 0.92f / designWidth);
            var maximum = Math.Min(parentHeight * 0.98f / designHeight, parentWidth * 0.98f / designWidth);
            var minimum = Math.Min(0.5f, maximum);
            return Clamp(automatic * preference, minimum, maximum);
        }

        internal static float ClampOffset(float value, float parentSize, float designSize, float scale)
        {
            var limit = Math.Max(0f, (parentSize - designSize * scale) * 0.5f);
            return Clamp(value, -limit, limit);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
