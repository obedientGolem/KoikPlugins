using HarmonyLib;
using KKAPI.Chara;
using System;
using System.Collections.Generic;
using System.Text;

namespace Kokyu
{
    internal static class Helpers
    {
        internal const string PregnancyGUID = "KK_Pregnancy";
        internal static T GetCharacterComponent<T>(ChaControl chara, string extendedDataId) where T : CharaCustomFunctionController
        {
            if (chara == null) return null;

            var components = CharacterApi.GetBehaviours(chara);

            if (components == null) return null;

            foreach (var component in components)
            {
                if (component.ExtendedDataId == extendedDataId)
                    return (T)component;
            }
            return null;
        }

        internal static int GetPregnancyWeek(ChaControl chara)
        {
            var component = GetCharacterComponent<CharaCustomFunctionController>(chara, PregnancyGUID);

            if (component == null) return -1;

            var data = component.GetType().GetProperty("Data")?.GetValue(component, null);
            if (data == null) return -1;

            var week = Traverse.Create(data).Field("Week").GetValue<int>();
            if (week.Equals(null) || week < -1) return -1;

            return week;
        }
    }
}
